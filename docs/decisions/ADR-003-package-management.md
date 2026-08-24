# ADR-003 - Central Package Management



## Status



Accepted



## Context



Lunar Asset Studio contains multiple .NET projects.



Independent package version management can create version drift and make

dependency upgrades harder to maintain.



## Decision



The repository uses .NET Central Package Management.



Package versions are defined centrally using:



Directory.Packages.props



Individual projects reference packages without defining versions.



## Consequences



Benefits:



- consistent dependency versions

- easier upgrades

- reduced duplication

- clearer dependency ownership



New dependencies should only be introduced when they solve a real problem.

