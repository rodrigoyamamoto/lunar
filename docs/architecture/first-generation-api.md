# First Generation API Vertical

## Status

This is the first HTTP vertical that exposes Lunar's existing artifact
generation architecture to the product frontend. It is an internal/product
development v0 contract, not the final public API.

## Endpoints

### POST /api/assets

Creates a new Asset for the first product loop. This endpoint exists only
to support the product generation loop, not as generic Asset CRUD.

Request:

```json
{
  "name": "Ruined Gothic Watchtower",
  "assetType": "Environment"
}
```

Response (201 Created):

```json
{
  "assetId": "019...",
  "name": "Ruined Gothic Watchtower",
  "assetType": "Environment"
}
```

Valid `AssetType` values are: `Character`, `Weapon`, `Environment`, `Prop`.

The transport DTO uses a string for `assetType` (not the domain enum). The
endpoint validates the string against the exact named enum values and rejects
numeric representations (`"1"`, `1`). This keeps the browser/backend contract
explicit and prevents accidental enum-serializer symmetry from masking
contract drift.

### POST /api/generations

Creates a `WorkflowExecution` for an existing Asset using the built-in
text-to-image workflow, starts it, executes the configured step with a
`TextPromptInput`, and returns the resulting Artifact identity and metadata.

The product-facing request does not require workflow internals:

```json
{
  "assetId": "019...",
  "prompt": "A ruined gothic watchtower under a blood-red eclipse"
}
```

Response (201 Created):

```json
{
  "workflowExecutionId": "019...",
  "artifactId": "019...",
  "assetId": "019...",
  "artifactName": "test-output.jpg",
  "artifactType": "ConceptImage",
  "mediaType": "image/jpeg",
  "contentUrl": "/api/artifacts/019.../content"
}
```

The endpoint resolves the configured `GenerationWorkflowTarget` (workflow
definition ID, version, and step position) inside Application via
`GenerateDefaultArtifactService`. The caller never needs to know
`WorkflowDefinitionId`, `WorkflowDefinitionVersion`, or `StepPosition`.

### GET /api/artifacts/{artifactId}/content

Retrieves the durable physical content for an Artifact. Returns the exact
binary bytes with the stored media type. Does not Base64-encode, wrap in
JSON, expose filesystem paths, or call the provider.

### GET /api/assets/{assetId}/artifacts

Lists all Artifacts belonging to an Asset. Returns summary DTOs with
`contentUrl` references — no bytes, Base64, filesystem paths, provider
details, or prompt text.

Response (200 OK):

```json
[
  {
    "artifactId": "019...",
    "assetId": "019...",
    "artifactName": "test-output.jpg",
    "artifactType": "ConceptImage",
    "createdAt": "2026-08-27T12:34:56Z",
    "contentUrl": "/api/artifacts/019.../content"
  }
]
```

Behavior:

```text
valid existing Asset, no Artifacts -> 200 []
valid existing Asset, Artifacts    -> 200 [...]
missing Asset                       -> 404 asset_not_found
malformed/empty ID                  -> 400
```

The endpoint delegates to `ListAssetArtifactsService`, which validates
Asset existence, queries Artifacts by exact `AssetId`, and returns
deterministic newest-first ordering (`CreatedAt` descending, then
`ArtifactId` descending as tie-break). Sort policy is owned by
Application, not the repository.

Prompt text is not persisted to Artifact metadata in the current model.
The gallery response does not include prompt provenance. Generation input
provenance is future work.

### Asset Workspace Identity (Frontend)

Once an Asset is created in the frontend workspace, that Asset remains the
active creative identity for subsequent generations. Asset name/type are
not edited to implicitly create another Asset. The user explicitly chooses
`New asset` to start another Asset workspace.

```text
Asset creation mode:
    name/type editable
    first Generate creates Asset + generates
Asset workspace mode:
    name/type read-only
    prompt editable
    Generate always uses active AssetId
    New asset resets workspace (no backend delete)
```

If Asset creation succeeds but generation fails, the frontend stays in
workspace mode for that Asset. Retry reuses the same `AssetId`. Failed
generations preserve the active Asset, previous gallery, and selected
Artifact.

