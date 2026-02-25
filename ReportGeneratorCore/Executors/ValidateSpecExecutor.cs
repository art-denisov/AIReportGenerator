using Microsoft.Agents.AI.Workflows;

internal sealed partial class ValidateSpecExecutor : Executor
{
    public ValidateSpecExecutor()
        : base("ValidateSpec")
    {
    }

    [MessageHandler]
    private ValueTask<ValidatedSpec> HandleAsync(
        ReportSpec spec,
        IWorkflowContext context,
        CancellationToken ct)
    {
        var validation = Validate(spec);

        var result = new ValidatedSpec(spec, validation);

        return ValueTask.FromResult(result);
    }

    private static ValidationResult Validate(ReportSpec spec)
    {
        var issues = new List<ValidationIssue>();

        var title = (spec.Title ?? "").Trim();
        if (title.Length == 0)
            issues.Add(new("title.empty", "Title must be non-empty.", "title"));
        else if (title.Length > 120)
            issues.Add(new("title.too_long", "Title must be <= 120 chars.", "title"));

        if (spec.Columns is null || spec.Columns.Length == 0)
        {
            issues.Add(new("columns.empty", "At least one column is required.", "columns"));
        }
        else
        {
            var cleaned = spec.Columns.Select(c => (c ?? "").Trim()).Where(c => c.Length > 0).ToArray();
            if (cleaned.Length == 0)
                issues.Add(new("columns.empty", "At least one non-empty column is required.", "columns"));

            if (cleaned.Length > 20)
                issues.Add(new("columns.too_many", "Too many columns (max 20).", "columns"));

            var duplicates = cleaned
                .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            foreach (var d in duplicates)
                issues.Add(new("columns.duplicate", $"Duplicate column: '{d}'.", "columns"));

            var tooLong = cleaned.Where(c => c.Length > 60).ToArray();
            foreach (var c in tooLong)
                issues.Add(new("columns.too_long", $"Column is too long (>60): '{c}'.", "columns"));
        }

        return issues.Count == 0 ? ValidationResult.Ok() : ValidationResult.Fail(issues.ToArray());
    }
}