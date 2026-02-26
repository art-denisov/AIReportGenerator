using Microsoft.Agents.AI.Workflows;
using ReportGeneratorCore.DTOs;

namespace ReportGeneratorCore.Executors;

internal sealed partial class ValidateSpecExecutor() : Executor("ValidateSpec") {
    [MessageHandler]
    ValueTask<ValidatedSpec> HandleAsync( ReportSpec spec, IWorkflowContext context, CancellationToken ct) {
        var validation = Validate(spec);

        var result = new ValidatedSpec(spec, validation);

        return ValueTask.FromResult(result);
    }

    static ValidationResult Validate(ReportSpec spec) {
        var issues = new List<ValidationIssue>();

        // var title = spec.Title.Trim();
        // switch (title.Length) {
        //     case 0:
        //         issues.Add(new ValidationIssue("title.empty", "Title must be non-empty.", "title"));
        //         break;
        //
        //     case > 120:
        //         issues.Add(new ValidationIssue("title.too_long", "Title must be <= 120 chars.", "title"));
        //         break;
        // }
        //
        // if (spec.Columns.Length == 0) 
        //     issues.Add(new ValidationIssue("columns.empty", "At least one column is required.", "columns"));
        // else {
        //     var cleaned = spec.Columns.Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();
        //     
        //     switch (cleaned.Length) {
        //         case 0:
        //             issues.Add(new ValidationIssue("columns.empty", "At least one non-empty column is required.", "columns"));
        //             break;
        //
        //         case > 20:
        //             issues.Add(new ValidationIssue("columns.too_many", "Too many columns (max 20).", "columns"));
        //             break;
        //     }
        //
        //     var duplicates = cleaned
        //         .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
        //         .Where(g => g.Count() > 1)
        //         .Select(g => g.Key)
        //         .ToArray();
        //
        //     issues.AddRange(duplicates.Select(d => new ValidationIssue("columns.duplicate", $"Duplicate column: '{d}'.", "columns")));
        //     var tooLong = cleaned.Where(c => c.Length > 60).ToArray();
        //     issues.AddRange(tooLong.Select(c => new ValidationIssue("columns.too_long", $"Column is too long (>60): '{c}'.", "columns")));
        //}

        return issues.Count == 0 ? ValidationResult.Ok() : ValidationResult.Fail(issues.ToArray());
    }
}