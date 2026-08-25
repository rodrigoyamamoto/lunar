# Lunar Asset Studio - Domain Boundaries

## Purpose

This document defines the initial domain boundaries of Lunar Asset
Studio.

The objective is not to create a complex architecture prematurely, but
to establish clear ownership boundaries so the platform can evolve
without coupling the core product to specific models, vendors, engines,
or execution environments.

------------------------------------------------------------------------

# Domain Overview

Lunar Asset Studio is an AI-assisted asset creation platform.

The user provides an initial creative intent:

-   reference images
-   descriptions
-   prompts
-   target requirements

The platform coordinates a pipeline of specialized capabilities that
transform this intent into production-ready assets.

The orchestration belongs to Lunar. Individual generation capabilities
remain replaceable.

------------------------------------------------------------------------

# Core Domain

The Core domain contains concepts that represent the product itself.

It must not depend on:

-   AI providers
-   local models
-   cloud services
-   Blender
-   Unreal Engine
-   Unity
-   file systems
-   databases

## Asset

Represents a creative output managed by Lunar.

Examples:

-   character
-   weapon
-   environment prop
-   texture
-   animation package
-   3D model

The Asset concept represents identity and lifecycle, not implementation
details.

An Asset carries a required human-readable `Name` that identifies the
creative entity to humans. The name is domain identity, not a file name,
storage key, or engine asset path. It cannot be null, empty, or
whitespace-only. The `AssetId` cannot be empty.

------------------------------------------------------------------------

## Artifact

Represents a generated or produced file/result during the asset
lifecycle.

Examples:

-   concept image
-   generated mesh
-   texture map
-   rigged model
-   Unreal-ready package

An Asset can have multiple Artifacts.

An Artifact records two independent provenance dimensions:

-   `SourceExecutionId` — the optional Lunar workflow execution that
    produced it.
-   `SourceArtifactIds` — the direct artifact-to-artifact lineage,
    identifying which earlier Artifacts this Artifact was derived from.

The two dimensions are independent: an Artifact may have either, both,
or neither. `SourceArtifactIds` records only direct sources; transitive
lineage is not expanded. Source order is preserved, duplicates and
self-references are rejected, and the exposed collection is immutable.

Artifact is fully immutable: all properties are get-only and there are
no mutation methods. Because of this, `Artifact.Rehydrate` is not
required — the in-memory repository stores and returns the same
immutable instance without reconstruction.

Core owns a persistence contract for Artifacts
(`IArtifactRepository`) keyed by `ArtifactId`. `TryAddAsync` inserts an
Artifact if absent (returns `false` if the exact identity already
exists; never overwrites). `GetAsync` retrieves by ID or returns
`null`. Infrastructure provides a concrete in-memory adapter
(`InMemoryArtifactRepository`). The repository persists Artifact domain
objects only — it does not store physical files, blobs, or binary data.
The persistence technology remains replaceable because Core owns the
contract.

------------------------------------------------------------------------

## Workflow

Represents the sequence of operations required to transform an input
into a final result.

A workflow may contain:

-   generation steps
-   validation steps
-   processing steps
-   export steps

The user should not need to manually understand every internal step.

A Workflow Definition has a stable logical identity
(`WorkflowDefinitionId`) and an immutable positive version number
(`Version`). The exact definition version is identified by
`(WorkflowDefinitionId, Version)`. Changing a definition creates a new
immutable version rather than mutating the previous one.

Core owns a persistence contract for Workflow Definitions
(`IWorkflowDefinitionRepository`) keyed by `(WorkflowDefinitionId, Version)`.
Infrastructure provides a concrete in-memory adapter
(`InMemoryWorkflowDefinitionRepository`). The persistence technology remains
replaceable because Core owns the contract.

------------------------------------------------------------------------

## Capability

Represents something Lunar knows how to do.

Examples:

-   generate image
-   create 3D mesh
-   extract parts
-   generate rig
-   optimize geometry
-   export package

Capabilities are abstractions.

The implementation behind a capability can change.

------------------------------------------------------------------------

## Execution

Represents a running operation.

Examples:

-   generating a character
-   processing a mesh
-   exporting Unreal assets

A Workflow Execution references the exact immutable definition version
via `WorkflowDefinitionId` and `WorkflowDefinitionVersion`, so an
execution continues to refer to the historical definition it was created
against even after later versions are introduced.

A Workflow Execution also carries a `Revision` for optimistic concurrency
in persistence. This is distinct from `WorkflowDefinitionVersion`:
`WorkflowDefinitionVersion` identifies the exact immutable process
definition, while `Revision` protects mutable execution persistence from
stale concurrent writes.

