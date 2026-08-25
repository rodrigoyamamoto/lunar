# Lunar Asset Studio Agent Guide

## Purpose

This file is the fast operational map for agents working in this repository.
Read it before scanning the repository. Open only the files relevant to the
current task, then expand the survey if this guide is stale or incomplete.

## Survey Metadata

- Last surveyed: 2026-08-25
- Survey baseline: `590b66c` (`feat: add artifact persistence boundary`)
- Baseline validation: restore/build/test passed, 258 tests, 0 warnings
- Repository root: repository root; paths in this document are repository-relative
- Main branch: `master`

## Product

Lunar Asset Studio is an AI-assisted asset creation platform for turning
creative intent into production-ready game assets. Lunar owns orchestration,
asset history, and workflow traceability. AI models, vendors, Blender, game
engines, storage technologies, and worker runtimes are replaceable technical
details.

The first engine target is Unreal Engine, but the Core must remain engine
independent.

## Current Development Stage

This repository is in the domain-foundation stage.

Implemented:

- initial Asset domain model and lifecycle;
- Artifact domain model with optional workflow provenance;
- Workflow Execution lifecycle with Asset and Workflow Definition references;
- Capability domain concept (provider-independent intent);
- Workflow Definition composed of ordered Capability steps;
- Workflow Definition versioning with immutable `(WorkflowDefinitionId, Version)` identity;
- `IWorkflowDefinitionRepository` Core persistence contract;
- `IWorkflowExecutionRepository` Core persistence contract with optimistic concurrency;
- `IAssetRepository` Core persistence contract;
- `IArtifactRepository` Core persistence contract;
- `InMemoryWorkflowDefinitionRepository` Infrastructure adapter;
- `InMemoryWorkflowExecutionRepository` Infrastructure adapter;
- `InMemoryAssetRepository` Infrastructure adapter;
- `InMemoryArtifactRepository` Infrastructure adapter;
- `WorkflowExecution.Rehydrate` persistence reconstruction factory;
- `Asset.Rehydrate` persistence reconstruction factory;
- `ExecuteWorkflowService` Application layer orchestration;
- `StartWorkflowExecutionService` Application layer orchestration;
- `RecordWorkflowArtifactService` Application layer orchestration;
- strongly typed UUID v7 identifiers;
- unit tests for the implemented domain behaviour;
- Infrastructure repository tests;
- Application service tests;
- repository and project boundaries.

Still scaffolding or intentionally absent:

- API endpoints beyond the template root endpoint;
- durable persistence, database mappings, and ORM;
- provider adapters;
- worker contracts and worker implementations;
- workflow scheduling, retries, and orchestration engine;
- runtime configuration implementations;
- production frontend—the current UI is the Vite/React starter;
- integration tests.

Do not infer production readiness from the presence of placeholder folders.

## Technology Baseline

- Backend: C# on .NET 10 (`global.json` pins SDK `10.0.302`)
- Architecture: modular monolith
- Frontend: React 19, TypeScript 6, Vite 8
- Tests: xUnit with Microsoft.NET.Test.Sdk and coverlet collector
- Package management: NuGet Central Package Management
- Identifiers: UUID version 7

Node.js is required but is not currently pinned in the repository.

## Repository Map

```text
backend/
  Lunar.slnx
  src/
    Lunar.Core/            Domain concepts and business rules
      Assets/
      Artifacts/
      Primitives/
      Workflows/
      Capabilities/        Provider-independent capability concepts
      Workers/             Placeholder
    Lunar.Infrastructure/  Technical implementations
      Persistence/         In-memory asset, artifact, and workflow repositories
    Lunar.Application/     Application-layer orchestration
      Workflows/           ExecuteWorkflowService, StartWorkflowExecutionService
      Artifacts/           RecordWorkflowArtifactService
    Lunar.Api/             Composition/API boundary; currently template only
  tests/
    Lunar.Tests/
      Unit/                Core domain unit tests
      Infrastructure/      Infrastructure adapter tests
      Application/         Application service tests
      Integration/         Placeholder

frontend/                  React/Vite starter application
workers/                   Future provider, Blender, and contract runtimes
config/                    Future runtime configuration
artifacts/                 Generated outputs; contents are Git-ignored
docs/
  decisions/               Accepted architectural decisions
  architecture/            Current domain and architecture models
scripts/setup/             Historical bootstrap tooling
```

