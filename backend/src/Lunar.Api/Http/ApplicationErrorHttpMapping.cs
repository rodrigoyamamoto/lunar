using Lunar.Application.Errors;
using Lunar.Core.Capabilities;

namespace Lunar.Api.Http;

public static class ApplicationErrorHttpMapping
{
    public static HttpErrorResult Map(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error switch
        {
            AssetNotFound => new HttpErrorResult(
                StatusCodes.Status404NotFound,
                new Contracts.ApiErrorResponse
                {
                    Code = "asset_not_found",
                    Message = error.Message
                }),

            WorkflowDefinitionNotFound => new HttpErrorResult(
                StatusCodes.Status404NotFound,
                new Contracts.ApiErrorResponse
                {
                    Code = "workflow_definition_not_found",
                    Message = error.Message
                }),

            WorkflowStepNotFound => new HttpErrorResult(
                StatusCodes.Status404NotFound,
                new Contracts.ApiErrorResponse
                {
                    Code = "workflow_step_not_found",
                    Message = error.Message
                }),

            WorkflowExecutionNotFound => new HttpErrorResult(
                StatusCodes.Status404NotFound,
                new Contracts.ApiErrorResponse
                {
                    Code = "workflow_execution_not_found",
                    Message = error.Message
                }),

            ArtifactNotFound => new HttpErrorResult(
                StatusCodes.Status404NotFound,
                new Contracts.ApiErrorResponse
                {
                    Code = "artifact_not_found",
                    Message = error.Message
                }),

            ArtifactContentNotFound => new HttpErrorResult(
                StatusCodes.Status404NotFound,
                new Contracts.ApiErrorResponse
                {
                    Code = "artifact_content_not_found",
                    Message = error.Message
                }),

            WorkflowExecutionConcurrencyConflict => new HttpErrorResult(
                StatusCodes.Status409Conflict,
                new Contracts.ApiErrorResponse
                {
                    Code = "workflow_execution_concurrency_conflict",
                    Message = error.Message
                }),

            WorkflowExecutionCannotStart => new HttpErrorResult(
                StatusCodes.Status409Conflict,
                new Contracts.ApiErrorResponse
                {
                    Code = "workflow_execution_cannot_start",
                    Message = error.Message
                }),

            WorkflowExecutionNotRunning => new HttpErrorResult(
                StatusCodes.Status409Conflict,
                new Contracts.ApiErrorResponse
                {
                    Code = "workflow_execution_not_running",
                    Message = error.Message
                }),

            WorkflowExecutionPersistenceFailed => new HttpErrorResult(
                StatusCodes.Status503ServiceUnavailable,
                new Contracts.ApiErrorResponse
                {
                    Code = "workflow_execution_persistence_failed",
                    Message = error.Message
                }),

            ArtifactContentPersistenceFailed => new HttpErrorResult(
                StatusCodes.Status503ServiceUnavailable,
                new Contracts.ApiErrorResponse
                {
                    Code = "artifact_content_persistence_failed",
                    Message = error.Message
                }),

            ArtifactPersistenceFailed => new HttpErrorResult(
                StatusCodes.Status503ServiceUnavailable,
                new Contracts.ApiErrorResponse
                {
                    Code = "artifact_persistence_failed",
                    Message = error.Message
                }),

            WorkflowStepExecutionFailed stepFailed => MapStepExecutionFailure(stepFailed),

            _ => new HttpErrorResult(
                StatusCodes.Status500InternalServerError,
                new Contracts.ApiErrorResponse
                {
                    Code = "internal_error",
                    Message = "An unexpected error occurred."
                })
        };
    }


    private static HttpErrorResult MapStepExecutionFailure(WorkflowStepExecutionFailed error)
    {
        var (statusCode, code) = error.Kind switch
        {
            CapabilityExecutionFailureKind.Rejected =>
                (StatusCodes.Status422UnprocessableEntity, "step_rejected"),

            CapabilityExecutionFailureKind.AuthenticationFailed =>
                (StatusCodes.Status503ServiceUnavailable, "provider_authentication_failed"),

            CapabilityExecutionFailureKind.AccessDenied =>
                (StatusCodes.Status503ServiceUnavailable, "provider_access_denied"),

            CapabilityExecutionFailureKind.QuotaExhausted =>
                (StatusCodes.Status503ServiceUnavailable, "quota_exhausted"),

            CapabilityExecutionFailureKind.RateLimited =>
                (StatusCodes.Status429TooManyRequests, "rate_limited"),

            CapabilityExecutionFailureKind.PaidPlanRequired =>
                (StatusCodes.Status503ServiceUnavailable, "paid_plan_required"),

            CapabilityExecutionFailureKind.TimedOut =>
                (StatusCodes.Status504GatewayTimeout, "timed_out"),

            CapabilityExecutionFailureKind.TemporarilyUnavailable =>
                (StatusCodes.Status503ServiceUnavailable, "temporarily_unavailable"),

            CapabilityExecutionFailureKind.RemoteOutcomeUnknown =>
                (StatusCodes.Status502BadGateway, "remote_outcome_unknown"),

            CapabilityExecutionFailureKind.InvalidResponse =>
                (StatusCodes.Status502BadGateway, "invalid_response"),

            _ =>
                (StatusCodes.Status502BadGateway, "unknown_step_failure")
        };

        var retryAfterSeconds = error.RetryAfter is { } retryAfter
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : (int?)null;

        return new HttpErrorResult(
            statusCode,
            new Contracts.ApiErrorResponse
            {
                Code = code,
                Message = error.Message,
                RetryAfterSeconds = retryAfterSeconds
            },
            retryAfterSeconds);
    }
}
