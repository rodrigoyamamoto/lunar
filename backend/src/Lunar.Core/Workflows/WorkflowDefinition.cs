using System.Collections.ObjectModel;
using Lunar.Core.Capabilities;

namespace Lunar.Core.Workflows;

public sealed class WorkflowDefinition
{
    private readonly ReadOnlyCollection<WorkflowStep> _steps;

    public WorkflowDefinitionId Id { get; }

    public int Version { get; }

    public string Name { get; }

    public IReadOnlyList<WorkflowStep> Steps => _steps;

    public DateTimeOffset CreatedAt { get; }


    public WorkflowDefinition(
        WorkflowDefinitionId id,
        int version,
        string name,
        IEnumerable<WorkflowStep> steps)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow definition identifier cannot be empty.",
                nameof(id));
        }

        if (version < 1)
        {
            throw new ArgumentException(
                "Workflow definition version must be a positive integer.",
                nameof(version));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Workflow definition name cannot be null, empty, or whitespace.",
                nameof(name));
        }

        ArgumentNullException.ThrowIfNull(steps);

        var stepList = steps.ToList();

        if (stepList.Count == 0)
        {
            throw new ArgumentException(
                "At least one workflow step is required.",
                nameof(steps));
        }

        ValidateStepPositions(stepList);

        _steps = stepList.AsReadOnly();

        Id = id;
        Version = version;
        Name = name;
        CreatedAt = DateTimeOffset.UtcNow;
    }


    private static void ValidateStepPositions(List<WorkflowStep> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var position = steps[i].Position;
            var expected = i + 1;

            if (position != expected)
            {
                throw new ArgumentException(
                    "Workflow step positions must be declared in contiguous order beginning at 1.",
                    nameof(steps));
            }
        }
    }
}
