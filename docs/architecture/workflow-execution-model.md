# Workflow Execution Model

## Status

Accepted

## Purpose

This document defines the initial workflow execution model for Lunar
Asset Studio.

The goal is to represent the execution journey that transforms user
intent into generated assets without introducing a complete workflow
engine prematurely.

## Context

Lunar Asset Studio will orchestrate multiple specialized capabilities:

-   image generation
-   model generation
-   validation
-   rigging
-   optimization
-   export preparation

Each capability may be provided by different implementations or
providers.

The workflow model must represent execution history and produced
artifacts while remaining independent from specific tools or vendors.

## Decision

The first version introduces the concept of a Workflow Execution.

A Workflow Execution represents a single attempt to execute a generation
process.

It does not represent:

-   a workflow designer
-   a visual node editor
-   a scheduler
-   a distributed workflow engine

Those capabilities may be introduced later if required.

## Core Concepts

### Workflow Execution

Represents one execution instance.

Responsibilities:

-   identify the execution
-   identify the Asset being processed
-   identify the Workflow Definition being executed
-   track lifecycle status
-   provide traceability

Every Workflow Execution references:

-   `AssetId AssetId` — the Asset being processed.
-   `WorkflowDefinitionId WorkflowDefinitionId` — the logical Workflow
    Definition being executed.
-   `int WorkflowDefinitionVersion` — the exact immutable version of that
    definition. This permits an execution to continue referring to the
    exact historical definition even after later versions are introduced.
-   `long Revision` — persistence-state revision for optimistic concurrency.
    This is distinct from `WorkflowDefinitionVersion`: the definition version
    identifies the exact immutable process definition, while `Revision`
    protects mutable execution persistence from stale concurrent writes.
    Initial value is `0`; each successful persisted update increments it by
    one. Lifecycle methods (`Start`, `Complete`, `Fail`, `Cancel`) do not
    change `Revision`; only repository persistence determines the next
    stored revision.

An execution cannot be created without all three reference values. The
version must be a positive integer (`>= 1`). See the
[Workflow Definition Model](./workflow-definition-model.md) for the definition
and capability concepts, and
[ADR-004](../decisions/ADR-004-workflow-definition-versioning.md) for the
versioning decision.

### Workflow Execution Status

Initial statuses:

-   Created
-   Running
-   Completed
-   Failed
-   Cancelled

The model should evolve only when real requirements appear.

### Lifecycle State Machine

Lifecycle rules are owned by `Lunar.Core`. The `WorkflowExecution` entity
exposes explicit intent methods that enforce valid transitions. No
external setter for `Status`, `StartedAt`, or `CompletedAt` exists.

Valid states and transitions:

```text
Created
   |
   v
Running
   |
   +--> Completed
   |
   +--> Failed
   |
   +--> Cancelled
```

Allowed transitions:

```text
Created  --> Running    (Start)
Running  --> Completed   (Complete)
Running  --> Failed      (Fail)
Running  --> Cancelled   (Cancel)
```

All other transitions are invalid. Invalid transition requests are
no-ops: the entity state, timestamps, and revision remain unchanged.
Terminal states (`Completed`, `Failed`, `Cancelled`) reject all
transitions.

Transition invariants:

-   `Start` is valid only from `Created`. It sets `StartedAt` to the
    current UTC time. It does not change `Revision`.
-   `Complete` is valid only from `Running`. It sets `CompletedAt` to the
    current UTC time. It does not change `Revision` or `StartedAt`.
-   `Fail` is valid only from `Running`. It sets `CompletedAt` to the
    current UTC time. It does not change `Revision` or `StartedAt`.
-   `Cancel` is valid only from `Running`. It sets `CompletedAt` to the
    current UTC time. It does not change `Revision` or `StartedAt`.

Timestamp rules:

-   `Created`: `StartedAt = null`, `CompletedAt = null`;
-   `Running`: `StartedAt != null`, `CompletedAt = null`;
-   terminal (`Completed`/`Failed`/`Cancelled`): both `StartedAt != null`
    and `CompletedAt != null`.

Invalid transitions do not modify timestamps. Repeated valid calls from
the same state are no-ops and do not overwrite timestamps.

Ownership:

-   Core owns lifecycle rules, transition validation, and timestamp
    management.
-   Application may request transitions by calling `Start`, `Complete`,
    `Fail`, or `Cancel`. The domain decides whether the transition is
    valid.
-   Application does not directly set `Status`, `StartedAt`, or
    `CompletedAt`.

## Relationship With Artifacts

Workflow executions produce artifacts.

Example:

User request:

"Create a dark fantasy warrior"

Execution:

    WorkflowExecution
        |
        +-- Concept Image Artifact
        |
        +-- Generated Model Artifact
        |
        +-- Rigged Model Artifact

Artifacts remain independent domain entities.

An Artifact stores an optional `SourceExecutionId` identifying the workflow
execution that produced it. The relationship is optional because imported or
user-provided artifacts may not originate from a Lunar workflow execution.