## Architecture

```text
HTTP request
    ↓
Lunar.Api (transport/composition boundary)
    ↓
GenerateDefaultArtifactService (Application, product-level)
    ↓
GenerateArtifactService (Application, lower-level)
    ↓
CreateWorkflowExecutionService → StartWorkflowExecutionService → ExecuteWorkflowStepService
    ↓
Lunar.Core ports/domain
    ↑
Lunar.Infrastructure adapters
```

The API layer owns:
- transport DTOs (`GenerationRequest`, `GenerationResponse`, `CreateAssetRequest`,
  `CreateAssetResponse`, `ArtifactSummaryResponse`, `ApiErrorResponse`);
- endpoint routing and HTTP status mapping;
- composition root (DI registration, configuration binding, startup validation);
- `FirstProductLoopWorkflowBootstrap` (runtime workflow seeding);
- `ApplicationErrorHttpMapping` (deterministic Application error to HTTP status).

The API layer does not own:
- generation orchestration sequence;
- workflow lifecycle rules;
- step prevalidation;
- revision-zero knowledge;
- Artifact persistence rules;
- Cloudflare logic;
- filesystem logic.

## Runtime Workflow Bootstrap

`FirstProductLoopWorkflowBootstrap` ensures the built-in text-to-image
`WorkflowDefinition` exists at application startup. It uses stable UUID v7
typed identifiers for the workflow definition and capability. The built-in
workflow is named `Text to Image`; its step references a stable
provider-independent `CapabilityId`. The bootstrap does not encode Cloudflare
or FLUX naming into the workflow contract. The bootstrap is idempotent and
safe to run multiple times.

Bootstrap semantics:

```text
missing                    -> insert expected definition
existing and compatible    -> no-op
existing same ID/version
  but incompatible         -> throw InvalidOperationException at startup
TryAddAsync returns false
  (concurrent writer)      -> re-read
                              compatible    -> success
                              missing       -> throw
                              incompatible  -> throw
```

Compatibility compares: ID, version, exact name, exact step count/order,
exact step position, and exact capability ID.

The bootstrap is a temporary product-phase composition mechanism while
workflow authoring and durable workflow persistence are not implemented.
It is not a future workflow authoring engine.

## Application Services

The asset creation endpoint delegates to:

- `CreateAssetService` — validates the caller-supplied name and `AssetType`,
  constructs an `Asset` with a Lunar-owned `AssetId`, and persists it via
  `IAssetRepository.TryAddAsync`. Returns `AssetPersistenceFailed` if the
  repository rejects the insert.

The gallery listing endpoint delegates to:

- `ListAssetArtifactsService` — validates non-empty `AssetId`, loads the
  Asset via `IAssetRepository.GetAsync` (returns `AssetNotFound` if
  missing), queries Artifacts by exact `AssetId` via
  `IArtifactRepository.GetByAssetIdAsync`, and returns deterministic
  newest-first ordering (`CreatedAt` descending, then `ArtifactId`
  descending). Does not retrieve bytes, call a workflow, mutate lifecycle,
  traverse lineage, or call Cloudflare.

The generation endpoint delegates to a single product-level Application use case:

- `GenerateDefaultArtifactService` — receives an `AssetId` and
  `CapabilityExecutionInput`, resolves the configured `GenerationWorkflowTarget`
  (workflow definition ID, version, step position), and delegates to
  `GenerateArtifactService`.

The lower-level generation service:

- `GenerateArtifactService` — prevalidates the exact WorkflowDefinition version
  and requested step position before any side effect. If the step is missing,
  returns `WorkflowStepNotFound` without creating, starting, or executing
  anything. On success, orchestrates `CreateWorkflowExecutionService` →
  `StartWorkflowExecutionService` (revision 0) → `ExecuteWorkflowStepService`,
  and returns `GeneratedArtifact` (pairing `WorkflowExecutionId` with
  `ProducedArtifact`).

The lower-level services remain:

1. `CreateWorkflowExecutionService` — validates Asset and WorkflowDefinition
   existence, creates a new `WorkflowExecution` in `Created` status, persists it.
