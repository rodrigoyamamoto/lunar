# Workflow Execution Model

## Status

Accepted

## Purpose

This document defines the initial workflow execution model for Lunar
Asset Studio.

The goal is to represent the execution journey that transforms user
intent into generated assets without introducing a complete workflow
engine prematurely.

## Context

Lunar Asset Studio will orchestrate multiple specialized capabilities:

-   image generation
-   model generation
-   validation
-   rigging
-   optimization
-   export preparation

Each capability may be provided by different implementations or
providers.

The workflow model must represent execution history and produced
artifacts while remaining independent from specific tools or vendors.

## Decision

The first version introduces the concept of a Workflow Execution.

A Workflow Execution represents a single attempt to execute a generation
process.

It does not represent:

-   a workflow designer
-   a visual node editor
-   a scheduler
-   a distributed workflow engine

Those capabilities may be introduced later if required.

## Core Concepts

### Workflow Execution

Represents one execution instance.

Responsibilities:

-   identify the execution
-   identify the Asset being processed
-   identify the Workflow Definition being executed
-   track lifecycle status
-   provide traceability

Every Workflow Execution references:

-   `AssetId AssetId` — the Asset being processed.
-   `WorkflowDefinitionId WorkflowDefinitionId` — the Workflow Definition being
    executed.

An execution cannot be created without both identifiers. See the
[Workflow Definition Model](./workflow-definition-model.md) for the definition
and capability concepts.

### Workflow Execution Status

Initial statuses:

-   Created
-   Running
-   Completed
-   Failed
-   Cancelled

The model should evolve only when real requirements appear.

## Relationship With Artifacts

Workflow executions produce artifacts.

Example:

User request:

"Create a dark fantasy warrior"

Execution:

    WorkflowExecution
        |
        +-- Concept Image Artifact
        |
        +-- Generated Model Artifact
        |
        +-- Rigged Model Artifact

Artifacts remain independent domain entities.

An Artifact stores an optional `SourceExecutionId` identifying the workflow
execution that produced it. The relationship is optional because imported or
user-provided artifacts may not originate from a Lunar workflow execution.

The Workflow Execution does not own an in-memory collection of artifacts in
this initial model. This keeps persistence and aggregate decisions outside the
domain until they are required.

## Future Evolution

Possible future concepts:

-   workflow templates
-   retries
-   checkpoints
-   resumable executions
-   provider selection

These are intentionally not part of the first implementation.

## Principles

-   Keep domain models simple.
-   Avoid premature abstraction.
-   Avoid coupling with external providers.
-   Prefer explicit models over generic frameworks.