## Dependency Rules

The allowed project dependency direction is:

```text
Lunar.Api ───────────────> Lunar.Core
    └──────> Lunar.Application ───────> Lunar.Core
    └──────> Lunar.Infrastructure ───────> Lunar.Core

Lunar.Tests ─────────────> Lunar.Core
Lunar.Tests ─────────────> Lunar.Application
Lunar.Tests ─────────────> Lunar.Infrastructure
Lunar.Core ──────────────> no Lunar project and no external technology
```

Rules:

- Core must not reference Infrastructure, API, Application, providers,
  databases, file systems, external SDKs, AI models, Blender, Unreal, or
  Unity.
- Application coordinates use cases by depending on Core abstractions. It
  must not reference Infrastructure directly; the API composes Application
  services with Infrastructure adapters.
- Infrastructure adapts technical systems to Core concepts.
- API is the composition and delivery boundary.
- Tests reference only projects they currently exercise. Do not add a
  reference in anticipation of future tests.
- Avoid cycles and speculative layers.

## Source of Architectural Truth

Read these before changing the related area:

- `docs/decisions/ADR-001-foundation.md` — technology and foundation boundaries
- `docs/decisions/ADR-002-domain-modeling-principles.md` — domain design rules
- `docs/decisions/ADR-003-package-management.md` — central NuGet versions
- `docs/decisions/ADR-004-workflow-definition-versioning.md` — workflow definition version identity
- `docs/architecture/lunar-domain-boundaries.md` — ownership and dependency map
- `docs/architecture/asset-lifecycle-model.md` — Asset and Artifact semantics
- `docs/architecture/workflow-execution-model.md` — execution lifecycle
- `docs/architecture/workflow-definition-model.md` — workflow definition and capability model
- `docs/architecture/application-error-handling.md` — Application Result pattern and error classification

Accepted ADRs describe binding decisions. Architecture documents describe the
current model. Code and tests implement that model. If they disagree, do not
silently choose one: identify the drift and update documentation, code, and
tests together in the same coherent change.

## Current Domain Model

### Identifiers

`AssetId`, `ArtifactId`, `WorkflowExecutionId`, `CapabilityId`, and
`WorkflowDefinitionId` are distinct readonly record structs. They must remain
type-safe; do not replace them with raw `Guid` or a generic `Id<T>` without a
new accepted decision.

`Lunar.Core.Primitives.IdGenerator` is the single UUID v7 generation rule. It
is static, stateless, and safe for concurrent calls. Do not introduce an ID
inheritance hierarchy merely to share generation code.

### Asset

An Asset is the creative entity, not a file. It contains identity, name, type,
status, and creation time.

The `Name` is a required human-readable name describing the creative entity. It
is domain identity, not a file name, storage key, provider identifier, model
identifier, or engine asset path. It cannot be null, empty, or whitespace-only.
A supplied valid name is preserved exactly; it is not trimmed, re-cased, or
normalised. There is no rename operation in the current model. The `AssetId`
cannot be empty; construction with an empty identifier is rejected.

Allowed status transitions:

```text
Draft ──> Processing ──> Completed
                    └──> Failed

Completed ──> Processing
Failed ─────> Processing
```

Completion and failure are valid only from Processing. Invalid transition
requests currently leave the state unchanged; they do not throw.

`Asset.Rehydrate` is a narrowly scoped reconstruction factory for
persistence. It accepts all persisted fields including `Status` and
`CreatedAt` and validates structural invariants. It does not act as an
unrestricted backdoor to invalid Asset states.

Core owns `IAssetRepository`, a persistence contract keyed by `AssetId`.
`TryAddAsync` inserts an Asset if absent (returns `false` if the exact
identity already exists; never overwrites). `GetAsync` retrieves by ID or
returns `null`. Invalid identity arguments are rejected with
`ArgumentException`. Infrastructure provides
`InMemoryAssetRepository` as a development/test adapter with snapshot
isolation.

