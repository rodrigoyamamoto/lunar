# ADR-004 - Workflow Definition Versioning



## Status



Accepted



## Context



A Workflow Execution previously stored only `WorkflowDefinitionId`.

Once reusable workflows evolve, that is insufficient unless IDs are
replaced for every modification, which loses stable logical workflow
identity.

Lunar needs reproducible historical execution traceability: an execution
created against a specific definition version must continue to refer to
that exact version even after later versions of the same logical workflow
are introduced.



## Decision



`WorkflowDefinitionId` represents the stable logical identity of a
reusable workflow definition across versions.

`int Version` (a positive integer, `>= 1`) represents an immutable
version number scoped to that `WorkflowDefinitionId`.

The exact immutable definition version is identified by the pair:

```text
(WorkflowDefinitionId, Version)
```

`WorkflowExecution` stores both `WorkflowDefinitionId` and
`WorkflowDefinitionVersion`, so an execution refers to the exact
historical definition version it was created against.

Workflow Definitions are immutable. Changing definition contents creates
another immutable version with the same `WorkflowDefinitionId` and a new
positive `Version`, rather than mutating the previous version.

Version numbers are scoped to a `WorkflowDefinitionId` and are not
globally unique. The Core does not enforce contiguity, latest-version
resolution, or version-sequence allocation — those are future
persistence/application concerns.



## Consequences



Benefits:

- historical executions remain unambiguous;
- definitions can evolve under stable identity;
- no execution-owned step snapshot is required yet;
- no provider/runtime concerns enter Core.



Trade-offs:

- persistence must eventually preserve immutable historical versions;
- uniqueness of `(WorkflowDefinitionId, Version)` must eventually be
  enforced;
- determining the next version number is not currently a Core
  responsibility;
- deletion/retention rules for historical versions remain future work.



## Rejected Alternatives



1.  Generating a completely unrelated `WorkflowDefinitionId` for every
    edit. This loses stable logical workflow identity and makes it
    impossible to group versions of the same workflow.

2.  Referencing only the latest logical workflow ID from executions.
    This breaks historical reproducibility because an execution's
    meaning would silently change when a new version is introduced.

3.  Snapshotting all workflow steps inside every execution now. This
    duplicates definition contents into every execution and introduces
    aggregate/persistence concerns prematurely.

4.  Introducing UUID identifiers for versions (`WorkflowDefinitionVersionId`).
    A version is an ordinal domain value scoped to a definition, not a
    globally persistent entity. A UUID adds complexity without a current
    requirement.