2. `StartWorkflowExecutionService` — starts the execution through the existing
   lifecycle rules (Created → Running).
3. `ExecuteWorkflowStepService` — executes the specified step, persists content
   before metadata, applies compensation on failure.

The content endpoint uses:

- `GetArtifactContentService` — loads Artifact metadata and durable content,
  returns `ProducedArtifact`.

## Missing-Step No-Side-Effect Guarantee

`GenerateArtifactService` loads the exact WorkflowDefinition version and
searches for a step at the requested position before calling
`CreateWorkflowExecutionService`. If no step matches, the service returns
`WorkflowStepNotFound` immediately. No `WorkflowExecution` is persisted, no
execution is started, and no capability executor is called.

## Binary Content Contract Guard

Both endpoints use an explicit type guard instead of a direct cast:

```csharp
if (produced.Content is not BinaryArtifactContent binaryContent)
{
    throw new InvalidOperationException(
        "The first generation API requires binary artifact content.");
}
```

This is a programmer/configuration defect, not a client error. It is not
mapped to 400 or 404.

## Request Validation

Validation is performed explicitly at the API boundary without
FluentValidation. Each endpoint validates only its own transport contract.

### POST /api/assets

- `name` must not be null, empty, or whitespace;
- `assetType` must be one of the exact named product values:
  `Character`, `Weapon`, `Environment`, `Prop`. Numeric representations
  (`"1"`, `1`) are rejected.

### POST /api/generations

- `assetId` must be a valid non-empty UUID;
- `prompt` must not be null, empty, or whitespace.

The configured workflow definition ID, version, and step position come from
`GenerationWorkflowTarget` inside Lunar (Application composition), not from
the HTTP request. The caller never supplies `WorkflowDefinitionId`,
`WorkflowDefinitionVersion`, or `StepPosition`.

### GET /api/assets/{assetId}/artifacts

- `assetId` route parameter must be a valid non-empty UUID.

### GET /api/artifacts/{artifactId}/content

- `artifactId` route parameter must be a valid non-empty UUID.

Valid prompt text is preserved exactly into `TextPromptInput.Prompt`.
No provider-specific prompt maximum is imposed at the HTTP boundary.

## HTTP Error Mapping

`ApplicationErrorHttpMapping` provides deterministic mapping:

| Application Error | HTTP Status |
|---|---|
| `AssetNotFound` | 404 |
| `WorkflowDefinitionNotFound` | 404 |
| `WorkflowStepNotFound` | 404 |
| `WorkflowExecutionNotFound` | 404 |
| `ArtifactNotFound` | 404 |
| `ArtifactContentNotFound` | 404 |
| `WorkflowExecutionConcurrencyConflict` | 409 |
| `WorkflowExecutionCannotStart` | 409 |
| `WorkflowExecutionNotRunning` | 409 |
| `AssetPersistenceFailed` | 503 |
| `WorkflowExecutionPersistenceFailed` | 503 |
| `ArtifactContentPersistenceFailed` | 503 |
| `ArtifactPersistenceFailed` | 503 |
| `WorkflowStepExecutionFailed` (Rejected) | 422 |
| `WorkflowStepExecutionFailed` (AuthenticationFailed) | 503 |
| `WorkflowStepExecutionFailed` (AccessDenied) | 503 |
| `WorkflowStepExecutionFailed` (QuotaExhausted) | 503 |
| `WorkflowStepExecutionFailed` (RateLimited) | 429 + Retry-After |
| `WorkflowStepExecutionFailed` (PaidPlanRequired) | 503 |
| `WorkflowStepExecutionFailed` (TimedOut) | 504 |
| `WorkflowStepExecutionFailed` (TemporarilyUnavailable) | 503 |
| `WorkflowStepExecutionFailed` (RemoteOutcomeUnknown) | 502 |
| `WorkflowStepExecutionFailed` (InvalidResponse) | 502 |

Provider authentication failures map to 503, not 401 — the caller did not
fail Lunar authentication.

For `RateLimited`, a `Retry-After` response header is included when
`RetryAfter` is present in the failure.

## Cancellation

