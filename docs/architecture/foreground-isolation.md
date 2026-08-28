# Foreground Isolation (Background Removal)

## Overview

Foreground isolation removes the background from an existing image
Artifact, producing a new transparent PNG Artifact in the same Asset
with direct lineage to the source.

This is the first image-transformation product operation in Lunar,
extending the platform beyond text-to-image generation.

## Architecture

```text
User selects Artifact
    ↓
POST /api/artifacts/{artifactId}/remove-background
    ↓
RemoveArtifactBackgroundService (Application)
    ↓ loads source Artifact + content
    ↓ validates supported image media type
    ↓ constructs ImageArtifactInput (binary content only)
    ↓ passes WorkflowStepArtifactContext with:
    ↓   ArtifactName = "{source name} - background removed"
    ↓   ArtifactType = source Artifact.Type
    ↓   SourceArtifactIds = [source Artifact.Id]
    ↓
GenerateArtifactService (Application)
    ↓ prevalidates foreground-isolation workflow definition/step
    ↓ creates WorkflowExecution
    ↓ starts WorkflowExecution
    ↓
ExecuteWorkflowStepService (Application)
    ↓ resolves executor via ICapabilityExecutorResolver
    ↓ passes WorkflowStepArtifactContext to Artifact construction
    ↓
CloudflareImagesForegroundIsolationExecutor (Infrastructure)
    ↓ sends raw image bytes to Worker adapter
    ↓
Cloudflare Worker (provider-edge adapter)
    ↓ calls Cloudflare Images binding (segment=foreground)
    ↓ returns transparent PNG bytes
    ↓
Executor returns CapabilityExecutionOutput (content only)
    ↓ no name/type/lineage in output — all metadata is Application-owned
    ↓
Artifact content persisted (transparent PNG)
    ↓
Artifact metadata persisted (with Application-owned name, type, SourceArtifactIds)
    ↓
ArtifactTransformationResponse returned to API
```

## Metadata Ownership

The Application layer owns all Artifact business metadata: name, type,
and direct lineage. The provider executor only returns what it physically
produced: content. `WorkflowStepArtifactContext` defensively snapshots
`SourceArtifactIds` at construction time so caller-side mutation cannot
alter provenance after the context is created.

```text
RemoveArtifactBackgroundService
    → passes WorkflowStepArtifactContext with:
        ArtifactName = "{source artifact name} - background removed"
        ArtifactType = source Artifact.Type
        SourceArtifactIds = [source Artifact.Id] (defensively copied)
    → ExecuteWorkflowStepService applies them to Artifact construction
    → Artifact.Name = "{source artifact name} - background removed"
    → Artifact.Type = source Artifact.Type
    → Artifact.SourceArtifactIds = [source Artifact.Id]
```

`CapabilityExecutionOutput` carries only `Content`. The provider does not
return a name, type, or lineage. This prevents a provider from altering
Lunar business classification or provenance.

`ImageArtifactInput` carries only binary image content and media type.
It does not carry `SourceArtifactId` because the provider does not need
Lunar Artifact identity to transform bytes.

### Derived type preservation

The derived Artifact preserves the source Artifact's type:

```text
source Artifact.Type = Texture
    → derived Artifact.Type = Texture

source Artifact.Type = ConceptImage
    → derived Artifact.Type = ConceptImage
```

The provider cannot override this. The Cloudflare executor does not
supply `ArtifactType`.

### Naming convention

The derived Artifact name follows the Application-owned convention:

```text
{source artifact name} - background removed
```

Example:

```text
source name = "Knight Sprite"
    → derived name = "Knight Sprite - background removed"
```

The name does not contain `Cloudflare`, `BiRefNet`, `segment`, or
`provider`.

## Capability Routing

The single-executor model is replaced by `ICapabilityExecutorResolver`:

```text
ICapabilityExecutorResolver
    → resolves ICapabilityExecutor by CapabilityId
    → composition root registers deterministic dictionary
    → ExecuteWorkflowStepService resolves per step
    → CapabilityExecutorNotFound when no executor configured
    → duplicate registrations rejected at startup via Create()
```

