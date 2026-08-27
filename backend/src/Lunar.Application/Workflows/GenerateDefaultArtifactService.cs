using Lunar.Application.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;

namespace Lunar.Application.Workflows;

public sealed class GenerateDefaultArtifactService
{
    private readonly GenerateArtifactService _generateArtifactService;
    private readonly GenerationWorkflowTarget _target;

    public GenerateDefaultArtifactService(
        GenerateArtifactService generateArtifactService,
        GenerationWorkflowTarget target)
    {
        ArgumentNullException.ThrowIfNull(generateArtifactService);
        ArgumentNullException.ThrowIfNull(target);

        _generateArtifactService = generateArtifactService;
        _target = target;
    }


    public async Task<Result<GeneratedArtifact>> GenerateAsync(
        AssetId assetId,
        CapabilityExecutionInput input,
        CancellationToken cancellationToken = default)
    {
        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
        }

        ArgumentNullException.ThrowIfNull(input);

        return await _generateArtifactService.GenerateAsync(
            assetId,
            _target.WorkflowDefinitionId,
            _target.Version,
            _target.StepPosition,
            input,
            cancellationToken);
    }
}
