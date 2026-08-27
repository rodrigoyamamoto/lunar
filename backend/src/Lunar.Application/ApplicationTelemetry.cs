using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Lunar.Application;

/// <summary>
/// Owns the long-lived reusable <see cref="ActivitySource"/> and
/// <see cref="Meter"/> for the Lunar Application layer. These are
/// OpenTelemetry-compatible BCL instrumentation objects. No OpenTelemetry
/// SDK or exporter is configured yet.
/// </summary>
public static class ApplicationTelemetry
{
    /// <summary>
    /// The ActivitySource name for Application-layer semantic operations.
    /// </summary>
    public const string ActivitySourceName = "Lunar.Application";

    /// <summary>
    /// The Meter name for Application-layer metrics.
    /// </summary>
    public const string MeterName = "Lunar.Application";

    /// <summary>
    /// Long-lived reusable ActivitySource. Do not dispose or recreate per
    /// operation. When no listener is subscribed, StartActivity returns null
    /// and instrumentation is effectively free.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Long-lived reusable Meter. Do not dispose or recreate per operation.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName, "1.0.0");

    // --- Activity names ---

    public const string GenerationActivityName = "lunar.generation";
    public const string WorkflowGenerateActivityName = "lunar.workflow.generate";
    public const string WorkflowExecutionCreateActivityName = "lunar.workflow.execution.create";
    public const string WorkflowExecutionStartActivityName = "lunar.workflow.execution.start";
    public const string WorkflowStepExecuteActivityName = "lunar.workflow.step.execute";
    public const string CapabilityExecuteActivityName = "lunar.capability.execute";
    public const string ArtifactContentPersistActivityName = "lunar.artifact.content.persist";
    public const string ArtifactMetadataPersistActivityName = "lunar.artifact.metadata.persist";
    public const string ArtifactContentGetActivityName = "lunar.artifact.content.get";
    public const string AssetArtifactsListActivityName = "lunar.asset.artifacts.list";

    // --- Trace tag names ---

    public const string AssetIdTag = "lunar.asset.id";
    public const string ArtifactIdTag = "lunar.artifact.id";
    public const string WorkflowExecutionIdTag = "lunar.workflow.execution.id";
    public const string WorkflowDefinitionIdTag = "lunar.workflow.definition.id";
    public const string WorkflowDefinitionVersionTag = "lunar.workflow.definition.version";
    public const string WorkflowStepPositionTag = "lunar.workflow.step.position";
    public const string CapabilityIdTag = "lunar.capability.id";
    public const string OperationOutcomeTag = "lunar.operation.outcome";
    public const string FailureStageTag = "lunar.failure.stage";
    public const string FailureKindTag = "lunar.failure.kind";
    public const string ContentMediaTypeTag = "lunar.content.media_type";
    public const string ContentSizeBytesTag = "lunar.content.size_bytes";
    public const string ArtifactCountTag = "lunar.artifact.count";

    // --- Metric instruments ---

    public static Counter<long> GenerationAttempts { get; } =
        Meter.CreateCounter<long>("lunar.generation.attempts", unit: "{attempt}");

    public static Histogram<double> GenerationDuration { get; } =
        Meter.CreateHistogram<double>("lunar.generation.duration", unit: "ms");

    public static Histogram<double> CapabilityExecutionDuration { get; } =
        Meter.CreateHistogram<double>("lunar.capability.execution.duration", unit: "ms");

    public static Histogram<double> ArtifactContentPersistenceDuration { get; } =
        Meter.CreateHistogram<double>("lunar.artifact.content.persistence.duration", unit: "ms");

    // --- Metric tag names (low-cardinality only) ---

    public const string OutcomeTag = "outcome";
    public const string FailureStageMetricTag = "failure_stage";
    public const string FailureKindMetricTag = "failure_kind";

    // --- Outcome values ---

    public const string OutcomeSuccess = "success";
    public const string OutcomeFailure = "failure";
    public const string OutcomeCancelled = "cancelled";

    // --- Failure stage values ---

    public const string StageAssetValidation = "asset_validation";
    public const string StageWorkflowPrevalidation = "workflow_prevalidation";
    public const string StageWorkflowExecutionCreation = "workflow_execution_creation";
    public const string StageWorkflowExecutionStart = "workflow_execution_start";
    public const string StageCapabilityExecution = "capability_execution";
    public const string StageArtifactContentPersistence = "artifact_content_persistence";
    public const string StageArtifactMetadataPersistence = "artifact_metadata_persistence";
    public const string StageApplication = "application";
}
