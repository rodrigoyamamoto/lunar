namespace Lunar.Core.Workflows;

public sealed class WorkflowExecutionStatus
{
    public static readonly WorkflowExecutionStatus Created =
        new(nameof(Created));

    public static readonly WorkflowExecutionStatus Running =
        new(nameof(Running));

    public static readonly WorkflowExecutionStatus Completed =
        new(nameof(Completed));

    public static readonly WorkflowExecutionStatus Failed =
        new(nameof(Failed));

    public static readonly WorkflowExecutionStatus Cancelled =
        new(nameof(Cancelled));

    private WorkflowExecutionStatus(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value;
    }
}