namespace ReportGeneratorCore.DTOs;

public sealed record ValidatedSpec(
    ReportSpec Spec,
    ValidationResult Validation
);