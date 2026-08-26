# Cloudflare Workers AI Integration

## Overview

Lunar Asset Studio integrates with Cloudflare Workers AI as its first
concrete remote capability executor. The integration is an Infrastructure
implementation detail — no Cloudflare concept enters Core or Application.

## Current Configuration

- **Provider:** Cloudflare Workers AI
- **Model:** `@cf/black-forest-labs/flux-1-schnell` (FLUX.1 Schnell)
- **Output format:** JPEG (`image/jpeg`)
- **Transport:** REST API over HTTPS
- **Request body:** `{ "prompt": "<exact Lunar prompt>", "steps": 4 }`
- **Authentication:** Bearer token

## Architecture

```text
TextPromptInput
    ↓
ExecuteWorkflowStepService (Application)
    ↓
CapabilityExecutionRequest (Core)
    ↓
ICapabilityExecutor (Core port)
    ↓
CloudflareWorkersAiTextToImageExecutor (Infrastructure adapter)
    ↓
CloudflareWorkersAiClient (Infrastructure HTTP client)
    ↓
HttpClient (IHttpClientFactory-managed, redirects disabled)
    ↓
Cloudflare REST API
    ↓
FLUX.1 Schnell
    ↓
Cloudflare JSON/Base64 JPEG response
    ↓
CloudflareWorkersAiResponseParser (Infrastructure protocol parser)
    ↓
Infrastructure result (CloudflareImageGenerationResult)
    ↓
CloudflareWorkersAiTextToImageExecutor
    ↓
CapabilityExecutionSucceeded (Core outcome)
    ↓
CapabilityExecutionOutput (Core)
    ↓
Artifact metadata persistence
    ↓
ProducedArtifact (Application result)
```

## SOLID Responsibility Split

Each production type has one primary reason to change:

- **`CloudflareWorkersAiOptions`** changes when Cloudflare configuration
  binding shape changes. This is mutable configuration-binding data
  consumed by the .NET configuration binder.
- **`CloudflareWorkersAiConfiguration`** changes when validated runtime
  execution configuration changes. This is the immutable validated
  snapshot used by the client.
- **`CloudflareWorkersAiTextToImageExecutor`** changes when
  Lunar-to-Cloudflare text-to-image adaptation semantics change.
- **`CloudflareWorkersAiClient`** changes when Cloudflare HTTP transport
  behavior changes.
- **`CloudflareWorkersAiResponseParser`** changes when Cloudflare response
  protocol/envelope semantics change.

### Options vs Configuration

`CloudflareWorkersAiOptions` is a mutable class required by the .NET
configuration binder. It is **not** immutable and **not** described as
immutable. It exists only for framework binding.

`CloudflareWorkersAiConfiguration` is a sealed class with readonly
properties. It is constructed once from `CloudflareWorkersAiOptions` via
`CloudflareWorkersAiConfiguration.From(options)`, which performs all
validation. The client stores this immutable snapshot, not the mutable
options object.

### Executor responsibility

`CloudflareWorkersAiTextToImageExecutor` knows:

- `CapabilityExecutionRequest`;
- `TextPromptInput`;
- that this executor is text-to-image;
- how a successful Cloudflare image becomes `BinaryArtifactContent`;
- how a provider-neutral Lunar execution outcome is created;
- provider-neutral output metadata required by the current capability boundary.

It does **not** implement raw HTTP response parsing, deserialize Cloudflare
envelopes, inspect Cloudflare numeric error codes, parse `Retry-After`, or
manually build Authorization headers.

### Client responsibility

`CloudflareWorkersAiClient` knows:

- Cloudflare REST route shape;
- HTTP method (POST);
- bearer authentication;
- account configuration;
- request DTO serialization;
- request timeout;
- caller cancellation discrimination;
- response-body transport failure classification;
- local timeout classification;
- redirect policy (disabled);
- how to invoke the response parser.

It does not know `Artifact`, `IArtifactRepository`, `WorkflowExecution`,
`ProducedArtifact`, Application `Result<T>`, or UI messages.

The Client owns caller-vs-timeout discrimination because it owns the
original caller token and the linked timeout token. The Parser does not
decide this.

### Parser responsibility

`CloudflareWorkersAiResponseParser` knows:

