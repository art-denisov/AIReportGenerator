using System.Runtime.CompilerServices;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

public sealed class ReportGenerator
{
    private readonly Workflow _workflow;

    public ReportGenerator(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        // Executors
        var spec = new SpecExecutor(chatClient);
        var validate = new ValidateSpecExecutor();
        //var output = new OutputExecutor();

        // Graph: Spec -> Validate
        var builder = new WorkflowBuilder(spec);
        builder.AddEdge(spec, validate);
        //builder.AddEdge(validate, output);
        
        
        // Input message type is string (requirements)
        _workflow = builder
            .WithOutputFrom(validate)
            .Build();
    }

    /// <summary>
    /// Streaming execution: yields all workflow events as they happen
    /// (ExecutorInvoked/Completed, custom events, output, errors, etc.).
    /// </summary>
    public async IAsyncEnumerable<WorkflowEvent> RunStreamingAsync(
        string requirements,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requirements))
            throw new ArgumentException("Requirements must be non-empty.", nameof(requirements));
        
        await using var run = await InProcessExecution
            .RunStreamingAsync(_workflow, requirements, cancellationToken: ct)
            .ConfigureAwait(false);

        // await foreach (var evt in run.WatchStreamAsync().WithCancellation(ct))
        // {
        //     Console.WriteLine($"FROM GENERATOR {evt}" );
        // }

        await foreach (var evt in run.WatchStreamAsync().WithCancellation(ct).ConfigureAwait(false))
            yield return evt;
    }

    /// <summary>
    /// Convenience wrapper: streams events to a callback and returns the final ValidatedSpec output.
    /// Throws if workflow errors or finishes without output.
    /// </summary>
    public async Task<ValidatedSpec> GenerateAsync(
        string requirements,
        Action<WorkflowEvent>? onEvent = null,
        CancellationToken ct = default)
    {
        ValidatedSpec? output = null;
        Exception? error = null;

        await foreach (var evt in RunStreamingAsync(requirements, ct).ConfigureAwait(false))
        {
            onEvent?.Invoke(evt);

            switch (evt)
            {
                case WorkflowOutputEvent o when o.Is<ValidatedSpec>(out var data):
                    output = data;
                    break;

                case WorkflowErrorEvent e:
                    error = e.Exception;
                    break;
            }

            if (output is not null || error is not null)
                break;
        }

        if (error is not null) throw error;
        if (output is null) throw new InvalidOperationException("Workflow completed without output.");
        return output;
    }
}