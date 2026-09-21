using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Services.Captcha;

public class TurnstileCaptchaService : ICaptchaService
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private readonly HttpClient _httpClient;
    private readonly CaptchaSettings _settings;
    private readonly ILogger<TurnstileCaptchaService> _logger;

    public TurnstileCaptchaService(HttpClient httpClient, IOptions<CaptchaSettings> settings, ILogger<TurnstileCaptchaService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string token, string? remoteIp)
    {
        if (_settings.Disabled)
            return true;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var formData = new List<KeyValuePair<string, string>>
        {
            new("secret", _settings.SecretKey),
            new("response", token)
        };
        if (!string.IsNullOrWhiteSpace(remoteIp))
            formData.Add(new KeyValuePair<string, string>("remoteip", remoteIp));

        try
        {
            using var response = await _httpClient.PostAsync(VerifyUrl, new FormUrlEncodedContent(formData));
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>();
            return result?.Success ?? false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Turnstile verification request failed");
            return false;
        }
    }

    private sealed class TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}