The text-to-image and foreground-isolation capabilities are mapped to
their respective Infrastructure executors at composition time via
`CapabilityExecutorResolver.Create`, which rejects duplicate
`CapabilityId` mappings deterministically.

## Provider Edge

The Cloudflare Worker adapter is a narrow provider-edge adapter that:

- receives raw image bytes from the Lunar backend;
- authenticates via Bearer service token;
- validates Content-Type is an image media type;
- enforces a 20 MB input size limit (Cloudflare Images binding limit);
- calls the Cloudflare Images binding with `segment: "foreground"`;
- returns transparent PNG bytes;
- does not return raw provider exception messages to callers.

The Worker does not know about Lunar Assets, Artifacts,
WorkflowExecutions, or repositories.

The Infrastructure client also enforces the 20 MB limit before
sending, returning a `Rejected` failure kind for oversized inputs.

### Worker secret configuration

The service token is configured as a Wrangler secret, never as a
plaintext `vars` entry:

```bash
npx wrangler secret put LUNAR_FOREGROUND_ISOLATION_TOKEN
```

The `wrangler.jsonc` contains only the Images binding and non-secret
configuration. The token does not appear in `vars`.

If the secret is missing or empty at runtime, the Worker returns a
bounded `503 service_not_configured` response. This is a service
misconfiguration, not invalid caller authentication.

### Provider error handling

The Worker preserves the Cloudflare Images binding HTTP failure status.
A binding 4xx/5xx response is returned to the caller with the same
status class and a bounded JSON body — the raw provider body is never
forwarded. If the binding throws an exception, the Worker returns
`502 provider_error`.

```text
binding 4xx → same 4xx status, application/json, provider_error
binding 5xx → same 5xx status, application/json, provider_error
binding throw → 502, application/json, provider_error
binding success → 200, image/png, no-store
```

The bounded Worker error response:

```text
code = provider_error
message = "Foreground isolation provider failed."
```

Raw provider/runtime exception messages are not returned across the
service boundary.

## Configuration

```json
{
  "CloudflareForegroundIsolation": {
    "Endpoint": "",
    "ServiceToken": "",
    "RequestTimeout": "00:02:00"
  }
}
```

Committed `appsettings.json` truthfully represents "not configured"
with empty endpoint and token. The configuration is validated at
startup via `ValidateOnStart()`. The application will not start if:

- Endpoint is missing, not an absolute URI, or not HTTPS;
- ServiceToken is missing;
- RequestTimeout is not strictly positive.

Local development requires User Secrets:

```bash
cd backend/src/Lunar.Api
dotnet user-secrets set "CloudflareForegroundIsolation:Endpoint" "https://your-deployed-worker.example.com/"
dotnet user-secrets set "CloudflareForegroundIsolation:ServiceToken" "<shared-secret>"
```

## Supported Input Media Types

- `image/jpeg`
- `image/png`
- `image/webp`
- `image/gif`

Unsupported media types return `UnsupportedArtifactContent`
(HTTP 422).

## Observability

The foreground-isolation path emits the same BCL-native telemetry as
text-to-image generation:

- `ActivitySource`/`Activity` with `lunar.operation.outcome`,
  `lunar.failure.stage`, `lunar.asset.id`, `lunar.artifact.id` (source),
  `lunar.artifact.derived_id` (derived output), `lunar.workflow.execution.id` tags;
- `Meter` provider request duration, request count, output size;
- structured `ILogger<T>` logs with correlation;

The `lunar.artifact.id` tag remains stable as the source Artifact identity
throughout the operation. The derived Artifact identity is recorded separately
under `lunar.artifact.derived_id` on success. This prevents one tag from
changing semantic meaning during a single Activity.

The `DurationMs` log field reports actual elapsed milliseconds via
`Stopwatch.GetElapsedTime`, not a raw `Stopwatch.GetTimestamp()` counter value.
- image bytes are never logged or placed in telemetry tags;
- service tokens, Authorization headers, and local filesystem paths
  are never logged or placed in telemetry tags;
- provider exception messages are not returned to callers or logged
  in operational telemetry.
