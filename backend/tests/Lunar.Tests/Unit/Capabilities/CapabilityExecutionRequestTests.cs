using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Tests.Unit.Capabilities;

public class CapabilityExecutionRequestTests
{
    private static readonly CapabilityId ValidCapabilityId = CapabilityId.New();
    private static readonly AssetId ValidAssetId = AssetId.New();
    private static readonly WorkflowExecutionId ValidExecutionId = WorkflowExecutionId.New();
    private static readonly WorkflowDefinitionId ValidDefinitionId = WorkflowDefinitionId.New();
    private static readonly CapabilityExecutionInput ValidInput = new TextPromptInput("Generate a dark fantasy raven shrine.");


    [Fact]
    public void Constructor_ValidValues_ShouldPreserveExactValues()
    {
        var request = new CapabilityExecutionRequest(
            ValidCapabilityId,
            ValidAssetId,
            ValidExecutionId,
            ValidDefinitionId,
            3,
            2,
            ValidInput);

        Assert.Equal(ValidCapabilityId, request.CapabilityId);
        Assert.Equal(ValidAssetId, request.AssetId);
        Assert.Equal(ValidExecutionId, request.WorkflowExecutionId);
        Assert.Equal(ValidDefinitionId, request.WorkflowDefinitionId);
        Assert.Equal(3, request.WorkflowDefinitionVersion);
        Assert.Equal(2, request.StepPosition);
        Assert.Same(ValidInput, request.Input);
    }


    [Fact]
    public void Constructor_EmptyCapabilityId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new CapabilityExecutionRequest(
                new CapabilityId(Guid.Empty),
                ValidAssetId,
                ValidExecutionId,
                ValidDefinitionId,
                1,
                1,
                ValidInput));
    }


    [Fact]
    public void Constructor_EmptyAssetId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new CapabilityExecutionRequest(
                ValidCapabilityId,
                new AssetId(Guid.Empty),
                ValidExecutionId,
                ValidDefinitionId,
                1,
                1,
                ValidInput));
    }


    [Fact]
    public void Constructor_EmptyWorkflowExecutionId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new CapabilityExecutionRequest(
                ValidCapabilityId,
                ValidAssetId,
                new WorkflowExecutionId(Guid.Empty),
                ValidDefinitionId,
                1,
                1,
                ValidInput));
    }


    [Fact]
    public void Constructor_EmptyWorkflowDefinitionId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new CapabilityExecutionRequest(
                ValidCapabilityId,
                ValidAssetId,
                ValidExecutionId,
                new WorkflowDefinitionId(Guid.Empty),
                1,
                1,
                ValidInput));
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_VersionLessThanOne_ShouldThrow(int version)
    {
        Assert.Throws<ArgumentException>(() =>
            new CapabilityExecutionRequest(
                ValidCapabilityId,
                ValidAssetId,
                ValidExecutionId,
                ValidDefinitionId,
                version,
                1,
                ValidInput));
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_StepPositionLessThanOne_ShouldThrow(int stepPosition)
    {
        Assert.Throws<ArgumentException>(() =>
            new CapabilityExecutionRequest(
                ValidCapabilityId,
                ValidAssetId,
                ValidExecutionId,
                ValidDefinitionId,
                1,
                stepPosition,
                ValidInput));
    }


    [Fact]
    public void Constructor_NullInput_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CapabilityExecutionRequest(
                ValidCapabilityId,
                ValidAssetId,
                ValidExecutionId,
                ValidDefinitionId,
                1,
                1,
                null!));
    }
}