### Artifact

An Artifact is a concrete output belonging to an Asset. It contains:

- `ArtifactId`;
- owning `AssetId`;
- name and `ArtifactType`;
- optional `WorkflowExecutionId? SourceExecutionId`;
- `IReadOnlyList<ArtifactId> SourceArtifactIds` — direct artifact-to-artifact
  lineage;
- creation time.

`SourceExecutionId` is optional because imported or user-provided artifacts
may not originate from a Lunar workflow. Do not add an in-memory Artifact list
to WorkflowExecution until aggregate and persistence requirements justify it.

`SourceArtifactIds` records direct artifact derivation. It is independent of
`SourceExecutionId`: an Artifact may have either, both, or neither.
Invariants: the collection cannot be null (use empty for no lineage); every
source identifier must be non-empty; duplicates are rejected; direct
self-reference is rejected; source order is preserved exactly; the stored
collection is immutable and cannot be mutated through the exposed property or
the original caller-supplied collection. Cross-Asset lineage is permitted.
Transitive lineage is not expanded — only direct sources are recorded.

Artifact is fully immutable: all properties are get-only, there are no
mutation methods, and `SourceArtifactIds` is backed by a
`ReadOnlyCollection<ArtifactId>`. Because of this, `Artifact.Rehydrate` is
not required — the in-memory repository stores and returns the same
immutable instance without reconstruction. This differs from `Asset` and
`WorkflowExecution`, which have mutable lifecycle state and require
`Rehydrate` for snapshot isolation.

Core owns `IArtifactRepository`, a persistence contract keyed by
`ArtifactId`. `TryAddAsync` inserts an Artifact if absent (returns `false`
if the exact identity already exists; never overwrites). `GetAsync`
retrieves by ID or returns `null`. Invalid identity arguments are rejected
with `ArgumentException`. Infrastructure provides
`InMemoryArtifactRepository` as a development/test adapter. The repository
persists Artifact domain objects only — it does not store physical files,
blobs, or binary data. No durable persistence, ORM, or database is present.

### Capability

A Capability represents something Lunar can do independently of providers,
models, or implementations. It contains a `CapabilityId` and a `Name`. It does
not carry provider, model, endpoint, or configuration information.

### Workflow Definition

A Workflow Definition is a reusable ordered process composed of Workflow
Steps. Each step references a `CapabilityId` and has a one-based position.
`WorkflowDefinitionId` is the stable logical identity across versions; the
exact immutable version is identified by `(WorkflowDefinitionId, Version)`.

Invariants:

- `WorkflowDefinitionId` cannot be empty;
- `Version` must be a positive integer (`>= 1`);
- at least one step is required;
- step positions must be unique;
- positions must form a contiguous sequence beginning at 1;
- steps must be supplied in the same physical order as their positions;
- Workflow Definition preserves that declared order and does not sort or
  normalize the collection;
- the returned step collection is read-only.

Definitions are immutable. Changing contents creates a new immutable version
with the same `WorkflowDefinitionId` and a new positive `Version`. There is no
mutation method, `CreateNextVersion`, or version-sequence allocation in Core.
Version numbers are scoped to a `WorkflowDefinitionId` and are not globally
unique. See ADR-004 for the versioning decision.

Workflow Definitions do not contain provider implementations, parameters,
retry policies, status, or execution behaviour.

Core owns `IWorkflowDefinitionRepository`, a persistence contract keyed by
`(WorkflowDefinitionId, Version)`. `TryAddAsync` inserts an exact immutable
definition if absent (returns `false` if the exact identity already exists;
never overwrites). `GetAsync` retrieves an exact version or returns `null`.
Invalid identity arguments are rejected with `ArgumentException`.
Infrastructure provides `InMemoryWorkflowDefinitionRepository` as a
development/test adapter. No durable persistence, ORM, or database is
present.

### Workflow Execution

A Workflow Execution is one attempt to run a generation process. Every
execution references:

- `AssetId AssetId` — the Asset being processed;
- `WorkflowDefinitionId WorkflowDefinitionId` — the logical definition being
  executed;