An Artifact also stores `SourceArtifactIds`, a read-only collection of
direct source `ArtifactId` values identifying earlier Artifacts it was
derived from. `SourceExecutionId` and `SourceArtifactIds` are
independent provenance dimensions: an Artifact may have either, both, or
neither. `SourceArtifactIds` records only direct sources and does not
expand transitive lineage.

Because a Workflow Execution now references the exact definition version
via `(WorkflowDefinitionId, WorkflowDefinitionVersion)`, the full
traceability chain from an Artifact to the exact immutable Workflow
Definition version — and its ordered Workflow Steps and Capabilities —
is preserved without duplicating definition provenance onto the Artifact.

The Workflow Execution does not own an in-memory collection of artifacts in
this initial model. This keeps persistence and aggregate decisions outside the
domain until they are required.

## Persistence Boundary

Core owns a specific persistence contract for Workflow Executions:

```text
IWorkflowExecutionRepository
```

Its current responsibilities are:

-   add a new execution (`TryAddAsync`);
-   retrieve an execution by `WorkflowExecutionId` (`GetAsync`);
-   persist a lifecycle change using expected `Revision`
    (`TryUpdateAsync`).

Repository identity is `WorkflowExecutionId`. `TryAddAsync` accepts only
executions with `Revision = 0` and rejects duplicate IDs without
overwriting. `GetAsync` returns `null` for a valid but unknown ID.
`TryUpdateAsync` uses optimistic concurrency: `expectedRevision` must
match the stored `Revision`. A successful state-changing update increments
`Revision` by one and returns the persisted execution. A stale update
returns `null` without modifying stored state. A no-op update (identical
lifecycle state) returns the current persisted execution without
incrementing `Revision`.

Immutable fields (`Id`, `AssetId`, `WorkflowDefinitionId`,
`WorkflowDefinitionVersion`, `CreatedAt`) cannot be changed through
`TryUpdateAsync`; attempts are rejected with `ArgumentException`. Invalid
lifecycle transitions (e.g. `Running` → `Created`, `Completed` →
`Running`) are also rejected with `ArgumentException`.

The in-memory adapter stores and returns isolated snapshots reconstructed
via `WorkflowExecution.Rehydrate`. Mutation of a caller's original object,
a retrieved object, or a previously updated object does not affect stored
repository state. Only a successful `TryUpdateAsync` changes persisted
state.

Infrastructure provides the first concrete adapter:

```text
InMemoryWorkflowExecutionRepository
```

This is a development/test implementation. It is not a production database
decision and is not durable.

## Rehydration

`WorkflowExecution.Rehydrate` is a narrowly scoped reconstruction factory
for persistence. It accepts all persisted fields including `Revision` and
validates structural invariants:

-   `WorkflowExecutionId` cannot be empty;
-   `AssetId` cannot be empty;
-   `WorkflowDefinitionId` cannot be empty;
-   `WorkflowDefinitionVersion >= 1`;
-   `Revision >= 0`;
-   status/timestamp coherence:
    -   `Created`: `StartedAt = null`, `CompletedAt = null`;
    -   `Running`: `StartedAt != null`, `CompletedAt = null`;
    -   terminal (`Completed`/`Failed`/`Cancelled`): both
        `StartedAt != null` and `CompletedAt != null`.

It does not act as an unrestricted backdoor to invalid lifecycle states.

## Application Orchestration

The Application layer coordinates workflow execution creation through
`ExecuteWorkflowService`. The service:

-   resolves the referenced Asset through `IAssetRepository` (returns
    `AssetNotFound` if missing);
-   retrieves the exact Workflow Definition version through
    `IWorkflowDefinitionRepository` (returns `WorkflowDefinitionNotFound`
    if missing);
-   creates a `WorkflowExecution` through the domain `Create` factory;
-   persists the new execution through `IWorkflowExecutionRepository`
    (returns `WorkflowExecutionPersistenceFailed` if rejected);
-   returns a `Result<WorkflowExecution>` outcome.

The service requires the referenced Asset to exist before creating a
Workflow Execution. A structurally valid `AssetId` that does not
correspond to a persisted Asset is an expected use-case failure, not a
domain exception. The service does not own domain invariants, lifecycle
transitions, or persistence mechanics. It does not duplicate domain
validation — input validation is delegated to the domain `Create`
factory and repository contracts. It does not introduce CQRS.

Expected use-case failures are returned as `Result` failures, not
thrown. `AssetNotFound` is returned when the referenced Asset does not
exist. `WorkflowDefinitionNotFound` is returned when the requested
definition version does not exist. `WorkflowExecutionPersistenceFailed`
is returned when the repository rejects insertion. Invalid
caller/programmer usage (null dependencies, invalid domain construction)
remains exception-based. Core does not depend on `Result` or any
Application concern.

## Future Evolution

Possible future concepts:

-   workflow templates
-   retries
-   checkpoints
-   resumable executions
-   provider selection

These are intentionally not part of the first implementation.

## Principles

-   Keep domain models simple.
-   Avoid premature abstraction.
-   Avoid coupling with external providers.
-   Prefer explicit models over generic frameworks.
