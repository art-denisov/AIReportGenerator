using System.Text.Json.Serialization;

public sealed record ReportSpec(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("columns")] string[] Columns
);

public sealed record ValidationIssue(
    string Code,
    string Message,
    string? Field = null
);

public sealed record ValidationResult(
    bool IsValid,
    ValidationIssue[] Issues
)
{
    public static ValidationResult Ok() => new(true, Array.Empty<ValidationIssue>());
    public static ValidationResult Fail(params ValidationIssue[] issues) => new(false, issues);
}

/// <summary>
/// Результат этапа валидации: исходная спецификация + отчет о проверке.
/// </summary>
public sealed record ValidatedSpec(
    ReportSpec Spec,
    ValidationResult Validation
);