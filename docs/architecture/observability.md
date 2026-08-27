# Observability Architecture

## Overview

Lunar uses three complementary, BCL-native, OpenTelemetry-compatible
observability signals to make the platform diagnosable without
introducing any telemetry backend, exporter, SDK, or third-party
logging framework.

```text
Structured logs (ILogger<T>)
    + Activities (ActivitySource)
    + Metrics (Meter)
    =
    Controlled Observability Foundation
```

## ActivitySource

### Names and purposes

| ActivitySource name         | Layer           | Purpose                                      |
| --------------------------- | --------------- | -------------------------------------------- |
| `Lunar.Application`         | Application     | Generation, workflow, capability, persistence, gallery operations |
| `Lunar.Infrastructure`      | Infrastructure  | Provider request, content store read/write   |

### Activity catalog

| Activity name                        | Source                  | Description                                      |
| ------------------------------------ | ----------------------- | ------------------------------------------------ |
| `lunar.generation`                   | Application             | Top-level generation operation                   |
| `lunar.workflow.generate`            | Application             | Workflow generation orchestration                |
| `lunar.workflow.execution.create`    | Application             | Workflow execution creation                      |
| `lunar.workflow.execution.start`     | Application             | Workflow execution start                         |
| `lunar.workflow.step.execute`        | Application             | Workflow step execution                          |
| `lunar.capability.execute`           | Application             | Capability execution                             |
| `lunar.artifact.content.persist`     | Application             | Artifact content persistence                     |
| `lunar.artifact.metadata.persist`    | Application             | Artifact metadata persistence                    |
| `lunar.artifact.content.get`         | Application             | Artifact content retrieval                       |
| `lunar.asset.artifacts.list`         | Application             | Asset artifacts list                             |
| `lunar.provider.request`             | Infrastructure          | Provider HTTP request                            |
| `lunar.content_store.write`          | Infrastructure          | Content store write                              |
| `lunar.content_store.read`           | Infrastructure          | Content store read                               |

### Span hierarchy

```text
lunar.generation
  └─ lunar.workflow.generate
       ├─ lunar.workflow.execution.create
       ├─ lunar.workflow.execution.start
       └─ lunar.workflow.step.execute
            └─ lunar.capability.execute
                 └─ lunar.provider.request (Infrastructure)
            ├─ lunar.artifact.content.persist
            │    └─ lunar.content_store.write (Infrastructure)
            └─ lunar.artifact.metadata.persist
```

### Trace tag names

| Tag name                          | Used by         | Purpose                          |
| --------------------------------- | --------------- | -------------------------------- |
| `lunar.asset.id`                  | Application     | Asset identifier (logs/traces)   |
| `lunar.artifact.id`               | Application     | Artifact identifier              |
| `lunar.workflow.execution.id`     | Application     | Workflow execution identifier    |
| `lunar.workflow.definition.id`    | Application     | Workflow definition identifier   |
| `lunar.workflow.definition.version` | Application   | Workflow definition version      |
| `lunar.workflow.step.position`    | Application     | Step position                    |
| `lunar.capability.id`             | Application     | Capability identifier            |
| `lunar.operation.outcome`         | Application     | success/failure/cancelled        |
| `lunar.failure.stage`             | Application     | Failure stage classification     |
| `lunar.failure.kind`              | Application     | Provider failure kind            |
| `lunar.provider.name`             | Infrastructure  | Provider name                    |
| `lunar.provider.model`            | Infrastructure  | Provider model                   |
| `lunar.provider.http_status`      | Infrastructure  | HTTP status code                 |
| `lunar.content.media_type`        | Both            | Content media type               |
| `lunar.content.size_bytes`        | Both            | Content size in bytes            |
| `lunar.artifact.count`            | Application     | Artifact count for list          |

## Meter

### Names and purposes

| Meter name               | Layer           | Purpose                              |
| ------------------------ | --------------- | ------------------------------------ |
| `Lunar.Application`      | Application     | Generation, capability, persistence  |
| `Lunar.Infrastructure`   | Infrastructure  | Provider, content store              |

### Metric instruments

| Instrument name                                  | Type       | Unit  | Source         | Description                          |
| ------------------------------------------------ | ---------- | ----- | -------------- | ------------------------------------ |
| `lunar.generation.attempts`                      | Counter    | {attempt} | Application | Generation attempts by outcome       |
| `lunar.generation.duration`                      | Histogram  | ms    | Application    | Generation duration by outcome       |
| `lunar.capability.execution.duration`            | Histogram  | ms    | Application    | Capability execution duration        |
| `lunar.artifact.content.persistence.duration`    | Histogram  | ms    | Application    | Content persistence duration         |
| `lunar.provider.requests`                        | Counter    | {request} | Infrastructure | Provider requests by outcome       |
| `lunar.provider.request.duration`                | Histogram  | ms    | Infrastructure | Provider request duration            |
| `lunar.provider.output.size`                     | Histogram  | By    | Infrastructure | Provider output size                 |

### Metric tags (bounded)

| Tag name       | Values                                    |
| -------------- | ----------------------------------------- |
| `outcome`      | `success`, `failure`, `cancelled`         |
| `failure_stage` | Defined `Stage*` constants                |
| `failure_kind`  | Provider failure kind enum string         |
| `provider`     | `cloudflare_workers_ai`                   |
| `model`        | Bounded model identifier                  |

### Cardinality policy

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

## Structured logs

### Event catalog

