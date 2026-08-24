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
