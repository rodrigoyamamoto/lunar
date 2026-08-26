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
    -   `Running`: `StartedAt != null`, `CompletedAt = null`,
        `CreatedAt <= StartedAt`;
    -   terminal (`Completed`/`Failed`/`Cancelled`): both
        `StartedAt != null` and `CompletedAt != null`,
        `CreatedAt <= StartedAt <= CompletedAt`.

Chronological ordering uses non-strict comparisons (`<=`) so that
transitions sharing the same clock resolution remain valid.

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

### Starting an Existing Execution

`StartWorkflowExecutionService` coordinates the `Created -> Running`
lifecycle transition for an existing `WorkflowExecution`. The service:

-   loads the execution through `IWorkflowExecutionRepository.GetAsync`
    (returns `WorkflowExecutionNotFound` if missing);
-   checks that the caller-supplied `expectedRevision` matches the loaded
    revision (returns `WorkflowExecutionConcurrencyConflict` if already
    stale at read time);
-   requests the domain transition through `WorkflowExecution.Start()`
    (returns `WorkflowExecutionCannotStart` if the domain no-ops from
    `Running`, `Completed`, `Failed`, or `Cancelled`);
-   persists the transition through
    `IWorkflowExecutionRepository.TryUpdateAsync` with optimistic
    concurrency (returns `WorkflowExecutionConcurrencyConflict` if a
    concurrent update raced between read and update);
-   returns the persisted `WorkflowExecution` with the incremented
    `Revision` on success.

Core owns the `Start()` transition rule. Application owns use-case
orchestration and expected failure translation. Infrastructure owns
optimistic persistence implementation. The Application service detects
domain no-ops by comparing lifecycle state before and after calling
`Start()` — it does not duplicate the Core transition table.

The revision pre-check does not eliminate races: a concurrent writer can
update persisted state between the read and the `TryUpdateAsync` call.
Both the pre-check and the `TryUpdateAsync` null result are handled as
`WorkflowExecutionConcurrencyConflict`. No automatic retry or state
merge occurs.

`expectedRevision` is the persistence-state revision for optimistic
concurrency, distinct from `WorkflowDefinitionVersion` (the immutable
definition identity). Negative `expectedRevision` is invalid
caller/programmer input and remains exception-based. It is validated
before repository lookup so the outcome is deterministic regardless of
persistence state. A non-negative but stale `expectedRevision` is an
expected use-case failure returned as
`WorkflowExecutionConcurrencyConflict`.

### Recording Workflow Output

`RecordWorkflowArtifactService` coordinates recording an already-created
`Artifact` as an output of an existing running `WorkflowExecution`. The
service:

-   loads the execution through `IWorkflowExecutionRepository.GetAsync`
    (returns `WorkflowExecutionNotFound` if missing);
-   requires the execution status to be `Running` (returns
    `WorkflowExecutionNotRunning` otherwise);
-   requires the Artifact to carry workflow provenance through
    `Artifact.SourceExecutionId` (returns
    `ArtifactWorkflowProvenanceMissing` if absent);
-   requires `Artifact.SourceExecutionId` to exactly equal the requested
    `WorkflowExecutionId` (returns
    `ArtifactWorkflowExecutionMismatch` if it differs);
-   requires `Artifact.AssetId` to exactly equal
    `WorkflowExecution.AssetId` (returns
    `ArtifactWorkflowAssetMismatch` if it differs);
-   persists the Artifact through `IArtifactRepository.TryAddAsync`
    (returns `ArtifactPersistenceFailed` if the insert is rejected);
-   returns the recorded `Artifact` on success.

The service does not generate the Artifact, invoke providers/models, or
dispatch capabilities. It does not mutate `WorkflowExecution` or
automatically complete it. It does not resolve or traverse
`SourceArtifactIds`; cross-Asset direct lineage remains permitted
because only Artifact ownership relative to the execution is checked.
Artifact persistence remains insert-only; the service does not update,
replace, or delete Artifacts.

`ExecuteWorkflowStepService` invokes the capability referenced by one
`WorkflowStep` and records its output as a Lunar-owned `Artifact`,
returning a `ProducedArtifact` that pairs the persisted metadata with
the in-memory physical content. The service:

-   rejects `stepPosition < 1` with `ArgumentException` before any
    repository lookup;
-   rejects `null` input with `ArgumentNullException` before any
    repository lookup;
-   loads the `WorkflowExecution` (returns `WorkflowExecutionNotFound`
    if missing);
-   requires `Running` status (returns `WorkflowExecutionNotRunning`
    otherwise);
-   loads the exact `WorkflowDefinition` version recorded by the
    execution (returns `WorkflowDefinitionNotFound` if the exact version
    is missing — no latest-version fallback);
-   resolves the `WorkflowStep` by semantic `Position` (returns
    `WorkflowStepNotFound` if no such step exists);
-   builds a `CapabilityExecutionRequest` from authoritative loaded
    state and the caller-supplied `CapabilityExecutionInput` (passed
    through unchanged);
-   invokes `ICapabilityExecutor.ExecuteAsync` (unexpected exceptions
    propagate; cancellation propagates as `OperationCanceledException`);
-   on `CapabilityExecutionFailed`, returns `WorkflowStepExecutionFailed`
    with the exact `WorkflowExecutionId`, `StepPosition`, and the validated
    `CapabilityExecutionFailure` (carrying `Kind` and `RetryAfter`) — no
    Artifact is constructed or persisted;
-   on `CapabilityExecutionSucceeded`, creates an `Artifact` with Lunar-owned
    `ArtifactId`, `AssetId`, and `SourceExecutionId` — the executor cannot
    supply or override these;
-   persists the Artifact through `IArtifactRepository.TryAddAsync`
    (returns `ArtifactPersistenceFailed` if rejected);
-   returns a `ProducedArtifact` pairing the persisted `Artifact` with
    the executor's `CapabilityExecutionOutput.Content` (passed through
    unchanged) on success.

The service does not mutate `WorkflowExecution`. It does not maintain
persistent per-step runtime state, advance a current-step pointer, or
automatically progress to the next step. Repeated calls for the same
`(WorkflowExecutionId, StepPosition)` may invoke the capability again
and produce another Artifact — there is no idempotency semantic yet.

The caller supplies a typed `CapabilityExecutionInput`. The service
passes it through to the `CapabilityExecutionRequest` unchanged — it
does not transform, clone, validate, or interpret the input beyond the
null check. The first concrete input is `TextPromptInput` (textual
creative intent). Capability input is not persisted as historical
per-step invocation state in this slice; Lunar does not yet have a
historical input snapshot for each invocation.

The executor returns a `CapabilityExecutionOutput` carrying physical
`ArtifactContent`. The service passes the content through to the
`ProducedArtifact` unchanged — it does not transform, clone, or
re-encode it. The first concrete content type is `BinaryArtifactContent`
(in-memory bytes plus `MediaType`). Physical content is in-memory only;
it is not durably persisted, may be lost if the process exits, and may
be lost if metadata persistence fails after the executor produced
content. Durable content storage, streaming, chunking, multi-file
bundles, and content hashing/deduplication are deferred to future
slices. One logical Artifact currently carries one `ArtifactContent`.

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
