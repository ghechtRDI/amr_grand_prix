namespace AmrGrandPrix.API.Services.LlmExtraction;

public record ExtractionResult(
    List<ExtractedSection> Sections,
    string RawModelJson,
    string LlmModel,
    int InputTokens,
    int OutputTokens);

public record ExtractedSection(string? Name, string? Gender, List<ExtractedRow> Rows);

public record ExtractedRow(
    int? Place,
    string Name,
    int? Age,
    string? AgeCategory,
    string? Gender,
    string? TimeString,
    string? Status,
    string? Notes);