- `int WorkflowDefinitionVersion` — the exact immutable definition version;
- `long Revision` — persistence-state revision for optimistic concurrency,
  distinct from `WorkflowDefinitionVersion`.

An execution cannot be created without all three reference values. The version
must be a positive integer (`>= 1`). An execution continues to refer to the
exact historical definition version even after later versions are introduced;
there is no latest/current/active version resolution in Core. `Revision`
starts at `0` on creation; lifecycle methods do not change it; only
repository persistence determines the next stored revision.

```text
Created ──> Running ──> Completed
                   ├──> Failed
                   └──> Cancelled
```

Invalid transition requests currently leave the state unchanged. Terminal
states cannot restart. Lifecycle transitions are owned by Core: the
`WorkflowExecution` entity exposes `Start`, `Complete`, `Fail`, and `Cancel`
methods that enforce valid state changes and timestamp updates. Application
may request transitions but cannot directly set `Status`, `StartedAt`, or
`CompletedAt`. Workflow Execution does not execute the definition; it does
not yet model retries, scheduling, or an orchestration engine.

`WorkflowExecution.Rehydrate` is a narrowly scoped reconstruction factory for
persistence. It accepts all persisted fields including `Revision` and
validates structural invariants and status/timestamp coherence. It does not
act as an unrestricted backdoor to invalid lifecycle states.

Core owns `IWorkflowExecutionRepository`, a persistence contract keyed by
`WorkflowExecutionId`. `TryAddAsync` accepts only executions with
`Revision = 0` and rejects duplicate IDs. `GetAsync` retrieves by ID or
returns `null`. `TryUpdateAsync` uses optimistic concurrency:
`expectedRevision` must match the stored `Revision`; a successful
state-changing update increments `Revision` by one; a stale update returns
`null`; a no-op returns current state without incrementing. Immutable fields
cannot be changed through update. Invalid lifecycle transitions are rejected.
Infrastructure provides `InMemoryWorkflowExecutionRepository` as a
development/test adapter with snapshot isolation — stored and returned
objects are isolated copies, so mutating a caller's object does not affect
repository state.

### Application Layer

The Application layer coordinates use cases. It depends on Core
abstractions (domain objects and persistence contracts) but does not
reference Infrastructure directly. The API composes Application services
with Infrastructure adapters at runtime.

`ExecuteWorkflowService` is the first Application service. It coordinates:

- resolving the referenced Asset through `IAssetRepository` (returns
  `AssetNotFound` if missing);
- retrieving the exact Workflow Definition version through
  `IWorkflowDefinitionRepository` (returns `WorkflowDefinitionNotFound` if
  missing);
- creating a `WorkflowExecution` through the domain `Create` factory;
- persisting the new execution through `IWorkflowExecutionRepository`
  (returns `WorkflowExecutionPersistenceFailed` if rejected);
- returning an explicit `Result<WorkflowExecution>` outcome.

`StartWorkflowExecutionService` is the second Application service. It
coordinates starting an existing `WorkflowExecution`:

- loading the execution through `IWorkflowExecutionRepository.GetAsync`
  (returns `WorkflowExecutionNotFound` if missing);
- checking that the caller-supplied `expectedRevision` matches the loaded
  revision (returns `WorkflowExecutionConcurrencyConflict` if stale);
- requesting the domain transition through `WorkflowExecution.Start()`
  (returns `WorkflowExecutionCannotStart` if the domain no-ops);
- persisting the transition through
  `IWorkflowExecutionRepository.TryUpdateAsync` with optimistic
  concurrency (returns `WorkflowExecutionConcurrencyConflict` if the
  update is rejected due to a race);
- returning the persisted `WorkflowExecution` with the incremented
  `Revision` on success.

The service does not own domain invariants, lifecycle transitions, or
persistence mechanics. It does not duplicate domain logic — input
validation is delegated to the domain `Create` factory and repository
contracts. It does not introduce CQRS — `Command`, `Query`, and `Handler`
suffixes remain reserved for possible future adoption.

`RecordWorkflowArtifactService` is the third Application service. It
records an already-created `Artifact` as an output of an existing running
`WorkflowExecution`:

