# Lunar Asset Studio - Asset Lifecycle Model

## Purpose

This document defines the initial lifecycle model for assets inside
Lunar Asset Studio.

The goal is to establish how Lunar represents the evolution of a
creative idea into a production-ready game asset.

This is not a final implementation design. It defines the domain
language and boundaries that will guide future development.

------------------------------------------------------------------------

# Asset Concept

An Asset represents the creative entity being produced.

An Asset is not a file.

Examples:

-   Character
-   Weapon
-   Environment prop
-   Creature
-   Material package

The Asset represents the identity and lifecycle of the thing being
created.

An Asset contains:

-   `AssetId Id` — the strongly typed identifier.
-   `string Name` — a required human-readable name describing the
    creative entity.
-   `AssetType Type` — the kind of asset.
-   `AssetStatus Status` — the lifecycle status.
-   `DateTimeOffset CreatedAt` — the creation time.

The `Name` is descriptive domain identity. It is not:

-   a file name;
-   an artifact name;
-   a storage key;
-   a provider identifier;
-   a model identifier;
-   an Unreal asset path;
-   a project path.

Creation of an Asset requires a valid `AssetId` and a valid `Name`. The
`AssetId` cannot be empty. The `Name` cannot be null, empty, or
whitespace-only. A supplied valid name is preserved exactly; it is not
trimmed, re-cased, or normalised.

Example:

    Asset:
    Corrupted Knight

The Asset may produce multiple artifacts throughout its lifecycle.

## Initial Asset Status

The initial status flow is:

    Draft
      |
      v
    Processing
      |
      +-- Completed
      |
      +-- Failed

A completed or failed Asset may return to Processing when another generation
attempt begins. Completion and failure are valid only while the Asset is being
processed. Invalid transition requests do not change the current status.

------------------------------------------------------------------------

# Artifact Concept

An Artifact represents a concrete output generated during the asset
lifecycle.

Examples:

    Corrupted Knight Asset

    Artifacts:

    - concept-image.png
    - character-sheet.png
    - base-mesh.glb
    - rigged-character.fbx
    - unreal-package

Artifacts are outputs, not the source of truth.

The Asset remains the main entity tracked by Lunar.

An Artifact contains:

-   `ArtifactId Id` — the strongly typed identifier.
-   `AssetId AssetId` — the owning Asset.
-   `string Name` — a required human-readable name.
-   `ArtifactType Type` — the kind of artifact.
-   `WorkflowExecutionId? SourceExecutionId` — the optional Lunar
    workflow execution that produced this Artifact.
-   `IReadOnlyList<ArtifactId> SourceArtifactIds` — the direct
    artifact-to-artifact lineage.
-   `DateTimeOffset CreatedAt` — the creation time.

## Artifact Provenance and Lineage

An Artifact records two independent dimensions of provenance:

-   `SourceExecutionId` records **which Lunar workflow execution**
    produced the Artifact, when such an execution exists. It is optional
    because imported or user-provided artifacts may not originate from a
    Lunar workflow execution.
-   `SourceArtifactIds` records **which earlier Artifacts** this
    Artifact was directly derived from. It is a read-only collection of
    `ArtifactId` values.

These two dimensions are deliberately independent. An Artifact may have
a `SourceExecutionId` without any `SourceArtifactIds`, or
`SourceArtifactIds` without a `SourceExecutionId`, or both, or neither.

## Lineage Semantics

`SourceArtifactIds` records only **direct** provenance. For a chain:

    A → B → C

`B.SourceArtifactIds` contains `A`, and `C.SourceArtifactIds` contains
`B`. `C` does not automatically include `A`. The transitive chain can be
reconstructed by following lineage, but the Artifact does not expand it
transitively.

Lineage invariants:

-   zero sources are valid (an imported or freshly generated artifact);
-   multiple sources are valid (an artifact derived from several
    references);
-   source order is preserved exactly as supplied;
-   duplicate direct source identifiers are invalid;
-   empty source identifiers are invalid;
-   direct self-reference is invalid (an Artifact cannot list its own
    `Id` as a source);
-   the source collection cannot be null — an artifact with no known
    lineage uses an empty collection;
-   callers cannot mutate the stored lineage through the exposed
    collection or through the original supplied collection.

Cross-Asset lineage is permitted. An Artifact may list sources belonging
to a different Asset, because future creative workflows may legitimately
combine artifacts from different Assets.

------------------------------------------------------------------------

# Asset Evolution

An asset evolves through stages.

Example:

    Creative Intent

            ↓

    Concept Artifact

            ↓

    Refined Visual Artifact

            ↓

    3D Artifact

            ↓

    Rigged Artifact

            ↓

    Engine-ready Artifact

Each stage should preserve traceability.

The system should be able to understand:

-   what was generated
-   when it was generated
-   which workflow produced it
-   which capability produced it
-   which parameters influenced the result

An Artifact records the optional identifier of the Workflow Execution that
produced it. The identifier is optional because an imported or user-provided
artifact may exist without a workflow execution.

------------------------------------------------------------------------

# Workflow Relationship

Assets are transformed through workflows.

Example:

    Asset

      |
      |
      +-- Character Generation Workflow
              |
              +-- Image Generation Capability
              |
              +-- 3D Generation Capability
              |
              +-- Rigging Capability
              |
              +-- Export Capability

The workflow describes how an asset progresses.

The asset itself should not know implementation details about the
workflow engine.

------------------------------------------------------------------------

# Versioning and History

The lifecycle must preserve previous results.

A new generation should not destroy previous artifacts.

Example:

    Corrupted Knight

    Artifacts:

    v1 concept.png
    v2 concept.png
    v3 concept.png

The user should be able to:

-   compare results
-   return to previous stages
-   continue from an earlier artifact

------------------------------------------------------------------------

# Provider Independence

Artifacts must not depend on a specific generation technology.

The same lifecycle should support:

    Model A
       |
       ↓
    Artifact

    Model B
       |
       ↓
    Artifact

The asset lifecycle remains the same.

Only the capability implementation changes.

------------------------------------------------------------------------

# Testing Principles

Lunar will use testing pragmatically.

The objective is not achieving arbitrary test coverage percentages.

Tests should protect important behaviour.

Priority areas:

## Domain Rules

Examples:

-   lifecycle transitions
-   invariants
-   important business rules

## Critical Workflows

Examples:

-   asset creation
-   artifact generation flow
-   execution state changes

## Integration Boundaries

Examples:

-   provider communication
-   persistence behaviour
-   external system interaction

Implementation details that do not provide business value do not require
tests by default.

The rule:

> Important behaviour requires tests. Not every line requires a test.

------------------------------------------------------------------------

# Current Scope

This document does not define:

-   database schema
-   artifact storage implementation
-   workflow engine implementation
-   provider contracts

Those decisions should be introduced when required by the product.