Core owns a persistence contract for Workflow Executions
(`IWorkflowExecutionRepository`) keyed by `WorkflowExecutionId`.
`TryUpdateAsync` uses expected `Revision` for optimistic concurrency.
Infrastructure provides a concrete in-memory adapter
(`InMemoryWorkflowExecutionRepository`). The persistence technology remains
replaceable because Core owns the contract.

Execution contains lifecycle information:

-   created
-   running
-   completed
-   failed
-   cancelled

Lifecycle transitions are owned by Core. The `WorkflowExecution` entity
exposes explicit intent methods (`Start`, `Complete`, `Fail`, `Cancel`)
that enforce valid state transitions. Invalid transitions are no-ops.
Terminal states reject all transitions. Application may request
transitions but cannot directly set `Status`, `StartedAt`, or
`CompletedAt`. See
[Workflow Execution Model](./workflow-execution-model.md) for the
complete state machine.

------------------------------------------------------------------------

# Application Layer

The Application layer coordinates use cases by depending on Core
abstractions. It does not reference Infrastructure directly; the API
composes Application services with Infrastructure adapters at runtime.

`ExecuteWorkflowService` is the first Application service. It
coordinates workflow execution creation: it resolves the referenced
Asset, retrieves the exact Workflow Definition version, creates a
`WorkflowExecution` through the domain factory, and persists it through
the Core-owned repository contract. The service requires the referenced
Asset to exist before creating a Workflow Execution.

`StartWorkflowExecutionService` is the second Application service. It
coordinates starting an existing `WorkflowExecution`: it loads the
execution, checks the caller-supplied expected revision, requests the
domain `Start()` transition, and persists the change through optimistic
concurrency. The service returns the persisted execution with the
incremented revision on success.

The Application layer does not own domain invariants, lifecycle
transitions, or persistence mechanics. It does not duplicate domain
validation — input validation is delegated to the domain `Create` factory
and repository contracts. It does not introduce CQRS.

Application services use a Result pattern for expected use-case outcomes.
`Result<T>` is owned by `Lunar.Application` and does not leak into Core.
`Result<T>.Success(value)` represents a successful outcome;
`Result<T>.Failure(error)` represents an expected use-case failure such
as `AssetNotFound`, `WorkflowDefinitionNotFound`,
`WorkflowExecutionPersistenceFailed`, `WorkflowExecutionNotFound`,
`WorkflowExecutionConcurrencyConflict`, or
`WorkflowExecutionCannotStart`. Invalid caller/programmer usage (null
dependencies, invalid domain construction, negative expected revision)
remains exception-based; expected use-case outcomes are returned as
`Result` failures, not thrown.

------------------------------------------------------------------------

# Infrastructure Boundary

Infrastructure provides technical implementations required by the Core.

Examples:

## Persistence

Responsible for storing:

-   assets
-   artifacts
-   workflow state
-   execution history

The domain should not know the database technology.

------------------------------------------------------------------------

## Provider Adapters

Responsible for integrating with external systems.

Examples:

-   local AI models
-   cloud AI providers
-   inference servers

The Core should only know the capability contract.

------------------------------------------------------------------------

## File System

Responsible for handling physical storage.

Examples:

-   generated images
-   meshes
-   exported packages

------------------------------------------------------------------------

## Worker Communication

Responsible for communicating with specialized workers.

Examples:

-   Python inference workers
-   Blender automation workers
-   conversion workers

Workers are replaceable execution environments.

------------------------------------------------------------------------

# Workers Boundary

Workers execute specialized tasks.

Examples:

## AI Generation Worker

Responsible for:

-   image generation
-   model generation
-   inference execution

------------------------------------------------------------------------

## Blender Worker

Responsible for automation tasks:

-   importing assets
-   mesh operations
-   rig preparation
-   exporting formats

Lunar should communicate through contracts, not Blender internals.

------------------------------------------------------------------------

## Export Worker

Responsible for preparing final outputs.

Examples:

-   Unreal packages
-   texture bundles
-   optimized meshes

------------------------------------------------------------------------

# Dependency Direction

The dependency direction must remain:

    API
     |
    Infrastructure
     |
    Core

The Core is the center of the system.

External technologies depend on Lunar abstractions, never the opposite.

------------------------------------------------------------------------

# Initial Design Principles

## Replaceability

A provider or model can be replaced without rewriting the product.

------------------------------------------------------------------------

## Simplicity

Do not introduce abstractions without a real requirement.

------------------------------------------------------------------------

## Explicit Boundaries

Every component must have a clear responsibility.

------------------------------------------------------------------------

## No Hidden Configuration

No hardcoded:

-   provider settings
-   paths
-   model names
-   execution parameters

Configuration belongs outside the code.

------------------------------------------------------------------------

# Current Scope

This document intentionally does not define:

-   database schema
-   API contracts
-   workflow engine implementation
-   AI model selection
-   deployment architecture

Those decisions should emerge when the product requires them.
