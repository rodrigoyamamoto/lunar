# Lunar Asset Studio Agent Guide

## Purpose

This file is the fast operational map for agents working in this repository.
Read it before scanning the repository. Open only the files relevant to the
current task, then expand the survey if this guide is stale or incomplete.

## Survey Metadata

- Last surveyed: 2026-08-24
- Survey baseline: `90dce8a` (`docs: add project agent guide`), based on commit
  `90dce8a`, including the pending workflow-definition slice
- Baseline validation: restore/build/test passed, 55 tests, 0 warnings
- Repository root: `D:\LunarAssetStudio\las`
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
- `InMemoryWorkflowDefinitionRepository` Infrastructure adapter;
- `InMemoryWorkflowExecutionRepository` Infrastructure adapter;
- `WorkflowExecution.Rehydrate` persistence reconstruction factory;
- strongly typed UUID v7 identifiers;
- unit tests for the implemented domain behaviour;
- Infrastructure repository tests;
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
      Persistence/         In-memory workflow definition repository
    Lunar.Api/             Composition/API boundary; currently template only
  tests/
    Lunar.Tests/
      Unit/                Core domain unit tests
      Infrastructure/      Infrastructure adapter tests
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
    └──────> Lunar.Infrastructure ───────> Lunar.Core

Lunar.Tests ─────────────> Lunar.Core
Lunar.Tests ─────────────> Lunar.Infrastructure
Lunar.Core ──────────────> no Lunar project and no external technology
```

Rules:

- Core must not reference Infrastructure, API, providers, databases, file
  systems, external SDKs, AI models, Blender, Unreal, or Unity.
- Infrastructure adapts technical systems to Core concepts.
- API is the composition and delivery boundary.
- Tests reference only projects they currently exercise. Do not add an
  Infrastructure reference to unit tests in anticipation of future tests.
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
states cannot restart. Workflow Execution does not execute the definition; it
does not yet model retries, scheduling, or an orchestration engine.

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

## Design Rules

- Prefer the simplest explicit domain model that satisfies a real requirement.
- Apply KISS, DRY, and SOLID without building internal frameworks.
- Prefer composition; avoid inheritance used only to share a line of code.
- Do not create generic interfaces without a real consumer.
- Keep provider, model, engine, path, and environment choices in configuration
  or Infrastructure—not in Core.
- Preserve history and traceability; generated artifacts are not the Asset.
- Expected invalid lifecycle transitions currently use no-op guards. Do not
  introduce exceptions or a Result abstraction without a concrete caller that
  needs failure details.
- Give one clear recommended implementation for simple decisions; avoid a menu
  of speculative alternatives.

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
