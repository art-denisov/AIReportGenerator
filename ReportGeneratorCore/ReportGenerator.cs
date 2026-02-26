using System.Runtime.CompilerServices;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ReportGeneratorCore.DTOs;

namespace ReportGeneratorCore;

public sealed class ReportGenerator {
    readonly Workflow workflow;

    public ReportGenerator(IChatClient chatClient) {
        ArgumentNullException.ThrowIfNull(chatClient);

        var spec = new Executors.SpecExecutor(chatClient);
        var validate = new Executors.ValidateSpecExecutor();

        var builder = new WorkflowBuilder(spec);
        builder.AddEdge(spec, validate);

        workflow = builder
            .WithOutputFrom(validate)
            .Build();
    }

    async IAsyncEnumerable<WorkflowEvent> RunStreamingAsync( string userPrompt, [EnumeratorCancellation] CancellationToken ct = default) {
        if (string.IsNullOrWhiteSpace(userPrompt))
            throw new ArgumentException("Requirements must be non-empty.", nameof(userPrompt));

        await using var run = await InProcessExecution
            .RunStreamingAsync(workflow, userPrompt, cancellationToken: ct)
            .ConfigureAwait(false);

        await foreach (var evt in run.WatchStreamAsync(ct).ConfigureAwait(false))
            yield return evt;
    }

    public async Task<ValidatedSpec> GenerateAsync(string userPrompt, Action<WorkflowEvent>? onEvent = null, CancellationToken ct = default) {
        ValidatedSpec? output = null;
        Exception? error = null;

        await foreach (var evt in RunStreamingAsync(userPrompt, ct).ConfigureAwait(false)) {
            onEvent?.Invoke(evt);

            switch (evt) {
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

        if (error is not null) 
            throw error;
        return output ?? throw new InvalidOperationException("Workflow completed without output.");
    }
}