- Cloudflare response envelope structure (unified DTO);
- Cloudflare error DTO shape;
- known Cloudflare numeric error codes;
- generic HTTP fallback classification;
- `Retry-After`;
- Base64 transport decoding;
- JPEG signature validation;
- response structural validity;
- HTTP 2xx + `success:false` envelope semantics.

It does not make HTTP requests, read configuration, construct Lunar
`Artifact`, persist anything, log secrets, decide caller-vs-timeout, or
retry. It lets `OperationCanceledException` propagate to the Client.

## Why No Generic Cloudflare Client Interface

There is no `ICloudflareWorkersAiClient` or
`ICloudflareWorkersAiResponseParser`. The concrete client is tested with a
fake `HttpMessageHandler`. The parser is a pure static class tested directly.
Dependency inversion is already provided at the Lunar capability boundary
(`ICapabilityExecutor`). Adding interfaces inside Infrastructure would
create indirection without a second implementation or consumer.

## HttpClientFactory Registration and Lifetimes

```text
Named HttpClient "CloudflareWorkersAi"
    → Timeout = InfiniteTimeSpan (linked CTS is authoritative)
    → Primary handler: SocketsHttpHandler with AllowAutoRedirect = false

CloudflareWorkersAiConfiguration
    → created once per client resolution via Configuration.From(options)

CloudflareWorkersAiClient
    → transient (factory creates per request)
    → receives HttpClient from IHttpClientFactory
    → receives immutable CloudflareWorkersAiConfiguration

CloudflareWorkersAiTextToImageExecutor
    → transient
    → receives CloudflareWorkersAiClient via DI

ICapabilityExecutor
    → transient
    → bound to CloudflareWorkersAiTextToImageExecutor
```

No singleton captures a factory-created `HttpClient`. The
`HttpClient.Timeout` is set to `InfiniteTimeSpan` because the linked
`CancellationTokenSource` applies the configured `RequestTimeout` per
operation. This avoids competing timeout sources.

## Redirect Policy

Automatic redirects are disabled (`AllowAutoRedirect = false` on the
primary `SocketsHttpHandler`). This is mandatory for a non-idempotent
generation POST — a hidden redirect follow would violate the one-POST
guarantee. Redirect responses (3xx) are classified as `InvalidResponse`.

## Configuration Model

### appsettings.json (safe, source-controlled)

```json
{
  "Cloudflare": {
    "BaseAddress": "https://api.cloudflare.com/",
    "RequestTimeout": "00:01:00",
    "TextToImageModelId": "@cf/black-forest-labs/flux-1-schnell",
    "TextToImageSteps": 4
  }
}
```

### User Secrets (machine-local, not committed)

```
Cloudflare:AccountId
Cloudflare:ApiToken
```

### Configuration vs Protocol Constants

Environment/deployment values (in configuration):
- `BaseAddress`
- `RequestTimeout`
- `AccountId`
- `ApiToken`
- `TextToImageModelId`
- `TextToImageSteps`

Protocol/adapter constants (in code):
- Cloudflare numeric error codes (3036, 3040, 5035, 3007, 3008)
- Authorization scheme "Bearer"
- JSON property names (DTO attributes)
- Output media type `image/jpeg` (guaranteed by FLUX.1 Schnell contract)
- HTTP method POST

`TextToImageModelId` and `TextToImageSteps` are in configuration but
documented as valid only for the currently supported FLUX.1 Schnell
text-to-image contract. Changing these to an incompatible model is
unsupported. There is no model registry and no claim of arbitrary model
replaceability.

### Options Validation (Single Boundary)

Validation occurs exactly once, in
`CloudflareWorkersAiConfiguration.From(options)`, called when the
`CloudflareWorkersAiClient` is constructed by the DI factory. There is no
duplicate validation in `Program.cs` or anywhere else.

Validation ensures:

- `BaseAddress` non-empty, absolute, HTTPS;
- `AccountId` non-empty/non-whitespace;
- `ApiToken` non-empty/non-whitespace;
- `RequestTimeout` strictly positive;
- `TextToImageModelId` non-empty/non-whitespace;
- `TextToImageSteps` between 1 and 8.

Validation error messages never include the `ApiToken` value.

## Execution Outcome Model

The executor returns `CapabilityExecutionOutcome` — either:

- `CapabilityExecutionSucceeded` (wrapping `CapabilityExecutionOutput`)
- `CapabilityExecutionFailed` (wrapping `CapabilityExecutionFailure`)

