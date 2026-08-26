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

`ICapabilityExecutor` is the provider-independent Core execution port
that bridges a `WorkflowStep.CapabilityId` to an actual invocation. It
receives a `CapabilityExecutionRequest` (authoritative Lunar execution
context and a typed `CapabilityExecutionInput`) and returns a
`CapabilityExecutionOutcome` — either `CapabilityExecutionSucceeded`
(wrapping a `CapabilityExecutionOutput` with physical `ArtifactContent`)
or `CapabilityExecutionFailed` (wrapping a `CapabilityExecutionFailure`
with a provider-independent `Kind` and optional `RetryAfter`). The
executor does not own `ArtifactId`, `AssetId`, `SourceExecutionId`, or
`CreatedAt` — Lunar assigns those when constructing the `Artifact`. The
first production executor is `CloudflareWorkersAiTextToImageExecutor` in
`Lunar.Infrastructure`, targeting Cloudflare Workers AI
`@cf/black-forest-labs/flux-1-schnell`. No Cloudflare type, model name,
error code, or HTTP concept enters Core or Application.

`CapabilityExecutionInput` is a minimal abstract record that serves as
the typed capability-input family carried by `CapabilityExecutionRequest`.
It exists so the already-generic `ICapabilityExecutor` can carry
different concrete input shapes over time without introducing a generic
parameter bag, dictionary, JSON payload, or universal `Prompt` field.

`TextPromptInput` is the first concrete `CapabilityExecutionInput`. It
represents textual creative intent and owns the semantic validity of a
text prompt: `Prompt` cannot be null, empty, or whitespace-only, and a
valid prompt is preserved exactly without trimming or normalization.
`TextPromptInput` is one concrete capability input, not a universal
field required by every future capability. Future capability input types
may be introduced only when concrete product requirements justify them.

`ArtifactContent` is a minimal abstract record that serves as the
provider-independent physical-content family carried by
`CapabilityExecutionOutput`. It exists so the capability execution
boundary can carry actual produced bytes, not just metadata, while
remaining independent of any specific provider, storage system, file
API, or transport.

`BinaryArtifactContent` is the first concrete `ArtifactContent`. It
holds in-memory binary bytes plus a provider-independent `MediaType`
string. It owns a defensive copy of caller-supplied bytes and rejects
null/empty data and null/empty/whitespace media type. No file path,
URL, blob key, stream, hash, or storage location is present.

`ProducedArtifact` is an Application-layer use-case result that pairs
the persisted `Artifact` metadata with the in-memory `ArtifactContent`.
It is not a domain aggregate and has no repository. The physical content
is in-memory only; it is not durably stored, may be lost if the process
exits, and may be lost if a later Application step fails after the
executor produced content. Durable content storage is a future slice.

Capability input is currently passed to the executor in-memory for the
invocation and is not yet persisted as historical per-step invocation
state. Lunar does not yet have a historical input snapshot for each
invocation; full invocation reconstruction and input audit trails remain
a future requirement. Physical content is durably persisted through the
provider-neutral `IArtifactContentStore` Core port; the first
implementation is `LocalFileArtifactContentStore` in Infrastructure. One
logical Artifact currently carries one `ArtifactContent`. Multi-file
bundles, streaming, chunking, and large-output modeling are deferred to
future slices. See `docs/architecture/artifact-content-storage.md` for
the storage boundary, durable representation, atomic publication, and
compensation semantics.

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
abstractions. It does not reference Infrastructure directly. The API is
the intended composition boundary and will compose Application services
with Infrastructure adapters once API use cases require that dependency.
`Lunar.Api` currently references `Lunar.Core` and `Lunar.Infrastructure`
only; it does not yet reference `Lunar.Application`.

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

`RecordWorkflowArtifactService` is the third Application service. It
records an already-created `Artifact` as an output of an existing running
`WorkflowExecution`: it loads the execution, requires it to be `Running`,
verifies that the Artifact carries matching workflow provenance
(`SourceExecutionId`), verifies that the Artifact's `AssetId` matches the
execution's `AssetId`, and persists the Artifact through the Core-owned
`IArtifactRepository` contract. The service does not generate the
Artifact, invoke providers/models, or dispatch capabilities. It does not
mutate `WorkflowExecution` or automatically complete it. It does not
resolve or traverse `SourceArtifactIds`; cross-Asset direct lineage
remains permitted because only Artifact ownership relative to the
execution is checked. Artifact persistence remains insert-only.

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
`WorkflowExecutionConcurrencyConflict`,
`WorkflowExecutionCannotStart`, `WorkflowExecutionNotRunning`,
`ArtifactWorkflowProvenanceMissing`,
`ArtifactWorkflowExecutionMismatch`, `ArtifactWorkflowAssetMismatch`, or
`ArtifactPersistenceFailed`. Invalid caller/programmer usage (null
dependencies, null Artifact, invalid domain construction, negative
expected revision) remains exception-based; expected use-case outcomes
are returned as `Result` failures, not thrown.

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
