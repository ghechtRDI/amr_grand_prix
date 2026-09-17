using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace AmrGrandPrix.API.Services.LlmExtraction.Providers;

public class OllamaLlmProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly OllamaLlmSettings _settings;
    private readonly ILogger<OllamaLlmProvider> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    private const string SystemPrompt = """
        You are a race results extractor for the Alaska Mountain Runners Grand Prix.
        Extract all runner results from the provided text and output ONLY valid JSON
        matching this exact schema (no other text):

        {
          "sections": [
            {
              "name": <string or null>,
              "gender": <"Male" | "Female" | null>,
              "rows": [
                {
                  "place": <integer or null>,
                  "name": <string>,
                  "age": <integer or null>,
                  "gender": <"Male" | "Female" | "Nonbinary" | null>,
                  "time_string": <string or null>,
                  "status": <"Finished" | "DNF" | "DNS" | "DQ">,
                  "notes": <string or null>
                }
              ]
            }
          ]
        }

        Rules:
        - If the file has sections (e.g. "MALE RESULTS", "FEMALE RESULTS"), create a separate
          section object for each. Set section.gender to "Male" or "Female" as appropriate.
        - If there are no section dividers, create a single section with name=null and gender=null.
        - For names in "Last, First" format (e.g. "Smith, John"), output "John Smith".
        - For age groups, use the lower bound as the integer age:
            "30-39" → 30, "80+" → 80, "U17" → 17, "17 and Under" → 17.
        - status must be one of: Finished, DNF, DNS, DQ. Default to "Finished".
        - Preserve time strings exactly (e.g. "1:23:45", "23:45"). Use null for DNF/DNS/DQ rows.
        - Annotations on times (*, #, CR, WR, etc.) belong in notes, not time_string.
        - Output ONLY the JSON object. No markdown, no explanation.
        """;

    public OllamaLlmProvider(
        HttpClient http,
        IOptions<LlmSettings> options,
        ILogger<OllamaLlmProvider> logger)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromMinutes(10);
        _settings = options.Value.Ollama;
        _logger = logger;
    }

    public async Task<LlmProviderResult> ExtractAsync(string text, string fileName, CancellationToken ct = default)
    {
        var userContent = $"filename: {fileName}\n---\n{text}";

        var body = new
        {
            model = _settings.Model,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",   content = userContent }
            },
            format = "json",
            stream = false,
            options = new { temperature = 0, num_predict = 4096, num_ctx = 8192 }
        };

        var json = JsonSerializer.Serialize(body, _jsonOpts);
        var url  = $"{_settings.BaseUrl.TrimEnd('/')}/api/chat";

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        _logger.LogInformation("Calling Ollama {Model} at {Url} for {FileName}", _settings.Model, url, fileName);

        var res = await _http.SendAsync(req, ct);
        var responseBody = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("Ollama API error {Status}: {Body}", res.StatusCode, responseBody);
            throw new InvalidOperationException($"Ollama API error {res.StatusCode}: {responseBody}");
        }

        var doc = JsonNode.Parse(responseBody)!;
        var inputTokens  = doc["prompt_eval_count"]?.GetValue<int>() ?? 0;
        var outputTokens = doc["eval_count"]?.GetValue<int>()        ?? 0;
        var model        = doc["model"]?.GetValue<string>()          ?? _settings.Model;
        var content      = doc["message"]?["content"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Ollama response had no message content");

        _logger.LogInformation("Ollama extraction complete. Tokens: {In}in / {Out}out", inputTokens, outputTokens);

        return new LlmProviderResult(content, model, inputTokens, outputTokens);
    }
}