- loading the execution through `IWorkflowExecutionRepository.GetAsync`
  (returns `WorkflowExecutionNotFound` if missing);
- checking that the execution status is `Running` (returns
  `WorkflowExecutionNotRunning` otherwise);
- requiring that the Artifact carries workflow provenance through
  `Artifact.SourceExecutionId` (returns
  `ArtifactWorkflowProvenanceMissing` if absent);
- checking that `Artifact.SourceExecutionId` exactly equals the
  requested `WorkflowExecutionId` (returns
  `ArtifactWorkflowExecutionMismatch` if it differs);
- checking that `Artifact.AssetId` exactly equals
  `WorkflowExecution.AssetId` (returns `ArtifactWorkflowAssetMismatch`
  if it differs);
- persisting the Artifact through `IArtifactRepository.TryAddAsync`
  (returns `ArtifactPersistenceFailed` if the insert is rejected, e.g.
  duplicate identity);
- returning the recorded `Artifact` on success.

The service does not generate the Artifact, invoke providers/models, or
dispatch capabilities. It does not mutate `WorkflowExecution` or
automatically complete it. It does not resolve or traverse
`SourceArtifactIds`; cross-Asset direct lineage remains permitted because
only Artifact ownership relative to the execution is checked. Artifact
persistence remains insert-only; the service does not update, replace, or
delete Artifacts.

Application services use a Result pattern for expected use-case outcomes.
`Result<T>` is owned by `Lunar.Application` and does not leak into Core.
`Result<T>.Success(value)` represents a successful outcome;
`Result<T>.Failure(error)` represents an expected use-case failure.
Application errors (`AssetNotFound`, `WorkflowDefinitionNotFound`,
`WorkflowExecutionPersistenceFailed`, `WorkflowExecutionNotFound`,
`WorkflowExecutionConcurrencyConflict`, `WorkflowExecutionCannotStart`,
`WorkflowExecutionNotRunning`, `ArtifactWorkflowProvenanceMissing`,
`ArtifactWorkflowExecutionMismatch`, `ArtifactWorkflowAssetMismatch`,
`ArtifactPersistenceFailed`) are sealed records inheriting from
`ApplicationError`. They represent failed use-case execution, not domain
exceptions or programmer errors.

Exception policy:

- Invalid caller/programmer usage (null dependencies, null Artifact,
  invalid domain construction, negative expected revision) remains
  exception-based.
- Expected use-case outcomes (asset not found, definition not found,
  persistence rejected, execution not found, concurrency conflict,
  cannot start, execution not running, artifact provenance missing,
  artifact provenance mismatch, artifact asset mismatch, artifact
  persistence rejected) are returned as `Result` failures, not thrown.

## Design Rules

- Prefer the simplest explicit domain model that satisfies a real requirement.
- Apply KISS, DRY, and SOLID without building internal frameworks.
- Prefer composition; avoid inheritance used only to share a line of code.
- Do not create generic interfaces without a real consumer.
- Keep provider, model, engine, path, and environment choices in configuration
  or Infrastructure—not in Core.
- Preserve history and traceability; generated artifacts are not the Asset.
- Expected invalid lifecycle transitions currently use no-op guards. Do not
  introduce exceptions or a Result abstraction in Core without a concrete
  caller that needs failure details. The Application layer owns its own
  `Result<T>` for use-case outcomes; Core does not depend on it.
- Give one clear recommended implementation for simple decisions; avoid a menu
  of speculative alternatives.

## Naming Conventions

Names must communicate architectural responsibility. Avoid generic names
that create ambiguity between different architectural concepts.

### Commands

The suffix `Command` is reserved exclusively for CQRS command objects.

Examples:

```text
CreateAssetCommand
StartWorkflowExecutionCommand
GenerateArtifactCommand
```

A Command represents an intention or request to change system state. Do
not use `Command` as a generic suffix for services, operations, workflow
actions, or arbitrary request models. Avoid names such as
`AssetCommandService` or `WorkflowCommandProcessor` unless they are
explicitly justified as part of a CQRS design.

### Queries

