using Lunar.Application.Errors;

namespace Lunar.Application;

/// <summary>
/// Maps <see cref="ApplicationError"/> subtypes to controlled, low-cardinality
/// failure-stage strings for observability. This is a private diagnostic
/// classification, not a domain concept.
/// </summary>
public static class FailureStageClassifier
{
    /// <summary>
    /// Returns a (stage, kind) tuple where kind is null when not applicable.
    /// </summary>
    public static (string Stage, string? Kind) Classify(ApplicationError error)
    {
        return error switch
        {
            AssetNotFound => (ApplicationTelemetry.StageAssetValidation, null),
            AssetPersistenceFailed => (ApplicationTelemetry.StageAssetValidation, null),

            WorkflowDefinitionNotFound => (ApplicationTelemetry.StageWorkflowPrevalidation, null),
            WorkflowStepNotFound => (ApplicationTelemetry.StageWorkflowPrevalidation, null),

            CapabilityExecutorNotFound => (ApplicationTelemetry.StageCapabilityExecution, null),

            WorkflowExecutionPersistenceFailed => (ApplicationTelemetry.StageWorkflowExecutionCreation, null),

            GenerationInputPersistenceFailed => (ApplicationTelemetry.StageGenerationInputPersistence, null),

            WorkflowExecutionCannotStart => (ApplicationTelemetry.StageWorkflowExecutionStart, null),
            WorkflowExecutionConcurrencyConflict => (ApplicationTelemetry.StageWorkflowExecutionStart, null),

            WorkflowStepExecutionFailed stepFailed =>
                (ApplicationTelemetry.StageCapabilityExecution, stepFailed.Kind.ToString()),

            ArtifactContentPersistenceFailed => (ApplicationTelemetry.StageArtifactContentPersistence, null),
            ArtifactPersistenceFailed => (ApplicationTelemetry.StageArtifactMetadataPersistence, null),

            ArtifactNotFound => (ApplicationTelemetry.StageApplication, null),
            ArtifactContentNotFound => (ApplicationTelemetry.StageApplication, null),

            _ => (ApplicationTelemetry.StageApplication, null)
        };
    }
}