`CapabilityExecutionFailure` carries a provider-independent
`CapabilityExecutionFailureKind` and an optional `RetryAfter` duration.

The outcome family is closed: `CapabilityExecutionOutcome` has a
`private protected` constructor, so only `CapabilityExecutionSucceeded`
and `CapabilityExecutionFailed` (both in `Lunar.Core`) can subclass it.

`CapabilityExecutionFailure` rejects undefined enum values.

## Application Error Model

`WorkflowStepExecutionFailed` accepts a `CapabilityExecutionFailure`
directly — it does not tear apart `Kind` and `RetryAfter`. This reuses
the already-validated Core value and prevents invalid ApplicationError
state. It exposes `Kind` and `RetryAfter` as convenience properties
delegating to `Failure`.

## Failure Mapping

Cloudflare-specific error codes are translated at the Infrastructure
boundary. All errors in the Cloudflare `errors` array are scanned; the
first recognized code in provider error array order wins (deterministic
precedence).

### HTTP 2xx + success:false

Cloudflare envelope semantics are considered independently from HTTP
status. A response with HTTP 200 + `success:false` + recognized error
code maps according to the error code, not the HTTP status:

| Cloudflare Code | Lunar Kind |
|----------------|------------|
| 3036 | `QuotaExhausted` |
| 3040 | `TemporarilyUnavailable` |
| 5035 | `PaidPlanRequired` |
| 3007 | `TimedOut` |
| 3008 | `TimedOut` |

HTTP 200 + `success:false` + no interpretable errors → `InvalidResponse`.

### Generic HTTP status mapping

When no specific code is recognized, HTTP status is used:

| HTTP Status | Lunar Kind |
|-------------|------------|
| 401 | `AuthenticationFailed` |
| 403 | `AccessDenied` |
| 408 | `TimedOut` |
| 429 | `RateLimited` |
| 3xx | `InvalidResponse` |
| 400-499 | `Rejected` |
| 500-599 | `TemporarilyUnavailable` |

Transport failures (`HttpRequestException`) and local request timeouts
map to `RemoteOutcomeUnknown`.

Malformed/invalid provider responses map to `InvalidResponse`.

## Cancellation and Timeout Semantics

### Caller cancellation

Caller cancellation always propagates as `OperationCanceledException`. It
is never converted into `CapabilityExecutionFailed`. The executor checks
`cancellationToken.IsCancellationRequested` before dispatching. The
Client inspects the original caller token to distinguish caller
cancellation from local timeout.

### Local timeout (Lunar request timeout)

The Client creates a linked `CancellationTokenSource` combining the caller
token with `RequestTimeout`. If the linked token fires and the caller
token is not cancelled:

- **During `SendAsync`**: returns `RemoteOutcomeUnknown`
- **During success body read**: returns `RemoteOutcomeUnknown` (generation
  may have completed and quota consumed, but Lunar did not acquire the
  artifact)
- **During error body read**: falls back to HTTP status classification
  (a trustworthy non-success status was already received)

### Provider-declared timeout

If Cloudflare returns a known timeout/aborted response (codes 3007, 3008,
or HTTP 408), the parser returns `TimedOut`.

### Parser cancellation behavior

The Parser accepts a `CancellationToken` and flows it through every
internal method on the success path: `ParseAsync` →
`ClassifyByEnvelopeOrStatus` → `ValidateSuccessPayload`. The same token
used for `ReadFromJsonAsync` is the token checked before and after
Base64 decoding. The Parser lets `OperationCanceledException` propagate
to the Client. The Parser does not decide caller-vs-timeout — that is
transport responsibility owned by the Client.

### Base64 cancellation

`Convert.FromBase64String` is synchronous and cannot be interrupted
mid-operation. The Parser checks
`cancellationToken.ThrowIfCancellationRequested()` immediately before
decoding and immediately after decoding. This establishes the precise
supported semantic: cancellation can be observed before Base64 decode
and immediately after Base64 decode, but the synchronous decode
operation itself is not interruptible.

### Coverage

Cancellation/timeout semantics cover the entire HTTP lifecycle:

```text
SendAsync
+ response body read
+ JSON deserialization
+ Base64 decoding (observed before/after, not mid-decode)
```

## Response-Body Transport Failures

A response body can fail after headers are received (connection reset,
stream aborted, HTTP/2 stream error). The Client handles these:

