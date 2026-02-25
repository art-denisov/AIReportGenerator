namespace ReportGeneratorCore.DTOs;

public sealed record ValidationIssue(
    string Code,
    string Message,
    string? Field = null
);