The HTTP request's cancellation token flows through every Application
service call in the generation path and into infrastructure:

```text
ASP.NET request
   → GenerateDefaultArtifactService
   → GenerateArtifactService
   → CreateWorkflowExecutionService
   → StartWorkflowExecutionService
   → ExecuteWorkflowStepService
   → ICapabilityExecutor
   → IArtifactContentStore
```

The only exception is compensation cleanup, which intentionally uses
`CancellationToken.None` per the durable content storage contract.

This chain represents the generation endpoint only. The gallery
(`GET /api/assets/{assetId}/artifacts`) and content
(`GET /api/artifacts/{artifactId}/content`) endpoints propagate the
request cancellation token through their own Application service calls
(`ListAssetArtifactsService`, `GetArtifactContentService`) and repository
queries.

## No Retries

No retry, hedging, Polly, or resilience handler is added. The provider
performs one generation attempt. This guarantee is preserved from the
Cloudflare slice.

## No Background Jobs

Generation is synchronous from the HTTP caller's perspective. No job
queue, background worker, SignalR, SSE, or WebSocket is added.

## Composition Root

The API composition root (`Program.cs`) registers:

- In-memory repositories (`IAssetRepository`, `IWorkflowDefinitionRepository`,
  `IWorkflowExecutionRepository`, `IArtifactRepository`) as Singleton —
  they use `ConcurrentDictionary` and are safe for concurrent access;
- `LocalFileArtifactContentStore` as Singleton for `IArtifactContentStore`,
  with `LocalRootPath` from `ArtifactContentStorage` configuration;
- `GenerationWorkflowTarget` as Singleton — a configured value object
  carrying the built-in workflow definition ID, version, and step position;
- Application services as Transient (stateless orchestration):
  `CreateAssetService`, `ListAssetArtifactsService`,
  `CreateWorkflowExecutionService`, `StartWorkflowExecutionService`,
  `ExecuteWorkflowStepService`, `GetArtifactContentService`,
  `GenerateArtifactService`, `GenerateDefaultArtifactService`;
- `ICapabilityExecutor` as Transient, implemented by
  `CloudflareWorkersAiTextToImageExecutor` (the current Cloudflare
  Workers AI adapter at the composition edge).

Before normal request handling, the composition root calls
`FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync` against
the registered `IWorkflowDefinitionRepository` to seed the built-in
text-to-image workflow definition. This is a runtime product-phase
mechanism, not a recurring background job.

## Configuration

```json
{
  "ArtifactContentStorage": {
    "LocalRootPath": "data/artifacts"
  }
}
```

Relative paths resolve against the application content root. The
`LocalFileArtifactContentStoreOptions` is validated at host startup via
`.Validate(...)` and `.ValidateOnStart()`. If `LocalRootPath` is missing
or blank, the host throws `OptionsValidationException` during startup
and refuses to run.

## In-Memory Metadata Restart Limitation

Artifact content is durable (survives process restart if the same content
root remains), but Asset, WorkflowDefinition, WorkflowExecution, and
Artifact metadata are still in-memory. After a process restart, durable
content files may exist for Artifact IDs whose metadata is no longer
available. This is a known limitation until durable metadata persistence
is implemented.

## Testing

All HTTP tests are fully offline:
- `ICapabilityExecutor` is overridden with a deterministic test double;
- content storage root is overridden with a unique temporary directory;
- in-memory repositories are used;
- no Cloudflare credentials, internet access, or external databases.

## Out of Scope

- durable metadata persistence (PostgreSQL, EF Core);
- cloud content stores (R2, S3, Azure Blob);
- generic CRUD endpoints;
- provider/model selector or registry;
- retry, fallback, or hedging;
- job queue, background worker, progress endpoint;
- authentication, authorization, user accounts;
- API versioning framework;
- OpenAPI customization or Swagger UI redesign;
- streaming content abstraction;
- workflow automatic progression or completion;
- generation input provenance (prompt text is not persisted to Artifact metadata);
- reference image / image-to-image generation;
- observability foundation (OpenTelemetry not yet introduced);
- artifact delete/rename, favorites, tags, folders, multi-select, pagination, or search.
