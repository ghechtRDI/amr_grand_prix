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
        - If an exact age is given, put it in "age" and leave "age_category" null.
        - If only an age group/range is given (e.g. "30-39", "40-49", "U17", "80+"), do NOT
          guess a specific age — leave "age" null and put the group in "age_category", normalized
          to one of: "17 and Under", "18-29", "30-39", "40-49", "50-59", "60-69", "70-79", "80-89"
          (e.g. "U17" and "17 and Under" → "17 and Under"; "80+" → "80-89").
        - If neither an age nor an age group is present, leave both "age" and "age_category" null.
        - status must be one of: Finished, DNF, DNS, DQ. Default to "Finished".
        - Preserve time strings exactly (e.g. "1:23:45", "23:45"). Use null for DNF/DNS/DQ rows.
        - If a field is absent from the source data set it to null.
        - Annotations on times (*, #, CR, WR, etc.) belong in the notes field, not time_string.
        - Do not invent data. If something is unclear, use null.
        - Many PDFs print results twice: once in a "Gender Results" view and again in an "Age Group
          Results" view. Extract each runner only once — prefer the Gender Results section and skip
          any Age Group sub-sections that repeat the same runners.
        - If the document contains results from more than one distinct race, course, or event (for
          example, different distances, an adult/open race vs. a kids'/junior race, or named course
          variants like "Full Monty" vs "Uphill Only"), set "course" on each section to a short label
          identifying which race/course it belongs to, and use the SAME label on every section
          (regardless of gender) that belongs to that race. If the document describes only one race
          (even if split into Male/Female sections), leave "course" null on every section.
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
                        course  = new { type = new[] { "string", "null" }, description = "Short label identifying which distinct race/course/event this section belongs to, shared across sections of the same race; null if the document describes only one race" },
                        rows = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    place        = new { type = new[] { "integer", "null" } },
                                    name         = new { type = "string" },
                                    age          = new { type = new[] { "integer", "null" }, description = "Exact age, or null if only an age category is known" },
                                    age_category = new { type = new[] { "string", "null" }, @enum = new[] { "17 and Under", "18-29", "30-39", "40-49", "50-59", "60-69", "70-79", "80-89", null! }, description = "Set only when the source reports an age group/range instead of an exact age" },
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
            // Claude Haiku 4.5's output cap. Large results files (e.g. Mount Marathon's ~900+
            // finishers with per-leg splits) need most of this to avoid truncated/invalid JSON.
            max_tokens = 64000,
            temperature = 0,
            // Required at this max_tokens: a non-streaming call whose estimated generation time
            // exceeds Anthropic's ~10-minute threshold is rejected outright, and even when it
            // isn't, an idle non-streaming connection (no bytes until the full response is ready)
            // is exactly what corporate proxies/NAT idle timeouts kill — raising our own HttpClient
            // or frontend timeout can't fix either. Streaming sends bytes continuously instead.
            stream = true,
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

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!res.IsSuccessStatusCode)
        {
            var errorBody = await res.Content.ReadAsStringAsync(ct);
            _logger.LogError("Anthropic API error {Status}: {Body}", res.StatusCode, errorBody);
            throw new InvalidOperationException($"Anthropic API error {res.StatusCode}: {errorBody}");
        }

        var model             = _settings.Model;
        var inputTokens       = 0;
        var outputTokens      = 0;
        string? stopReason    = null;
        var toolBlockIndex    = -1;
        var toolInputByIndex  = new Dictionary<int, StringBuilder>();

        await using (var stream = await res.Content.ReadAsStreamAsync(ct))
        using (var reader = new StreamReader(stream))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                    continue;

                var payload = line["data:".Length..].Trim();
                if (payload.Length == 0)
                    continue;

                var evt  = JsonNode.Parse(payload)!;
                var type = evt["type"]?.GetValue<string>();

                switch (type)
                {
                    case "message_start":
                        var message = evt["message"]!;
                        model       = message["model"]?.GetValue<string>() ?? model;
                        inputTokens = message["usage"]?["input_tokens"]?.GetValue<int>() ?? 0;
                        break;

                    case "content_block_start":
                        var startIndex = evt["index"]!.GetValue<int>();
                        if (evt["content_block"]?["type"]?.GetValue<string>() == "tool_use")
                        {
                            toolBlockIndex = startIndex;
                            toolInputByIndex[startIndex] = new StringBuilder();
                        }
                        break;

                    case "content_block_delta":
                        var deltaIndex = evt["index"]!.GetValue<int>();
                        var delta      = evt["delta"]!;
                        if (delta["type"]?.GetValue<string>() == "input_json_delta"
                            && toolInputByIndex.TryGetValue(deltaIndex, out var builder))
                            builder.Append(delta["partial_json"]?.GetValue<string>());
                        break;

                    case "message_delta":
                        stopReason   = evt["delta"]?["stop_reason"]?.GetValue<string>() ?? stopReason;
                        outputTokens = evt["usage"]?["output_tokens"]?.GetValue<int>() ?? outputTokens;
                        break;

                    case "error":
                        var errMessage = evt["error"]?["message"]?.GetValue<string>() ?? payload;
                        throw new InvalidOperationException($"Anthropic API streaming error: {errMessage}");
                }
            }
        }

        if (stopReason == "max_tokens")
            _logger.LogWarning("Anthropic hit max_tokens limit for {FileName} — output may be truncated", fileName);

        if (toolBlockIndex < 0 || !toolInputByIndex.TryGetValue(toolBlockIndex, out var toolInput))
            throw new InvalidOperationException("Anthropic response contained no tool_use block");

        var resultJson = toolInput.ToString();

        _logger.LogInformation("Anthropic extraction complete. Tokens: {In}in / {Out}out", inputTokens, outputTokens);

        return new LlmProviderResult(resultJson, model, inputTokens, outputTokens);
    }
}
