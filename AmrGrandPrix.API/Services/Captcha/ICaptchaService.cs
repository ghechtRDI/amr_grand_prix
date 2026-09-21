namespace AmrGrandPrix.API.Services.Captcha;

/// <summary>
/// Verifies a Cloudflare Turnstile widget response token server-side before a bot-sensitive
/// action (registration, the public report form) is allowed to proceed.
/// </summary>
public interface ICaptchaService
{
    Task<bool> VerifyAsync(string token, string? remoteIp);
}
