# Lunar Asset Studio Agent Guide

## Purpose

This file is the fast operational map for agents working in this repository.
Read it before scanning the repository. Open only the files relevant to the
current task, then expand the survey if this guide is stale or incomplete.

## Survey Metadata

- Last surveyed: 2026-08-24
- Survey baseline: `c0ef032` (`fix: align domain model and restore build integrity`)
- Baseline validation: restore/build/test passed, 26 tests, 0 warnings
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
- Workflow Execution lifecycle;
- strongly typed UUID v7 identifiers;
- unit tests for the implemented domain behaviour;
- repository and project boundaries.

Still scaffolding or intentionally absent:

- API endpoints beyond the template root endpoint;
- persistence and database mappings;
- provider adapters;
- worker contracts and worker implementations;
- workflow definitions, steps, scheduling, retries, and orchestration engine;
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
      Capabilities/        Placeholder
      Workers/             Placeholder
    Lunar.Infrastructure/  Technical implementations; currently placeholders
    Lunar.Api/             Composition/API boundary; currently template only
  tests/
    Lunar.Tests/
      Unit/                Current test suite
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
- `docs/architecture/lunar-domain-boundaries.md` — ownership and dependency map
- `docs/architecture/asset-lifecycle-model.md` — Asset and Artifact semantics
- `docs/architecture/workflow-execution-model.md` — execution lifecycle

Accepted ADRs describe binding decisions. Architecture documents describe the
current model. Code and tests implement that model. If they disagree, do not
silently choose one: identify the drift and update documentation, code, and
tests together in the same coherent change.

## Current Domain Model

### Identifiers

`AssetId`, `ArtifactId`, and `WorkflowExecutionId` are distinct readonly record
structs. They must remain type-safe; do not replace them with raw `Guid` or a
generic `Id<T>` without a new accepted decision.

`Lunar.Core.Primitives.IdGenerator` is the single UUID v7 generation rule. It
is static, stateless, and safe for concurrent calls. Do not introduce an ID
inheritance hierarchy merely to share generation code.

### Asset

An Asset is the creative entity, not a file. It contains identity, name, type,
status, and creation time.

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
- creation time.

`SourceExecutionId` is optional because imported or user-provided artifacts
may not originate from a Lunar workflow. Do not add an in-memory Artifact list
to WorkflowExecution until aggregate and persistence requirements justify it.

### Workflow Execution

A Workflow Execution is one attempt to run a generation process.

```text
Created ──> Running ──> Completed
                   ├──> Failed
                   └──> Cancelled
```

Invalid transition requests currently leave the state unchanged. Terminal
states cannot restart. Workflow execution does not yet model steps, providers,
retries, scheduling, or workflow definitions.

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
