# Lunar Asset Studio - Domain Boundaries

## Purpose

This document defines the initial domain boundaries of Lunar Asset
Studio.

The objective is not to create a complex architecture prematurely, but
to establish clear ownership boundaries so the platform can evolve
without coupling the core product to specific models, vendors, engines,
or execution environments.

------------------------------------------------------------------------

# Domain Overview

Lunar Asset Studio is an AI-assisted asset creation platform.

The user provides an initial creative intent:

-   reference images
-   descriptions
-   prompts
-   target requirements

The platform coordinates a pipeline of specialized capabilities that
transform this intent into production-ready assets.

The orchestration belongs to Lunar. Individual generation capabilities
remain replaceable.

------------------------------------------------------------------------

# Core Domain

The Core domain contains concepts that represent the product itself.

It must not depend on:

-   AI providers
-   local models
-   cloud services
-   Blender
-   Unreal Engine
-   Unity
-   file systems
-   databases

## Asset

Represents a creative output managed by Lunar.

Examples:

-   character
-   weapon
-   environment prop
-   texture
-   animation package
-   3D model

The Asset concept represents identity and lifecycle, not implementation
details.

------------------------------------------------------------------------

## Artifact

Represents a generated or produced file/result during the asset
lifecycle.

Examples:

-   concept image
-   generated mesh
-   texture map
-   rigged model
-   Unreal-ready package

An Asset can have multiple Artifacts.

------------------------------------------------------------------------

## Workflow

Represents the sequence of operations required to transform an input
into a final result.

A workflow may contain:

-   generation steps
-   validation steps
-   processing steps
-   export steps

The user should not need to manually understand every internal step.

------------------------------------------------------------------------

## Capability

Represents something Lunar knows how to do.

Examples:

-   generate image
-   create 3D mesh
-   extract parts
-   generate rig
-   optimize geometry
-   export package

Capabilities are abstractions.

The implementation behind a capability can change.

------------------------------------------------------------------------

## Execution

Represents a running operation.

Examples:

-   generating a character
-   processing a mesh
-   exporting Unreal assets

Execution contains lifecycle information:

-   created
-   running
-   completed
-   failed
-   cancelled

------------------------------------------------------------------------

# Infrastructure Boundary

Infrastructure provides technical implementations required by the Core.

Examples:

## Persistence

Responsible for storing:

-   assets
-   artifacts
-   workflow state
-   execution history

The domain should not know the database technology.

------------------------------------------------------------------------

## Provider Adapters

Responsible for integrating with external systems.

Examples:

-   local AI models
-   cloud AI providers
-   inference servers

The Core should only know the capability contract.

------------------------------------------------------------------------

## File System

Responsible for handling physical storage.

Examples:

-   generated images
-   meshes
-   exported packages

------------------------------------------------------------------------

## Worker Communication

Responsible for communicating with specialized workers.

Examples:

-   Python inference workers
-   Blender automation workers
-   conversion workers

Workers are replaceable execution environments.

------------------------------------------------------------------------

# Workers Boundary

Workers execute specialized tasks.

Examples:

## AI Generation Worker

Responsible for:

-   image generation
-   model generation
-   inference execution

------------------------------------------------------------------------

## Blender Worker

Responsible for automation tasks:

-   importing assets
-   mesh operations
-   rig preparation
-   exporting formats

Lunar should communicate through contracts, not Blender internals.

------------------------------------------------------------------------

## Export Worker

Responsible for preparing final outputs.

Examples:

-   Unreal packages
-   texture bundles
-   optimized meshes

------------------------------------------------------------------------

# Dependency Direction

The dependency direction must remain:

    API
     |
    Infrastructure
     |
    Core

The Core is the center of the system.

External technologies depend on Lunar abstractions, never the opposite.

------------------------------------------------------------------------

# Initial Design Principles

## Replaceability

A provider or model can be replaced without rewriting the product.

------------------------------------------------------------------------

## Simplicity

Do not introduce abstractions without a real requirement.

------------------------------------------------------------------------

## Explicit Boundaries

Every component must have a clear responsibility.

------------------------------------------------------------------------

## No Hidden Configuration

No hardcoded:

-   provider settings
-   paths
-   model names
-   execution parameters

Configuration belongs outside the code.

------------------------------------------------------------------------

# Current Scope

This document intentionally does not define:

-   database schema
-   API contracts
-   workflow engine implementation
-   AI model selection
-   deployment architecture

Those decisions should emerge when the product requires them.
