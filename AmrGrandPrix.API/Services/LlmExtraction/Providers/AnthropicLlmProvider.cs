using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace AmrGrandPrix.API.Services.LlmExtraction.Providers;

public class AnthropicLlmProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly AnthropicLlmSettings _settings;
    private readonly ILogger<AnthropicLlmProvider> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    private const string SystemPrompt = """
        You are a race results extractor for the Alaska Mountain Runners Grand Prix.
        Extract all runner results from the provided text into the exact JSON structure
        defined by the extract_results tool.

        Rules:
        - If the file has sections (e.g. "MALE RESULTS", "FEMALE RESULTS"), create a separate
          section object for each. Set section.gender to "Male" or "Female" as appropriate.
        - If there are no section dividers, create a single section with name=null and gender=null.
        - For names in "Last, First" format (e.g. "Smith, John"), output "John Smith".
        - For age groups, use the lower bound as the integer age:
            "30-39" → 30, "80+" → 80, "U17" → 17, "17 and Under" → 17.
        - status must be one of: Finished, DNF, DNS, DQ. Default to "Finished".
        - Preserve time strings exactly (e.g. "1:23:45", "23:45"). Use null for DNF/DNS/DQ rows.
        - If a field is absent from the source data set it to null.
        - Annotations on times (*, #, CR, WR, etc.) belong in the notes field, not time_string.
        - Do not invent data. If something is unclear, use null.
        """;

    private static readonly object ToolSchema = new
    {
        type = "object",
        properties = new
        {
            sections = new
            {
                type = "array",
                description = "List of result sections (e.g. Male / Female)",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        name    = new { type = new[] { "string", "null" }, description = "Section header text, or null" },
                        gender  = new { type = new[] { "string", "null" }, @enum = new[] { "Male", "Female", null! }, description = "Gender inferred from section name" },
                        rows = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    place       = new { type = new[] { "integer", "null" } },
                                    name        = new { type = "string" },
                                    age         = new { type = new[] { "integer", "null" } },
                                    gender      = new { type = new[] { "string", "null" }, @enum = new[] { "Male", "Female", "Nonbinary", null! } },
                                    time_string = new { type = new[] { "string", "null" } },
                                    status      = new { type = "string", @enum = new[] { "Finished", "DNF", "DNS", "DQ" } },
                                    notes       = new { type = new[] { "string", "null" } }
                                },
                                required = new[] { "name", "status" }
                            }
                        }
                    },
                    required = new[] { "rows" }
                }
            }
        },
        required = new[] { "sections" }
    };

    public AnthropicLlmProvider(
        HttpClient http,
        IOptions<LlmSettings> options,
        ILogger<AnthropicLlmProvider> logger)
    {
        _http = http;
        _settings = options.Value.Anthropic;
        _logger = logger;
    }

    public async Task<LlmProviderResult> ExtractAsync(string text, string fileName, CancellationToken ct = default)
    {
        var userContent = $"filename: {fileName}\n---\n{text}";

        var body = new
        {
            model = _settings.Model,
            max_tokens = 8192,
            temperature = 0,
            system = SystemPrompt,
            tools = new[]
            {
                new
                {
                    name        = "extract_results",
                    description = "Extract race results into a structured JSON format",
                    input_schema = ToolSchema
                }
            },
            tool_choice = new { type = "tool", name = "extract_results" },
            messages = new[]
            {
                new { role = "user", content = userContent }
            }
        };

        var json = JsonSerializer.Serialize(body, _jsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-api-key", _settings.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        _logger.LogInformation("Calling Anthropic {Model} for {FileName}", _settings.Model, fileName);

        var res = await _http.SendAsync(req, ct);
        var responseBody = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API error {Status}: {Body}", res.StatusCode, responseBody);
            throw new InvalidOperationException($"Anthropic API error {res.StatusCode}: {responseBody}");
        }

        var doc = JsonNode.Parse(responseBody)!;
        var inputTokens  = doc["usage"]?["input_tokens"]?.GetValue<int>()  ?? 0;
        var outputTokens = doc["usage"]?["output_tokens"]?.GetValue<int>() ?? 0;
        var model        = doc["model"]?.GetValue<string>() ?? _settings.Model;

        // Locate tool_use block
        var content = doc["content"]?.AsArray();
        var toolBlock = content?.FirstOrDefault(c => c?["type"]?.GetValue<string>() == "tool_use");
        if (toolBlock == null)
            throw new InvalidOperationException("Anthropic response contained no tool_use block");

        var resultJson = toolBlock["input"]?.ToJsonString(_jsonOpts)
            ?? throw new InvalidOperationException("tool_use block has no input");

        _logger.LogInformation("Anthropic extraction complete. Tokens: {In}in / {Out}out", inputTokens, outputTokens);

        return new LlmProviderResult(resultJson, model, inputTokens, outputTokens);
    }
}
