# ADR-005 - Controlled Observability Foundation



## Status



Accepted



## Context



Lunar's first product loop, asset generation gallery, and workspace
identity slices are implemented. Raw framework logs such as:

```text
Start processing HTTP request POST https://api.cloudflare.com/...
Sending HTTP request POST https://api.cloudflare.com/...
Received HTTP response headers after 6691 ms - 200
End processing HTTP request after 6700 ms - 200
```

do not explain Lunar's own operation and expose the Cloudflare account
identifier in the request URL.

Lunar needs to be diagnosable without building a full observability
platform. The desired diagnostic questions include:

- Did the request reach Lunar?
- Which Asset did it belong to?
- Which `WorkflowExecution` was created?
- Which workflow step was running?
- Did failure occur before the provider call?
- Did the provider fail?
- Did provider execution succeed but content persistence fail?
- Did metadata persistence fail?
- How long did each meaningful stage take?
- Which Artifact was eventually produced?
- Can related log lines be correlated?
- Was cancellation involved?
- Was an expected Application failure returned or did an unexpected
  exception occur?



## Decision



Add three complementary, BCL-native, OpenTelemetry-compatible
observability signals without introducing any telemetry backend,
exporter, SDK, or third-party logging framework:

1. **Activities** (`System.Diagnostics.ActivitySource`) — meaningful
   spans around product operations and stages. Future OpenTelemetry
   SDK/exporter compatibility is preserved by using standard BCL
   types. No OTel SDK is configured.

2. **Metrics** (`System.Diagnostics.Metrics.Meter`) — counters and
   histograms for meaningful operations. Metric tags are bounded to
   low-cardinality values (outcome, failure stage, failure kind,
   provider name, model). No IDs, prompt text, or arbitrary exception
   messages appear in metric tags.

3. **Structured logs** (`ILogger<T>`) — correlatable through Activity
   `TraceId` / `SpanId` via a custom logging scope. Useful at
   Information level without logging every method invocation. Debug
   details only where appropriate.

### ActivitySource names

- `Lunar.Application` — Application-layer semantic operations
  (generation, workflow generate, execution create/start, step
  execute, capability execute, content/metadata persist, content get,
  asset artifacts list).
- `Lunar.Infrastructure` — Infrastructure-layer operations
  (provider request, content store read/write).

### Meter names

- `Lunar.Application` — Application-layer metrics (generation
  attempts, generation duration, capability execution duration,
  artifact content persistence duration).
- `Lunar.Infrastructure` — Infrastructure-layer metrics (provider
  requests, provider request duration, provider output size).

### Metric cardinality policy

Metric tags are bounded to a fixed set of values:

- `outcome`: `success`, `failure`, `cancelled`
- `failure_stage`: one of the defined `Stage*` constants
- `failure_kind`: provider failure kind enum string
- `provider`: `cloudflare_workers_ai`
- `model`: bounded model identifier

The following must never appear in metric tags:

- Asset IDs
- Workflow execution IDs
- Artifact IDs
- Prompt text
- Cloudflare account IDs
- Request URLs
- Raw exception messages (exception.type is allowed)
- Raw provider response bodies
- Trace IDs / Span IDs

### Logging-level policy

- `Information`: generation started, generation completed, provider
  generation completed.
- `Warning`: generation failed (expected Application Result failure),
  generation cancelled, provider failure, compensation cleanup.
- `Error`: generation crashed (unexpected exception, logged exactly
  once at the `GenerateDefaultArtifactService` boundary, then
  rethrown).
- `Debug`: content store write/read duration and size (no filesystem
  paths).

### Logging ownership

The product Application use-case boundary
(`GenerateDefaultArtifactService`) owns all generation lifecycle logs:

```text
Generation started   -> Information
Generation completed -> Information
Generation failed    -> Warning (expected Result failure)
Generation crashed   -> Error (unexpected exception, logged once)
Generation cancelled -> Warning
```

`GenerationEndpoints` must not emit duplicate generation lifecycle
logs. It performs only transport validation and error mapping.

The provider executor (`CloudflareWorkersAiTextToImageExecutor`) owns
provider-specific logs:

```text
Provider generation completed -> Information
Provider failure              -> Warning
```

For an unexpected technical exception in the provider, the executor
marks the provider Activity Error and rethrows without emitting
another Error log. `GenerateDefaultArtifactService` logs the
exception exactly once.

Inner workflow/storage services use spans/tags for stage isolation
rather than repeating the same Warning/Error at each layer.

### TraceId/SpanId correlation

The ASP.NET Core logging framework is configured with
`ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId` via
`builder.Logging.Configure(...)`. The Simple console formatter is
configured with `IncludeScopes = true` via
`builder.Logging.AddSimpleConsole(...)`, which configures the default
(unnamed) `SimpleConsoleFormatterOptions` that the formatter actually
reads. When `Activity.Current` is active, `TraceId` and `SpanId` are
rendered in the console output as part of the logging scope.

`Activity.Current` is non-null during request processing because the
ASP.NET Core hosting layer creates a request `Activity` when logging
is enabled, even without an explicit `ActivityListener`. Lunar's
custom `ActivitySource` activities require a listener to materialize
and remain ready for a future OpenTelemetry listener/exporter.

### Cloudflare HttpClient noise reduction

The named Cloudflare `HttpClient` (`"CloudflareWorkersAi"`) is
configured in `appsettings.json` with the logging category
`System.Net.Http.HttpClient.CloudflareWorkersAi` set to `Warning`.
This suppresses the framework HTTP logging that previously exposed
the Cloudflare account URL at Information level.

Provider logs identify provider/model and outcome, but never:

- Account ID
- Full URL
- API token
- Response body
- Prompt text



## Consequences



- Lunar is diagnosable through structured logs, activities, and
  metrics without a telemetry backend.
- `TraceId`/`SpanId` correlation allows related log lines to be
  connected.
- Cloudflare account URLs no longer appear in normal Information-level
  logs.
- Prompt content is never logged or used in metric tags; only prompt
  length is acceptable in logs.
- The Activity/Meter instrumentation is compatible with a future
  OpenTelemetry SDK/exporter addition without code changes.
- No Serilog, NLog, OTel SDK, OTLP exporter, collector, dashboard, or
  telemetry database is introduced.



## Known limitations



- No OpenTelemetry SDK or OTLP exporter is configured.
- No trace backend, metrics backend, or dashboard exists.
- No historical telemetry retention.
- No cross-service collector.
- No provider cost accounting or usage tracking.
- No database query instrumentation.
- Activities and metrics are in-process only; no persistence.
