# ADR-002 - Domain Modeling Principles

## Status

Accepted

## Context

Lunar Asset Studio is designed as a platform that coordinates multiple
specialised capabilities to transform creative intent into
production-ready game assets.

As the platform evolves, there is a risk of introducing unnecessary
complexity, coupling the core product to implementation details, or
creating abstractions before real requirements exist.

This document defines the principles that guide domain modeling
decisions.

------------------------------------------------------------------------

# Core Domain Responsibility

The Core domain represents concepts that are fundamental to Lunar.

The Core must contain business concepts and rules.

The Core must not depend on:

-   databases
-   file systems
-   AI providers
-   inference frameworks
-   cloud services
-   game engines
-   external SDKs

External technologies should adapt to the Core, never the opposite.

------------------------------------------------------------------------

# Model the Problem, Not the Implementation

Domain models should represent concepts from the product.

Examples:

Good domain concepts:

-   Asset
-   Artifact
-   Workflow
-   Capability
-   Execution

Implementation details should not become domain concepts.

Examples:

Avoid:

-   TrellisAssetGenerator
-   BlenderMeshProcessor
-   UnrealExporterService

These are implementations of capabilities, not core concepts.

------------------------------------------------------------------------

# Avoid Premature Abstraction

Abstractions should be introduced because a real requirement exists.

Do not create:

-   generic interfaces without consumers
-   unnecessary inheritance hierarchies
-   framework-like layers
-   excessive wrappers

The preferred approach is:

1.  Understand the problem.
2.  Implement the simplest solution.
3.  Introduce abstraction when change requires it.

------------------------------------------------------------------------

# Composition Over Complexity

The system should prefer simple composition.

A complex asset lifecycle should emerge from combining clear concepts:

    Asset
     |
     +-- Artifact
     |
     +-- Workflow
     |
     +-- Capability
     |
     +-- Execution

Avoid creating large objects that try to represent every possible
scenario.

------------------------------------------------------------------------

# Provider Independence

AI models and external tools are replaceable implementation details.

The domain must not know:

-   which model generated an asset
-   where inference runs
-   which vendor provides the capability

The system should allow changing:

-   models
-   providers
-   execution environments

without changing core business concepts.

------------------------------------------------------------------------

# Configuration Driven Behaviour

Runtime decisions should come from configuration.

The following should not be hardcoded:

-   model selection
-   provider settings
-   execution parameters
-   workflow definitions
-   environment-specific paths

Code should define behaviour.

Configuration should define choices.

------------------------------------------------------------------------

# Entity Identity

Persistent domain entities use UUID version 7 identifiers.

Identifiers should provide:

-   uniqueness
-   ordering characteristics
-   suitability for distributed workflows

Entity identity should not depend on database-generated values.

------------------------------------------------------------------------

# Domain Objects and Persistence

Domain objects should not be designed around database concerns.

Avoid:

-   leaking ORM behaviour into entities
-   database-specific rules in Core
-   persistence-driven models

Persistence is an infrastructure responsibility.

------------------------------------------------------------------------

# Testing Principles

Tests should protect domain behaviour.

Priorities:

-   business rules
-   lifecycle transitions
-   invariants

Avoid writing tests that only verify implementation details.

------------------------------------------------------------------------

# Simplicity Principle

The architecture should remain pragmatic.

Complexity is acceptable when the problem requires it.

Complexity introduced only for theoretical flexibility should be
avoided.

The goal is a system that can evolve without becoming difficult to
understand.

------------------------------------------------------------------------

# Current Scope

This document does not define:

-   persistence strategy
-   API design
-   workflow execution engine
-   messaging architecture
-   deployment model

Those decisions should be introduced only when required by the product.
