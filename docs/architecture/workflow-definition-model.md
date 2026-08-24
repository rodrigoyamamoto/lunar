# Workflow Definition Model

## Status

Accepted

## Purpose

This document defines the Workflow Definition model introduced to Lunar Asset
Studio. It describes provider-independent Workflow Definitions composed of
ordered Capability steps, and explains how definitions relate to Assets,
Workflow Executions, Artifacts, and Capabilities.

The goal is to represent **what** should be executed and improve traceability,
without introducing a workflow execution engine.

## Context

Lunar Asset Studio orchestrates multiple specialized capabilities to transform
creative intent into production-ready game assets. Before this slice, the
domain model contained a Workflow Execution that tracked lifecycle status but
did not reference the Asset being processed or the definition being executed.

This slice introduces:

- **Capability** — a provider-independent description of something Lunar can do.
- **Workflow Definition** — a reusable ordered sequence of Capability steps.
- **Workflow Step** — one ordered capability invocation within a definition.

It also updates **Workflow Execution** to identify both the Asset being
processed and the Workflow Definition being executed.

## Capability

A Capability represents something Lunar can do independently of providers,
models, executables, endpoints, or implementations.

Examples:

- image generation
- mesh generation
- validation
- rigging
- export

A Capability contains:

- `CapabilityId Id`
- `string Name`

A Capability does **not** contain:

- provider information
- model selection
- executable paths
- endpoint URLs
- configuration values
- implementation details

The implementation behind a Capability can change without altering the domain
concept. Capabilities are abstractions; concrete providers and workers are
Infrastructure concerns.

## Workflow Definition

A Workflow Definition represents a reusable ordered process.

It contains:

- `WorkflowDefinitionId Id`
- `string Name`
- `IReadOnlyList<WorkflowStep> Steps`
- `DateTimeOffset CreatedAt`

Invariants:

- the identifier cannot be empty;
- the name cannot be null, empty, or whitespace;
- at least one step is required;
- step positions must be unique;
- step positions must form a contiguous sequence beginning at 1;
- the declared step order is preserved;
- callers cannot mutate the internal step collection through a returned list
  reference.

A Workflow Definition does **not** contain:

- provider implementations or model selection;
- step parameters, retry policies, or execution behaviour;
- status, runtime lifecycle timestamps such as `StartedAt` or `CompletedAt`, or
  other runtime information;
- versioning (deferred).

## Workflow Step

A Workflow Step is a value representing one ordered capability invocation
within a Workflow Definition.

It contains:

- `int Position`
- `CapabilityId CapabilityId`

Rules:

- positions are one-based;
- every position must be positive;
- a capability identifier cannot be empty.

A Workflow Step does **not** contain:

- provider, parameters, retry policy, status, timestamps, or execution
  behaviour.

## Relationship Between Domain Entities

The intended traceability chain after this slice is:

```text
Asset
  ↑
WorkflowExecution ──> WorkflowDefinition ──> ordered WorkflowSteps
  ↑                                            │
Artifact                                  Capability
```

- An **Asset** is the creative entity being produced.
- A **Workflow Execution** is one attempt to run a generation process. Every
  execution references:
  - the `AssetId` being processed;
  - the `WorkflowDefinitionId` being executed.
- A **Workflow Definition** is a reusable ordered sequence of Workflow Steps,
  each referencing a Capability.
- An **Artifact** is a concrete output belonging to an Asset. An Artifact
  stores an optional `SourceExecutionId` identifying the Workflow Execution
  that produced it. The identifier is optional because imported or
  user-provided artifacts may not originate from a Lunar workflow execution.
- A **Capability** is a provider-independent description of something Lunar can
  do.

These entities are connected through **typed identifiers**, not object
navigation properties. This keeps the domain model simple and avoids
premature aggregate boundaries.

## Why Definitions Do Not Contain Provider Implementations

Capabilities and Workflow Definitions describe **domain intent**, not
implementation. A Workflow Definition says "generate an image, then generate a
mesh, then rig, then export" without specifying which AI model, vendor, or
worker performs each step.

This follows ADR-002 Domain Modeling Principles:

- model the problem, not the implementation;
- keep provider, model, engine, path, and environment choices in configuration
  or Infrastructure—not in Core;
- preserve replaceability without changing core business concepts.

Provider adapters, model selection, and worker contracts are Infrastructure
concerns that adapt external systems to Core capabilities.

## Explicitly Deferred Concepts

The following are intentionally out of scope for this slice:

- workflow execution engine;
- scheduler or queue;
- workflow templates separate from definitions;
- workflow definition versioning;
- providers or provider interfaces;
- model selection;
- step parameters;
- retries or checkpoints;
- persistence or ORM mappings;
- API endpoints;
- domain events;
- dependency injection;
- generic graph abstractions;
- inheritance hierarchies.

These decisions should emerge only when the product requires them.

## Principles

- Keep domain models simple.
- Avoid premature abstraction.
- Avoid coupling with external providers.
- Prefer explicit models over generic frameworks.
- Preserve history and traceability through typed identifiers.