| Event                          | Level        | Owner                              | When                                  |
| ------------------------------ | ------------ | ---------------------------------- | ------------------------------------- |
| Generation started             | Information  | GenerateDefaultArtifactService     | Generation begins                     |
| Generation completed           | Information  | GenerateDefaultArtifactService     | Generation succeeds                   |
| Generation failed              | Warning      | GenerateDefaultArtifactService     | Expected Application failure          |
| Generation crashed             | Error        | GenerateDefaultArtifactService     | Unexpected exception (logged once)    |
| Generation cancelled           | Warning      | GenerateDefaultArtifactService     | Operation cancelled                   |
| Provider generation completed  | Information  | CloudflareWorkersAiTextToImageExecutor | Provider call succeeds             |
| Provider failure               | Warning      | CloudflareWorkersAiTextToImageExecutor | Provider returns failure           |
| Content persistence failed     | Warning      | ExecuteWorkflowStepService         | Content store rejects                 |
| Metadata persistence failed    | Warning      | ExecuteWorkflowStepService         | Metadata store rejects                |

### Logging ownership

`GenerateDefaultArtifactService` owns all generation lifecycle logs.
`GenerationEndpoints` performs only transport validation and error
mapping — it must not emit duplicate generation lifecycle logs.

The provider executor owns provider-specific logs but does not emit
an Error log for unexpected exceptions; it marks the Activity Error
and rethrows, letting `GenerateDefaultArtifactService` log the
exception exactly once.

### Log fields

| Field                  | Purpose                          | Privacy     |
| ---------------------- | -------------------------------- | ----------- |
| `assetId`              | Asset correlation                | Safe        |
| `workflowExecutionId`  | Execution correlation            | Safe        |
| `artifactId`           | Artifact correlation             | Safe        |
| `stepPosition`         | Step identification              | Safe        |
| `stage`                | Failure stage                    | Safe        |
| `errorType`            | Error type name                  | Safe        |
| `durationMs`           | Duration                         | Safe        |
| `provider`             | Provider name                    | Safe        |
| `model`                | Model name                       | Safe        |
| `httpStatus`           | HTTP status code                 | Safe        |
| `promptLength`         | Prompt length (not content)      | Safe        |
| `TraceId`/`SpanId`     | Activity correlation             | Safe        |

### Prompt privacy

Prompt content is never logged. Only `promptLength` is acceptable in
logs. Prompt content must not appear in:

- Log messages
- Log properties/state
- Activity tags
- Metric tags

### TraceId/SpanId correlation

The ASP.NET Core logging framework is configured with
`ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId` via
`builder.Logging.Configure(...)`. The Simple console formatter is
configured with `IncludeScopes = true` via
`builder.Services.Configure<SimpleConsoleFormatterOptions>(
ConsoleFormatterNames.Simple, ...)`. When `Activity.Current` is
active, `TraceId` and `SpanId` are rendered in the console output as
part of the logging scope.

## Successful generation log shape

```text
Generation started
traceId=...
assetId=...
promptLength=...

Provider generation completed
traceId=...
workflowExecutionId=...
stepPosition=1
provider=cloudflare_workers_ai
model=@cf/black-forest-labs/flux-1-schnell
durationMs=...
httpStatus=200

Generation completed
traceId=...
assetId=...
workflowExecutionId=...
artifactId=...
durationMs=...
```

## Failure log shape

```text
Generation failed
traceId=...
assetId=...
workflowExecutionId=...
stage=artifact_content_persistence
errorType=ArtifactContentPersistenceFailed
durationMs=...
```

## Provider behavior and privacy boundaries

Provider logs identify provider/model and outcome, but never:

- Account ID
- Full URL
- API token
- Response body
- Prompt text

The Cloudflare `HttpClient` is configured with framework HTTP logging
categories lowered to suppress the account URL noise at Information
level.

## Cloudflare HttpClient noise reduction

The named Cloudflare `HttpClient` (`"CloudflareWorkersAi"`) is
configured in `appsettings.json` with the logging category
`System.Net.Http.HttpClient.CloudflareWorkersAi` set to `Warning`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "System.Net.Http.HttpClient.CloudflareWorkersAi": "Warning"
    }
  }
}
```

This suppresses the `Start processing HTTP request`, `Sending HTTP
request`, `Received HTTP response headers`, and `End processing HTTP
request` messages that previously exposed the Cloudflare account URL.

### Filesystem path telemetry

`LocalFileArtifactContentStore` telemetry includes only:

```text
ArtifactId
operation=read|write
DurationMs
SizeBytes
Outcome
```

Filesystem paths (root path, artifact directory, content.bin,
metadata.json, temp directory) must never appear in telemetry, even
at Debug level. Technical exceptions may naturally contain paths when
they escape; paths are not copied into structured telemetry
properties.

### Exception telemetry privacy

For unexpected exceptions, Activity telemetry uses only controlled values:

```text
exception.type   (e.g. System.InvalidOperationException)
outcome=failure
```

Raw `Exception.Message` is never stored in Activity tags or status
descriptions. The exception object is logged exactly once at the
product Application boundary (`GenerateDefaultArtifactService`) via
`ILogger.LogError`, then rethrown.

Provider response bodies are never stored in logs, Activity tags, or
metric tags. Provider telemetry tests exercise the real
`CloudflareWorkersAiTextToImageExecutor` offline using a fake HTTP
handler — no test double manually emits Infrastructure telemetry.

## Current limitations

- No OpenTelemetry SDK or OTLP exporter is configured.
- No trace backend, metrics backend, or dashboard exists.
- No historical telemetry retention.
- No cross-service collector.
- No provider cost accounting or usage tracking.
- No database query instrumentation.
- Activities and metrics are in-process only; no persistence.
- No frontend debug panel or trace viewer.
