using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Api.Bootstrap;

public static class FirstProductLoopWorkflowBootstrap
{
    public static readonly WorkflowDefinitionId TextToImageWorkflowDefinitionId =
        new(new Guid("01a042eb-334e-7ff7-a6a5-38f2f3235209"));

    public static readonly CapabilityId TextToImageCapabilityId =
        new(new Guid("01a042eb-3357-72ac-9f58-5c2531b4a346"));

    public const int WorkflowVersion = 1;
    public const int StepPosition = 1;

    public const string WorkflowName = "Text to Image";


    public static async Task EnsureWorkflowExistsAsync(
        IWorkflowDefinitionRepository repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var existing = await repository.GetAsync(
            TextToImageWorkflowDefinitionId,
            WorkflowVersion,
            cancellationToken);

        if (existing is not null)
        {
            if (!IsCompatible(existing))
            {
                throw new InvalidOperationException(
                    "An incompatible workflow definition already exists for the first product loop "
                    + $"({TextToImageWorkflowDefinitionId}, v{WorkflowVersion}).");
            }

            return;
        }

        var definition = CreateExpectedDefinition();

        var added = await repository.TryAddAsync(definition, cancellationToken);

        if (added)
        {
            return;
        }

        var confirmed = await repository.GetAsync(
            TextToImageWorkflowDefinitionId,
            WorkflowVersion,
            cancellationToken);

        if (confirmed is null)
        {
            throw new InvalidOperationException(
                "Failed to bootstrap the first product loop workflow definition.");
        }

        if (!IsCompatible(confirmed))
        {
            throw new InvalidOperationException(
                "An incompatible workflow definition was inserted concurrently for the first product loop "
                + $"({TextToImageWorkflowDefinitionId}, v{WorkflowVersion}).");
        }
    }


    public static WorkflowDefinition CreateExpectedDefinition()
    {
        return new WorkflowDefinition(
            TextToImageWorkflowDefinitionId,
            WorkflowVersion,
            WorkflowName,
            new[] { new WorkflowStep(StepPosition, TextToImageCapabilityId) });
    }


    internal static bool IsCompatible(WorkflowDefinition definition)
    {
        if (definition.Id != TextToImageWorkflowDefinitionId)
        {
            return false;
        }

        if (definition.Version != WorkflowVersion)
        {
            return false;
        }

        if (!string.Equals(definition.Name, WorkflowName, StringComparison.Ordinal))
        {
            return false;
        }

        if (definition.Steps.Count != 1)
        {
            return false;
        }

        var step = definition.Steps[0];
        if (step.Position != StepPosition)
        {
            return false;
        }

        if (step.CapabilityId != TextToImageCapabilityId)
        {
            return false;
        }

        return true;
    }
}