The suffix `Query` is reserved exclusively for CQRS query objects.

Examples:

```text
GetAssetQuery
GetWorkflowExecutionHistoryQuery
SearchGeneratedAssetsQuery
```

A Query represents a request for information retrieval. Do not use
`Query` as a generic name for repositories, database queries, helper
classes, or infrastructure data access objects.

### Handlers

The suffix `Handler` is reserved exclusively for CQRS command or query
handlers.

Examples:

```text
CreateAssetCommandHandler
GetAssetQueryHandler
```

A Handler receives a specific `Command` or `Query` and produces a result
or state change. Do not use `Handler` as a generic suffix for services,
processors, workflow steps, event listeners, or arbitrary logic
containers. Avoid names such as `GenerationHandler` or
`WorkflowHandler` unless they are explicitly justified as part of a
CQRS design.

### Services

The suffix `Service` is allowed only when the architectural
responsibility is clear. A Service must belong to a specific
architectural layer.

**Application Services** orchestrate use cases, coordinate domain
objects, depend on domain abstractions, should not contain domain
invariants, and should not become "god classes". Examples:
`GenerateAssetService`, `ExecuteWorkflowService`.

**Domain Services** live inside the Core/domain layer, contain domain
logic that does not naturally belong to a single aggregate, and require
explicit justification. Avoid creating Domain Services simply because an
entity has many methods. Prefer keeping behavior inside aggregates when
ownership is clear.

**Infrastructure Services** handle technical concerns, integrate with
external systems, and implement infrastructure-specific behavior. They
must not leak infrastructure concerns into Core.

### Repositories

The suffix `Repository` is reserved for persistence boundaries that
implement a Core-owned persistence contract.

Naming pattern:

```text
I{Aggregate}Repository          — Core interface
InMemory{Aggregate}Repository   — Infrastructure in-memory adapter
```

Examples:

```text
IWorkflowDefinitionRepository
InMemoryWorkflowDefinitionRepository

IWorkflowExecutionRepository
InMemoryWorkflowExecutionRepository
```

A Repository stores and retrieves domain aggregates. It must not
contain domain logic, enforce domain invariants, or become a generic
data access object. Each Repository is specific to one aggregate and
reflects that aggregate's persistence semantics (e.g. insert-only for
immutable versions, optimistic concurrency for mutable aggregates). Do
not create a generic `IRepository<T>` or `RepositoryBase<T>`; design
each repository from its actual requirements.

### Events

The suffix `Event` is reserved for domain or integration event objects
that represent something that has already happened.

Examples:

```text
WorkflowExecutionStartedEvent
ArtifactGeneratedEvent
AssetCompletedEvent
```

An Event is a fact about a past state change. It is not a command, a
request, or an instruction. Name events in past tense to reflect that
the change has occurred. Do not use `Event` as a generic suffix for
messages, notifications, callbacks, or arbitrary data containers. Do
not introduce a domain event bus or event dispatcher without a concrete
requirement; the suffix is reserved so that future event-driven design
does not conflict with existing names.

### Avoid Generic Naming

The following names should be avoided unless there is strong
justification:

```text
Manager
Helper
Utility
Processor
Coordinator
Engine
Controller
```

These names frequently hide unclear responsibilities. Prefer names that
communicate intent.

Avoid: `AssetManager`, `WorkflowProcessor`, `GenerationCoordinator`.

Prefer: `AssetRepository`, `GenerateAssetService`,
`WorkflowExecutionRepository` — when those responsibilities are actually
correct.

### Architectural Intent

The current direction is an Application Service orchestrating domain
objects directly:

```text
Application Layer

GenerateAssetService
        |
        v

Domain Model
```

If CQRS is adopted in the future, `Command` and `Query` objects (and
their corresponding `Handler` implementations) would sit between the
Application Service and the Domain Model. CQRS is not currently adopted;
the `Command`, `Query`, and `Handler` suffixes are reserved so that
future adoption does not conflict with existing names.

Names should reveal ownership, responsibility, architectural layer, and
whether the object represents data, orchestration, persistence, or
domain behavior.

## Package Management

`Directory.Packages.props` at the repository root is the only source of NuGet
package versions.

