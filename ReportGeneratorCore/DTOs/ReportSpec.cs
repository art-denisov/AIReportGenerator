using System.Text.Json.Serialization;

namespace ReportGeneratorCore.DTOs;

public sealed class ReportSpec {
    [JsonPropertyName("ReportType")] 
    public string? ReportType { get; set; }

    [JsonPropertyName("Title")] 
    public string? Title { get; set; }

    [JsonPropertyName("Orientation")] 
    public string? Orientation { get; set; }

    [JsonPropertyName("PageSize")] 
    public string? PageSize { get; set; }

    [JsonPropertyName("Theme")] 
    public string? Theme { get; set; }

    [JsonPropertyName("GroupBy")] 
    public List<string> GroupBy { get; set; } = [];

    [JsonPropertyName("SortBy")] 
    public List<string> SortBy { get; set; } = [];

    [JsonPropertyName("SortingType")] 
    public string? SortingType { get; set; }

    [JsonPropertyName("Columns")] 
    public List<ReportColumnDto> Columns { get; set; } = [];

    [JsonPropertyName("OtherValuableTokens")]
    public List<string> OtherValuableTokens { get; set; } = [];
    
    [JsonPropertyName("NERSuggestions")]
    public List<string> NERSuggestions { get; set; } = [];
}

public sealed class ReportColumnDto {
    [JsonPropertyName("Field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("DataType")]
    public string? DataType { get; set; }
}