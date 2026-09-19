namespace AmrGrandPrix.API.Services.LlmExtraction.TextExtractors;

public static class CsvTextExtractor
{
    public static async Task<string> ExtractAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
