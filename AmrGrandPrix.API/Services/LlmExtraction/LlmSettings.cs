namespace AmrGrandPrix.API.Services.LlmExtraction;

public class LlmSettings
{
    public string Provider { get; set; } = "Anthropic";
    public AnthropicLlmSettings Anthropic { get; set; } = new();
    public OllamaLlmSettings Ollama { get; set; } = new();
}

public class AnthropicLlmSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-haiku-4-5-20251001";
}

public class OllamaLlmSettings
{
    public string BaseUrl { get; set; } = "http://ollama:11434";
    public string Model { get; set; } = "qwen2.5:14b-instruct";
}
