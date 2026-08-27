using Lunar.Api.Contracts;
using Lunar.Api.Http;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Microsoft.AspNetCore.Http;

namespace Lunar.Tests.Api.Http;

public class ApplicationErrorHttpMappingTests
{
    private static readonly AssetId TestAssetId = AssetId.New();
    private static readonly WorkflowDefinitionId TestDefinitionId = WorkflowDefinitionId.New();
    private static readonly WorkflowExecutionId TestExecutionId = WorkflowExecutionId.New();
    private static readonly ArtifactId TestArtifactId = ArtifactId.New();


    [Fact]
    public void Map_AssetNotFound_Returns404()
    {
        var result = ApplicationErrorHttpMapping.Map(new AssetNotFound(TestAssetId));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("asset_not_found", result.Response.Code);
    }

    [Fact]
    public void Map_WorkflowDefinitionNotFound_Returns404()
    {
        var result = ApplicationErrorHttpMapping.Map(
            new WorkflowDefinitionNotFound(TestDefinitionId, 1));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("workflow_definition_not_found", result.Response.Code);
    }

    [Fact]
    public void Map_WorkflowStepNotFound_Returns404()
    {
        var result = ApplicationErrorHttpMapping.Map(
            new WorkflowStepNotFound(TestDefinitionId, 1, 1));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("workflow_step_not_found", result.Response.Code);
    }

    [Fact]
    public void Map_WorkflowExecutionNotFound_Returns404()
    {
        var result = ApplicationErrorHttpMapping.Map(
            new WorkflowExecutionNotFound(TestExecutionId));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("workflow_execution_not_found", result.Response.Code);
    }

    [Fact]
    public void Map_ArtifactNotFound_Returns404()
    {
        var result = ApplicationErrorHttpMapping.Map(new ArtifactNotFound(TestArtifactId));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("artifact_not_found", result.Response.Code);
    }

    [Fact]
    public void Map_ArtifactContentNotFound_Returns404()
    {
        var result = ApplicationErrorHttpMapping.Map(new ArtifactContentNotFound(TestArtifactId));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("artifact_content_not_found", result.Response.Code);
    }

    [Fact]
    public void Map_WorkflowExecutionConcurrencyConflict_Returns409()
    {
        var result = ApplicationErrorHttpMapping.Map(
            new WorkflowExecutionConcurrencyConflict(TestExecutionId, 0));

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("workflow_execution_concurrency_conflict", result.Response.Code);
    }

    [Fact]
    public void Map_WorkflowExecutionCannotStart_Returns409()
    {
        var result = ApplicationErrorHttpMapping.Map(
            new WorkflowExecutionCannotStart(TestExecutionId, WorkflowExecutionStatus.Running));

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("workflow_execution_cannot_start", result.Response.Code);
    }

    [Fact]
    public void Map_WorkflowExecutionNotRunning_Returns409()
    {
        var result = ApplicationErrorHttpMapping.Map(
            new WorkflowExecutionNotRunning(TestExecutionId, WorkflowExecutionStatus.Created));

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("workflow_execution_not_running", result.Response.Code);
    }

    [Fact]
    public void Map_WorkflowExecutionPersistenceFailed_Returns503()
    {
        var result = ApplicationErrorHttpMapping.Map(
            new WorkflowExecutionPersistenceFailed(TestExecutionId));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("workflow_execution_persistence_failed", result.Response.Code);
    }

    [Fact]
    public void Map_ArtifactContentPersistenceFailed_Returns503()
    {
        var result = ApplicationErrorHttpMapping.Map(
            new ArtifactContentPersistenceFailed(TestArtifactId));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("artifact_content_persistence_failed", result.Response.Code);
    }

    [Fact]
    public void Map_ArtifactPersistenceFailed_Returns503()
    {
        var result = ApplicationErrorHttpMapping.Map(
            new ArtifactPersistenceFailed(TestArtifactId));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("artifact_persistence_failed", result.Response.Code);
    }

    [Theory]
    [InlineData(CapabilityExecutionFailureKind.Rejected, StatusCodes.Status422UnprocessableEntity, "step_rejected")]
    [InlineData(CapabilityExecutionFailureKind.AuthenticationFailed, StatusCodes.Status503ServiceUnavailable, "provider_authentication_failed")]
    [InlineData(CapabilityExecutionFailureKind.AccessDenied, StatusCodes.Status503ServiceUnavailable, "provider_access_denied")]
    [InlineData(CapabilityExecutionFailureKind.QuotaExhausted, StatusCodes.Status503ServiceUnavailable, "quota_exhausted")]
    [InlineData(CapabilityExecutionFailureKind.RateLimited, StatusCodes.Status429TooManyRequests, "rate_limited")]
    [InlineData(CapabilityExecutionFailureKind.PaidPlanRequired, StatusCodes.Status503ServiceUnavailable, "paid_plan_required")]
    [InlineData(CapabilityExecutionFailureKind.TimedOut, StatusCodes.Status504GatewayTimeout, "timed_out")]
    [InlineData(CapabilityExecutionFailureKind.TemporarilyUnavailable, StatusCodes.Status503ServiceUnavailable, "temporarily_unavailable")]
    [InlineData(CapabilityExecutionFailureKind.RemoteOutcomeUnknown, StatusCodes.Status502BadGateway, "remote_outcome_unknown")]
    [InlineData(CapabilityExecutionFailureKind.InvalidResponse, StatusCodes.Status502BadGateway, "invalid_response")]
    public void Map_WorkflowStepExecutionFailed_MapsEachKind(
        CapabilityExecutionFailureKind kind,
        int expectedStatusCode,
        string expectedCode)
    {
        var failure = new CapabilityExecutionFailure(kind);
        var error = new WorkflowStepExecutionFailed(TestExecutionId, 1, failure);

        var result = ApplicationErrorHttpMapping.Map(error);

        Assert.Equal(expectedStatusCode, result.StatusCode);
        Assert.Equal(expectedCode, result.Response.Code);
    }

    [Fact]
    public void Map_WorkflowStepExecutionFailed_WithRetryAfter_SetsRetryAfterSeconds()
    {
        var failure = new CapabilityExecutionFailure(
            CapabilityExecutionFailureKind.RateLimited,
            TimeSpan.FromSeconds(30));
        var error = new WorkflowStepExecutionFailed(TestExecutionId, 1, failure);

        var result = ApplicationErrorHttpMapping.Map(error);

        Assert.Equal(StatusCodes.Status429TooManyRequests, result.StatusCode);
        Assert.Equal(30, result.RetryAfterSeconds);
        Assert.Equal(30, result.Response.RetryAfterSeconds);
    }

    [Fact]
    public void Map_WorkflowStepExecutionFailed_WithoutRetryAfter_RetryAfterSecondsIsNull()
    {
        var failure = new CapabilityExecutionFailure(CapabilityExecutionFailureKind.QuotaExhausted);
        var error = new WorkflowStepExecutionFailed(TestExecutionId, 1, failure);

        var result = ApplicationErrorHttpMapping.Map(error);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Null(result.RetryAfterSeconds);
        Assert.Null(result.Response.RetryAfterSeconds);
    }

    [Fact]
    public void Map_UnknownError_Returns500()
    {
        var unknown = new UnknownError("Something unexpected.");

        var result = ApplicationErrorHttpMapping.Map(unknown);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal("internal_error", result.Response.Code);
    }


    private sealed record UnknownError(string Message) : ApplicationError(Message);
}
