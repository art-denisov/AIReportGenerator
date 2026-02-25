using Microsoft.Agents.AI.Workflows;

internal sealed partial class OutputExecutor : Executor
{
    public OutputExecutor() : base("Output") {}

    [MessageHandler]
    private async ValueTask HandleAsync(ValidatedSpec validated, IWorkflowContext context, CancellationToken ct)
        => await context.YieldOutputAsync(validated, ct);
}