# ADR-001 - Lunar Foundation Architecture

## Status

Accepted

## Context

Lunar Asset Studio is an AI-assisted asset creation platform.

The system orchestrates multiple specialised tools and AI models to
transform creative intent into production-ready game assets.

The architecture must avoid:

-   vendor lock-in
-   model lock-in
-   cloud lock-in
-   engine lock-in

The platform should allow replacing individual capabilities without
requiring changes across the entire system.

## Decisions

## Backend

The backend uses:

-   .NET 10
-   C#
-   Modular monolith architecture initially

The backend coordinates workflows, manages asset lifecycle, and exposes
application capabilities.

Specialised generation and processing capabilities should not be tightly
implemented inside the core application.

## Frontend

The frontend uses:

-   React
-   TypeScript

The frontend is responsible for providing a clear workflow experience,
including progress visibility, asset history, and user interaction with
generation pipelines.

## Workers

Specialised processing runs outside the core application through
workers.

Examples:

-   image generation
-   3D generation
-   rigging
-   mesh processing
-   Blender automation
-   Unreal Engine export preparation

Workers communicate through explicit contracts.

The core application must not depend on the internal implementation
details of individual workers.

## Provider Abstraction

AI providers and processing tools must be replaceable.

The core domain must not depend on:

-   specific AI models
-   inference frameworks
-   external vendors
-   cloud providers

A provider should be replaceable through configuration and
implementation of defined abstractions.

## Configuration

Runtime behaviour must be configuration driven.

Hardcoded values are prohibited for:

-   provider configuration
-   model selection
-   workflow definitions
-   environment-specific behaviour
-   runtime capabilities

Configuration should allow evolution of the platform without requiring
code changes for normal operational changes.

## Asset Lifecycle

Assets are treated as evolving artifacts.

A single asset may progress through multiple stages:

Concept → Generated image → Refined concept → 3D model → Rigged model →
Optimised asset → Engine package

Each stage should produce traceable artifacts.

The system should preserve history and allow returning to previous
stages when required.

## Storage

Source code, configuration, and architectural decisions are versioned
with Git.

Large generated artifacts should not be stored directly in the source
repository.

Future storage strategies may include dedicated asset storage solutions.

## Identifiers

New persistent entities use UUID version 7 identifiers.

This provides time-ordered identifiers suitable for distributed systems
and asset lifecycle tracking.

## Initial Engine Target

Unreal Engine is the first target platform.

The architecture must avoid preventing future support for other engines
or runtime environments.

## Consequences

This architecture prioritises:

-   flexibility
-   replaceability
-   maintainability
-   long-term evolution

over premature optimisation or unnecessary complexity.

The system should remain pragmatic: complexity should exist only where
the problem requires it.
