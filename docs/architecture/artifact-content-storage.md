# Artifact Content Storage

## Overview

Lunar separates Artifact metadata (identity, provenance, type) from
physical produced content (binary bytes, media type). This document
describes the provider-neutral content storage boundary and its first
local filesystem implementation.

## Core Boundary

`IArtifactContentStore` is a Core port owned by `Lunar.Core.Artifacts`.
It is provider-neutral and storage-technology neutral. It knows only
about `ArtifactId` and `ArtifactContent`.

```text
TryAddAsync(ArtifactId, ArtifactContent, CancellationToken) -> bool
GetAsync(ArtifactId, CancellationToken) -> ArtifactContent?
TryDeleteAsync(ArtifactId, CancellationToken) -> bool
```

The port does not expose paths, URLs, keys, buckets, streams, or any
storage-technology concept. Core and Application contain no references
to filesystem, cloud, HTTP, S3, R2, Azure, or any provider technology.

`Artifact` remains metadata-only. No content, path, key, URL, or
storage location is added to `Artifact`. The connection between metadata
and physical content is the typed `ArtifactId`.

## First Implementation: Local Filesystem

`LocalFileArtifactContentStore` in `Lunar.Infrastructure.FileSystem`
is the first concrete implementation.

### Durable Representation

Each Artifact's content is stored in a dedicated directory under the
configured root:

```text
<root>/
    <artifact-id-normalized>/
        content.bin
        metadata.json
```

- The directory name is `ArtifactId.Value.ToString("N")` — a
  filesystem-safe deterministic format derived only from the UUID.
- `content.bin` contains the raw binary bytes. No Base64 encoding is
  used in durable storage.
- `metadata.json` is a minimal Infrastructure-owned sidecar:

```json
{
  "schemaVersion": 1,
  "contentKind": "binary",
  "mediaType": "image/jpeg"
}
```

- `schemaVersion` is explicit and allows future format evolution.
- `contentKind` distinguishes the supported binary representation.
- `mediaType` preserves the exact value from `BinaryArtifactContent`.
- No Artifact metadata (name, type, asset, provenance) is duplicated.
- No provider/model/Cloudflare data is stored.
- No caller-supplied filename or path is stored.

### Path Safety

The final artifact path is derived only from:

```text
validated root directory + ArtifactId + fixed filenames
```

No user-controlled string becomes a path segment. `Artifact.Name`,
prompt text, media type, provider name, and model name are never used
as path components.

### Atomic Publication

Content is published atomically using a same-filesystem temp directory
and final rename/move:

```text
create unique temp directory under store root
    ↓
write content.bin fully
    ↓
write metadata.json fully
    ↓
flush/close both files
    ↓
check cancellation before publication
    ↓
atomically rename/move temp directory to final ArtifactId directory
    ↓
return true
```

A caller never observes a partially published final directory. The
final directory rename/move is the publication boundary.

### Temp Cleanup Semantics

The store distinguishes ordinary runtime cleanup from process crashes:

- **Ordinary non-published path** (cancellation, write failure, duplicate
  target): temp cleanup is attempted in the `finally` block. A
  publication flag ensures cleanup is never attempted after successful
  publication.
- **Cleanup technical failure**: if `Directory.Delete` throws during
  ordinary cleanup, the exception propagates. It is not swallowed. The
  caller sees the technical failure.
- **Process/machine crash**: a crash between temp creation and cleanup
  can leave an abandoned temp directory. This is a known limitation. No
  startup scavenger or reconciliation worker is implemented in this
  slice.

`TryDeleteAsync == false` during compensation is acceptable because the
desired postcondition (content absent) already holds.

### Insert-Only Semantics

`TryAddAsync` is insert-only. If content for an `ArtifactId` already
exists, it returns `false` and never overwrites. Concurrent writes for
the same `ArtifactId` result in exactly one success.

### Cancellation Semantics

- Cancellation before publication leaves no final durable entry and
  propagates `OperationCanceledException`.
- After successful publication, the operation returns `true` — no
  post-publication cancellation check can convert a known success into
  `OperationCanceledException`.

### Corrupt State Detection

`GetAsync` fails loudly (throws `InvalidDataException`) for corrupt or
incomplete durable state:

- missing metadata file
- missing content file
- malformed metadata JSON
- unsupported schema version
- unsupported content kind
- blank/invalid media type
- empty content file

Corrupt state is not the same as "not found" and does not silently
return null.

## Application Integration

`ExecuteWorkflowStepService` depends on `IArtifactContentStore` and
follows strict persistence ordering:

```text
1. construct Artifact using Lunar-owned ArtifactId
2. persist physical ArtifactContent using that ArtifactId
3. persist Artifact metadata
4. return ProducedArtifact
```

Content is persisted before metadata. Lunar never intentionally
persists metadata that points to physical content which was never
stored.

### Compensation

If content persistence succeeds but metadata persistence fails or
throws, the service attempts to delete the stored content using
compensation:

```text
content store add succeeds
    ↓
artifact repository add returns false or throws
    ↓
attempt content-store TryDeleteAsync with a non-cancelled token
    ↓
return ArtifactPersistenceFailed
```

The compensation token is `CancellationToken.None` — caller
cancellation must not prevent cleanup from being attempted. If
compensation itself throws, the exception is not swallowed.

### Application Errors

- `ArtifactContentPersistenceFailed` — content store returned `false`.
- `ArtifactPersistenceFailed` — metadata repository returned `false`
  (existing error, now with compensation).

## Cross-Store Crash Limitation

Local filesystem content persistence and `IArtifactRepository` metadata
persistence do not share a transaction. Compensation reduces
inconsistency but cannot provide perfect atomicity across process
crashes.

The following residual window exists:

```text
content final publication succeeds
    ↓
process crashes before metadata persistence or compensation
    ↓
orphan physical content may remain
```

No distributed transaction, event sourcing, outbox, or reconciliation
worker exists in this slice. Abandoned temp directories from process
crashes may also remain; no startup scavenger is implemented.

## Future Implementations

The same `IArtifactContentStore` contract can be implemented by:

- Cloudflare R2
- Amazon S3
- Azure Blob Storage
- another durable content service

without changing `Artifact`, `ExecuteWorkflowStepService` semantics,
or capability executors. No cloud store is implemented in this slice.

## Out of Scope

- content hash / deduplication
- content-addressable storage
- streaming abstraction
- chunking / multi-file bundles
- download API endpoint
- frontend / preview UI
- job queue / worker / progress
- retry orchestration
- provider fallback
- distributed transaction
- event sourcing / outbox
- reconciliation worker
- temp-directory scavenger
