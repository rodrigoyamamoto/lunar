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
  `CreateAssetResponse`, `ApiErrorResponse`);
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
typed identifiers for the workflow definition and capability. The capability
is named "Text to Image" — provider-neutral. The bootstrap is idempotent and
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
FluentValidation:

- `assetId` must be a valid non-empty UUID;
- `workflowDefinitionId` must be a valid non-empty UUID;
- `workflowDefinitionVersion >= 1`;
- `stepPosition >= 1`;
- `prompt` must not be null, empty, or whitespace.

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
service call and into infrastructure:

```text
ASP.NET request
   → GenerateArtifactService
   → CreateWorkflowExecutionService
   → StartWorkflowExecutionService
   → ExecuteWorkflowStepService
   → ICapabilityExecutor
   → IArtifactContentStore
```

The only exception is compensation cleanup, which intentionally uses
`CancellationToken.None` per the durable content storage contract.

## No Retries

No retry, hedging, Polly, or resilience handler is added. The provider
performs one generation attempt. This guarantee is preserved from the
Cloudflare slice.

## No Background Jobs

Generation is synchronous from the HTTP caller's perspective. No job
queue, background worker, SignalR, SSE, or WebSocket is added.

## Composition Root

The API composition root registers:

- In-memory repositories as Singleton (they use `ConcurrentDictionary`
  and are safe for concurrent access);
- `LocalFileArtifactContentStore` as Singleton with configuration from
  `ArtifactContentStorage:LocalRootPath`;
- `GenerateArtifactService` as Transient (stateless Application orchestration);
- Lower-level Application services as Transient;
- `ICapabilityExecutor` as Transient (Cloudflare Workers AI).

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

- frontend;
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
- workflow automatic progression or completion.