- **HTTP success status + body transport failure** → `RemoteOutcomeUnknown`
  (generation may have completed, quota may have been consumed, Lunar did
  not acquire the image)
- **HTTP non-success status + body transport failure** → generic
  classification for the known HTTP status (e.g. 429 → `RateLimited`)

Handled exception types: `HttpRequestException`, `IOException`,
`OperationCanceledException` (caller vs timeout), `JsonException`,
`FormatException`. No broad `catch (Exception)`.

## Retry Safety

**No automatic retry of generation POST requests.**

The generation operation is an HTTP POST with non-idempotent effects. A
failure can occur after Cloudflare generated the image but before Lunar
received the response. A blind retry could produce duplicate generations
and consume the free daily Neuron allocation multiple times.

All error mapping tests assert exactly one provider request as an
anti-retry proof.

`RetryAfter` is information for a future orchestration layer. It is not
permission to retry automatically.

No Polly, no resilience handler with retry enabled, no manual retry loop,
no hedging handler.

## JPEG Validation

The parser validates the JPEG signature (`FF D8 FF`) before declaring
content as `image/jpeg`. Arbitrary decoded bytes without the JPEG signature
are rejected as `InvalidResponse`.

## Concurrency Safety

The executor and client hold no mutable request-specific instance state.
The `HttpClient` and `CloudflareWorkersAiConfiguration` are shared
immutable configuration. No concurrency semaphore, queue, or bounded job
concurrency is imposed.

## Security

- No actual API token or Account ID appears in source code, tests, or
  documentation.
- Test credentials are dummy values: `test-account`, `test-token`.
- `CloudflareWorkersAiOptions` is a sealed class (not a positional record)
  to prevent accidental secret rendering through default `ToString()`.
- `CloudflareWorkersAiConfiguration` is a sealed class (not a positional
  record) for the same reason. Its default `ToString()` returns only the
  type name.
- Exception messages do not include the token value.
- The `Authorization` header is never logged.
- The Cloudflare JSON response body is never logged.
- Validation error messages never include the `ApiToken` value.
- The `UserSecretsId` in `Lunar.Api.csproj` is a project-level identifier,
  not a secret.

## Cost Policy

Current Lunar development policy: **£0 external inference spend**.

- No automatic paid fallback.
- No provider fallback.
- No Workers Paid plan activation.
- No AI Gateway prepaid-credit integration.
- If Cloudflare reports free quota exhaustion, Lunar exposes a typed
  `QuotaExhausted` failure.

## Current Limitations

- The Cloudflare adapter does not own durable storage. It receives
  JSON/Base64 from the Cloudflare REST API, decodes it to
  `BinaryArtifactContent` in memory, and returns it to
  `ExecuteWorkflowStepService`. Durable content persistence is owned by
  Lunar through the provider-neutral `IArtifactContentStore` Core port;
  the first implementation is `LocalFileArtifactContentStore` (local
  filesystem). See `docs/architecture/artifact-content-storage.md`.
- Base64 remains a Cloudflare transport encoding only. Lunar stores
  decoded raw bytes, not Base64.
- The Cloudflare provider URL and model response are not durable
  ownership. Lunar ownership is established through `ArtifactId`.
- No download API yet. Retrieval is through `IArtifactContentStore`
  only; no HTTP endpoint exists.
- Cloud content stores (Cloudflare R2, Amazon S3, Azure Blob Storage)
  remain future work. The same `IArtifactContentStore` contract can be
  implemented by those providers without changing the Cloudflare
  adapter, `Artifact`, or `ExecuteWorkflowStepService`.
- No streaming, chunking, or multi-file bundles.
- No content hash or deduplication.
- No provider/model selection or routing.
- No fallback to other providers or models.
- No automatic retry.
- No asynchronous job execution or progress tracking.
- No public HTTP generation endpoint.
- No worker runtime.
- No step runtime state.
- No automatic workflow progression or completion.
- One fixed model (`@cf/black-forest-labs/flux-1-schnell`).
- One fixed step count (`steps = 4`).
- One fixed output format (`image/jpeg`).

## Testing

All tests are deterministic and offline. No test accesses the internet
or consumes Cloudflare Neurons. Tests use a fake `HttpMessageHandler` to
simulate Cloudflare responses. No skipped or live tests exist in the
committed suite.