- Add or update versions with `PackageVersion` there.
- Use `PackageReference` without `Version` in project files.
- A clean restore must not produce `NU1008`.
- Add dependencies only when they solve a current requirement.

## Testing Rules

- Protect business rules, lifecycle transitions, invariants, and important
  integration boundaries.
- Do not target arbitrary coverage percentages.
- Keep unit tests grouped by domain under `backend/tests/Lunar.Tests/Unit/`.
- Test documented rules such as UUID v7, not only non-empty values.
- Cover valid and invalid lifecycle transitions.
- Do not write tests solely for trivial getters.

## Validation Commands

Run from the repository root.

Backend:

```powershell
dotnet test ./backend/Lunar.slnx
dotnet format ./backend/Lunar.slnx --verify-no-changes --no-restore
```

Frontend when it is in scope:

```powershell
npm --prefix ./frontend ci
npm --prefix ./frontend run lint
npm --prefix ./frontend run build
```

Use a clean restore/build/test after package or project-reference changes. Do
not treat `dotnet test --no-build` as proof of the current source unless the
current commit was built first.

## Mandatory Technical Review Protocol

Every implementation slice must pass this review before it is approved for a
commit. The implementer report is context only; reviewers and agents must
verify the actual repository state and must not rely on reported file lists,
test counts, or claims of compliance.

### 1. Establish the complete change set

Run:

```powershell
git status --short --branch
git diff --stat
git diff --cached --stat
git diff HEAD --name-status
git diff HEAD --stat
git diff HEAD --check
```

Review both the working tree and the index. `git diff` alone omits staged
changes; `git diff --cached` alone omits unstaged changes. Use `git diff HEAD`
to inspect the complete slice.

Before approval:

- there must be no unexpected, unrelated, generated, deleted, or empty files;
- existing tests must not disappear unless their removal is an explicit and
  justified part of the slice;
- partially staged slices are not review-ready;
- unless the user explicitly requests staging, leave all slice changes
  unstaged so one complete working-tree diff can be reviewed;
- never discard working-tree changes to normalize staging.

### 2. Read the actual implementation

Open every added or materially changed source, test, project, configuration,
and architecture file. Confirm that the implementation—not only its names or
report—matches the requested behaviour.

Trace important rules through:

```text
architecture decision or model
        ↓
domain implementation
        ↓
tests
        ↓
public documentation and AGENTS.md
```

Look specifically for:

- invariants that can be bypassed through alternate constructors, default
  values, mutable references, or collection ordering;
- tests that pass before reaching the class they claim to exercise;
- tests that assert only a happy path while documented invalid paths remain
  unprotected;
- stale binaries or `--no-build` results being presented as current evidence;
- mutable collection exposure or caller-owned collections retained directly;
- silent sorting, normalization, or data loss that changes caller intent;
- accidental public API changes outside the slice.

### 3. Validate architecture and dependencies

Inspect project references and package references after every structural or
dependency change:

```powershell
rg -n "ProjectReference|PackageReference" ./backend -g "*.csproj"
```

Confirm:

- Core has no project reference and no external-technology dependency;
- Infrastructure depends only inward on Core;
- API is the composition/delivery boundary;
- tests reference only projects they currently exercise;
- package versions exist only in `Directory.Packages.props`;
- no provider, model, engine, database, file-system, endpoint, or environment
  choice leaked into Core;
- no speculative interface, inheritance hierarchy, generic framework, or new
  package was added without a current consumer and requirement.

### 4. Validate domain behaviour and exception policy

For each changed domain type, enumerate its valid states, invalid inputs,
allowed transitions, terminal states, relationships, and collection rules.
Compare them with the implementation and tests.

The current exception boundary is:

```text
Invalid constructor or factory precondition
    → ArgumentException or ArgumentNullException is allowed

Expected invalid lifecycle transition
    → no-op guard without exception
```

Do not reject construction exceptions merely because lifecycle transitions use
no-op guards. Do not introduce Result types, custom exception hierarchies, or
validation frameworks without a concrete caller that requires them.

### 5. Validate test integrity

