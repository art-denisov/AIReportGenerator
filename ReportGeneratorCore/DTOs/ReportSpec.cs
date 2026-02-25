using System.Text.Json.Serialization;

namespace ReportGeneratorCore.DTOs;

public sealed record ReportSpec(
    [property: JsonPropertyName("title")] 
    string Title,
    
    [property: JsonPropertyName("columns")]
    string[] Columns
);