# Application Error Handling

This document establishes the Application-layer error-handling conventions
for Lunar Asset Studio. It defines the Result pattern ownership, failure
classification, and validation framework policy.

## Result Pattern Ownership

The `Result<T>` type and `ApplicationError` hierarchy belong exclusively to
`Lunar.Application`. They do not exist in and are not referenced by
`Lunar.Core` or `Lunar.Infrastructure`.

```text
Lunar.Application
    Result<T>
    ApplicationError
    WorkflowDefinitionNotFound
    WorkflowExecutionPersistenceFailed
    WorkflowExecutionNotRunning
    ArtifactWorkflowProvenanceMissing
    ArtifactWorkflowExecutionMismatch
    ArtifactWorkflowAssetMismatch
    ArtifactPersistenceFailed
```

Allowed:

```text
Lunar.Application -> Result<T>
Task<Result<T>> as an Application service return type
```

Not allowed:

```text
Lunar.Core -> Result<T>
Lunar.Infrastructure -> Result<T>
Domain entities returning Result<T>
Value objects returning Result<T>
```

Core must remain independent of Application concerns.

## Failure Classification

### Expected use-case outcomes

Use `Result<T>.Failure(error)`.

These are outcomes the application can anticipate and represent
explicitly. They are not programmer errors or unexpected technical
failures.

Examples:

```text
Workflow definition does not exist
Workflow execution could not be persisted
Requested operation is not available
Business rule prevents execution
```

Current concrete errors:

-   `AssetNotFound` — the referenced Asset was not found in the
    repository;
-   `WorkflowDefinitionNotFound` — the requested definition version was
    not found in the repository;
-   `WorkflowExecutionPersistenceFailed` — the repository rejected
    insertion of a new execution;
-   `WorkflowExecutionNotFound` — the referenced Workflow Execution was
    not found in the repository;
-   `WorkflowExecutionConcurrencyConflict` — the caller-supplied expected
    revision no longer matches persisted state (either already stale at
    read time or a concurrent update raced between read and update);
-   `WorkflowExecutionCannotStart` — the Workflow Execution exists but
    cannot be started from its current lifecycle state;
-   `WorkflowExecutionNotRunning` — the Workflow Execution exists but is
    not in the `Running` status required for recording workflow output;
-   `ArtifactWorkflowProvenanceMissing` — the Artifact carries no
    `SourceExecutionId`, so it cannot be recorded as workflow output;
-   `ArtifactWorkflowExecutionMismatch` — the Artifact's
    `SourceExecutionId` differs from the requested `WorkflowExecutionId`;
-   `ArtifactWorkflowAssetMismatch` — the Artifact's `AssetId` differs
    from the `WorkflowExecution.AssetId`;
-   `ArtifactPersistenceFailed` — the Artifact repository rejected
    insertion (e.g. duplicate identity).

### Programmer/domain errors

Remain exceptions.

These represent invalid usage or broken invariants. They should not be
caught and converted into `Result` failures.

Examples:

```text
null dependency injection
invalid constructor argument
broken domain invariant
invalid object creation
empty identifier
invalid version number
```

Core entities, factories, and value objects throw `ArgumentException` or
`ArgumentNullException` for these cases. Application services let these
propagate.

### Unexpected technical failures

Do not blindly convert into `Result` failures.

Examples:

```text
database unavailable
network failure
unexpected provider crash
filesystem failure
```

These require an explicit resilience policy when durable infrastructure is
introduced. The current in-memory adapters do not produce these failures.
Do not introduce catch-all exception-to-Result conversion in Application
services. Each unexpected failure category requires a deliberate decision
in a future slice.

## Current Implementation

`ExecuteWorkflowService` returns `Result<WorkflowExecution>` from
`ExecuteAsync`:

-   **Success:** `Result<WorkflowExecution>.Success(execution)` when the
    Asset exists, the definition exists, and the execution is persisted;
-   **Expected failure:** `Result<WorkflowExecution>.Failure(...)` when
    the Asset is not found, the definition is not found, or persistence
    is rejected;
-   **Exceptions:** `ArgumentNullException` for null dependencies;
    `ArgumentException` from repository contracts or
    `WorkflowExecution.Create` for invalid identifiers or version;
    `OperationCanceledException` for cancelled tokens.

`StartWorkflowExecutionService` returns `Result<WorkflowExecution>` from
`StartAsync`:

-   **Success:** `Result<WorkflowExecution>.Success(persistedExecution)`
    when the execution exists, the revision matches, the domain accepts
    the Start transition, and persistence succeeds. The returned
    execution has the incremented `Revision`;
-   **Expected failure:** `Result<WorkflowExecution>.Failure(...)` when
    the execution is not found (`WorkflowExecutionNotFound`), the
    expected revision is stale
    (`WorkflowExecutionConcurrencyConflict`), a concurrent update races
    between read and update (`WorkflowExecutionConcurrencyConflict`), or
    the domain rejects the Start transition from the current lifecycle
    state (`WorkflowExecutionCannotStart`);
-   **Exceptions:** `ArgumentNullException` for null dependencies;
    `ArgumentException` for empty `WorkflowExecutionId` (from repository
    contract) or negative `expectedRevision` (invalid caller input);
    `OperationCanceledException` for cancelled tokens.

Concurrency distinction:

-   Negative `expectedRevision` is invalid caller/programmer input and
    remains exception-based. It is validated before repository lookup so
    the outcome is deterministic regardless of persistence state.
-   A non-negative but stale `expectedRevision` is an expected use-case
    failure and is returned as `WorkflowExecutionConcurrencyConflict`.

`RecordWorkflowArtifactService` returns `Result<Artifact>` from
`RecordAsync`:

-   **Success:** `Result<Artifact>.Success(artifact)` when the execution
    exists, is `Running`, the Artifact carries matching workflow
    provenance, the Artifact's `AssetId` matches the execution's
    `AssetId`, and the Artifact is persisted;
-   **Expected failure:** `Result<Artifact>.Failure(...)` when the
    execution is not found (`WorkflowExecutionNotFound`), the execution
    is not `Running` (`WorkflowExecutionNotRunning`), the Artifact lacks
    workflow provenance (`ArtifactWorkflowProvenanceMissing`), the
    Artifact's `SourceExecutionId` differs from the requested execution
    (`ArtifactWorkflowExecutionMismatch`), the Artifact's `AssetId`
    differs from the execution's `AssetId`
    (`ArtifactWorkflowAssetMismatch`), or the repository rejects the
    insert (`ArtifactPersistenceFailed`);
-   **Exceptions:** `ArgumentNullException` for null dependencies or a
    null Artifact; `ArgumentException` for an empty
    `WorkflowExecutionId` (from repository contract);
    `OperationCanceledException` for cancelled tokens.

The service does not generate the Artifact, invoke providers/models, or
dispatch capabilities. It does not mutate `WorkflowExecution` or
automatically complete it. It does not resolve or traverse
`SourceArtifactIds`; cross-Asset direct lineage remains permitted
because only Artifact ownership relative to the execution is checked.
Artifact persistence remains insert-only.

No `catch` blocks exist in Application code. No exception-to-Result
conversion is performed. Domain exceptions propagate naturally.

## FluentValidation Policy

FluentValidation is not currently introduced.

It may be introduced in a future slice only when Application-layer
request validation becomes complex enough to justify it. Complex means:
multiple cross-field rules, user input composition, or request shapes
that cannot be validated by simple guard clauses.

### When introduced

Allowed:

```text
Lunar.Application -> FluentValidation
```

Not allowed:

```text
Lunar.Core -> FluentValidation
```

FluentValidation must not replace:

-   entity invariants;
-   value object validity;
-   identifier validity;
-   domain lifecycle rules.

Those remain in Core.

Appropriate FluentValidation use cases:

```text
GenerateAssetRequest
ImportReferenceRequest
ProviderExecutionRequest
```

Appropriate rules:

```text
required fields
cross-field validation
user input validation
request composition rules
```

If a future slice introduces FluentValidation:

-   justify the dependency in an ADR;
-   add tests;
-   ensure it stays in Application;
-   do not leak validators into Core;
-   propose a separate refactor slice for any existing validation that
    should move to FluentValidation.

## Architecture Compliance

-   `Result<T>` is owned by `Lunar.Application` only;
-   `Lunar.Core` has no reference to `Result` or `ApplicationError`;
-   `Lunar.Infrastructure` has no reference to `Result` or
    `ApplicationError`;
-   `Lunar.Application` depends only on `Lunar.Core`;
-   no external packages are used for Result or error handling;
-   no generic abstractions (`IResult`, `ResultBase`) are introduced;
-   no catch-all exception conversion exists;
-   domain invariants remain in Core entities and factories.