Inspect the test diff independently from the production-code diff:

```powershell
git diff HEAD -- ./backend/tests
rg -n "\[Fact\]|\[Theory\]" ./backend/tests/Lunar.Tests
```

Confirm:

- pre-existing relevant coverage remains present and is adapted to new APIs;
- each new business invariant has a positive and/or negative test appropriate
  to the rule;
- lifecycle tests cover valid and invalid transitions;
- tests instantiate and exercise the intended class before asserting its
  result or exception;
- test names describe the behaviour actually reached;
- test counts do not decrease without an explicit, reviewed explanation.

A higher or passing test count is not sufficient evidence: a test file may
have been deleted while new theory cases hide the coverage loss.

### 6. Validate documentation coherence

When a slice changes domain concepts, public construction contracts,
lifecycles, relationships, dependencies, tooling, or validation commands,
verify and update in the same slice:

- the relevant file under `docs/architecture/`;
- any affected accepted ADR or a new/superseding ADR when the decision changes;
- this `AGENTS.md` project map and survey metadata.

Documentation must describe implemented behaviour and explicitly distinguish
current scope from deferred concepts. Do not mark a model or validation state
as accepted/current when the working tree does not support that claim.

### 7. Execute current-source validation

Run the complete backend validation from the repository root:

```powershell
dotnet test ./backend/Lunar.slnx
dotnet format ./backend/Lunar.slnx --verify-no-changes --no-restore
git diff HEAD --check
git status --short
```

When frontend files or frontend contracts are affected, also run:

```powershell
npm --prefix ./frontend ci
npm --prefix ./frontend run lint
npm --prefix ./frontend run build
```

Approval requires:

- restore and compilation success;
- zero compiler warnings unless a pre-existing warning is documented;
- all tests passing from freshly compiled current source;
- formatting verification success;
- no whitespace errors;
- no unexpected staged entries or unrelated changes.

### 8. Produce an evidence-based verdict

The final review must lead with one of:

- `APPROVED FOR COMMIT`;
- `APPROVED WITH NON-BLOCKING NOTES`;
- `NOT APPROVED`.

List findings in severity order and cite exact files and lines. Separate:

- confirmed defects;
- architectural or design drift;
- missing tests or evidence;
- documentation drift;
- operational Git-state problems;
- optional future improvements.

Do not report “no deviations” until the complete `HEAD` diff, index, working
tree, current-source tests, formatting, and documentation have all been
inspected. A passing build does not override a domain, coverage, documentation,
or staging defect.

## Historical Bootstrap Warning

`scripts/setup/01-bootstrap-lunar.ps1` documents the original repository
bootstrap but is not idempotent against the current layout and dependencies.
In particular, it assumes a different solution location/name and still adds an
Infrastructure reference to the test project.

Do not rerun it on the existing repository. If bootstrap automation becomes a
current requirement, update and test the script in a dedicated change first.

## Change and Documentation Discipline

- Keep documentation of a model, its implementation, and its tests in the same
  commit when they form one unit of change.
- Use a separate ADR commit when a decision intentionally precedes
  implementation.
- Preserve unrelated working-tree changes.
- Do not commit generated assets, `bin/`, `obj/`, `node_modules/`, or `dist/`.
- Avoid adding placeholder abstractions or folders beyond an accepted design.

## Maintaining This Guide

Update this file as part of the same change whenever any of these occur:

- a project, top-level area, or dependency direction is added or removed;
- an ADR is accepted, superseded, or changes a binding rule;
- a public domain concept, lifecycle, relationship, or invariant changes;
- validation commands, SDKs, package-management rules, or tooling change;
- a placeholder area becomes implemented;
- this guide contains a statement contradicted by the current repository.

Periodic refresh rule:

- perform a lightweight survey when this guide is more than 30 days old during
  a substantial task, or when 10 or more commits have landed since the survey
  baseline;
- compare `git log <baseline>..HEAD`, changed paths, project references, ADRs,
  and validation commands before doing a full repository scan;
- after the refresh, update the survey date, baseline commit, current stage,
  test baseline, and any affected sections;
- do not rewrite this file for cosmetic-only changes.
