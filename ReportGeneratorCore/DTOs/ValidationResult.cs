namespace ReportGeneratorCore.DTOs;

public sealed record ValidationResult( bool IsValid, ValidationIssue[] Issues ) {
    public static ValidationResult Ok() {
        return new ValidationResult(true, []);
    }

    public static ValidationResult Fail(params ValidationIssue[] issues) {
        return new ValidationResult(false, issues);
    }
}