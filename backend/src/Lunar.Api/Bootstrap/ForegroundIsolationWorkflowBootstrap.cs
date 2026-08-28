using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Api.Bootstrap;

/// <summary>
/// Bootstrap for the built-in foreground-isolation workflow definition.
/// Uses stable UUID v7 identities and the same strict compatibility
/// semantics as <see cref="FirstProductLoopWorkflowBootstrap"/>.
/// </summary>
public static class ForegroundIsolationWorkflowBootstrap
{
    public static readonly WorkflowDefinitionId ForegroundIsolationWorkflowDefinitionId =
        new(new Guid("01a042eb-334e-7ff8-b1b2-48e7c3a9d201"));

    public static readonly CapabilityId ForegroundIsolationCapabilityId =
        new(new Guid("01a042eb-3357-72ad-a3c1-6e4f52b8c102"));

    public const int WorkflowVersion = 1;
    public const int StepPosition = 1;

    public const string WorkflowName = "Foreground Isolation";


    public static async Task EnsureWorkflowExistsAsync(
        IWorkflowDefinitionRepository repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var existing = await repository.GetAsync(
            ForegroundIsolationWorkflowDefinitionId,
            WorkflowVersion,
            cancellationToken);

        if (existing is not null)
        {
            if (!IsCompatible(existing))
            {
                throw new InvalidOperationException(
                    "An incompatible workflow definition already exists for foreground isolation "
                    + $"({ForegroundIsolationWorkflowDefinitionId}, v{WorkflowVersion}).");
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
            ForegroundIsolationWorkflowDefinitionId,
            WorkflowVersion,
            cancellationToken);

        if (confirmed is null)
        {
            throw new InvalidOperationException(
                "Failed to bootstrap the foreground-isolation workflow definition.");
        }

        if (!IsCompatible(confirmed))
        {
            throw new InvalidOperationException(
                "An incompatible workflow definition was inserted concurrently for foreground isolation "
                + $"({ForegroundIsolationWorkflowDefinitionId}, v{WorkflowVersion}).");
        }
    }


    public static WorkflowDefinition CreateExpectedDefinition()
    {
        return new WorkflowDefinition(
            ForegroundIsolationWorkflowDefinitionId,
            WorkflowVersion,
            WorkflowName,
            new[] { new WorkflowStep(StepPosition, ForegroundIsolationCapabilityId) });
    }


    internal static bool IsCompatible(WorkflowDefinition definition)
    {
        if (definition.Id != ForegroundIsolationWorkflowDefinitionId)
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

        if (step.CapabilityId != ForegroundIsolationCapabilityId)
        {
            return false;
        }

        return true;
    }
}
