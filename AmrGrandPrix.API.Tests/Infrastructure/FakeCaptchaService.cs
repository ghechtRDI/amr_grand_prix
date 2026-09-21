using AmrGrandPrix.API.Services.Captcha;

namespace AmrGrandPrix.API.Tests.Infrastructure;

/// <summary>
/// Fake captcha service for testing that always succeeds without calling Cloudflare.
/// </summary>
public class FakeCaptchaService : ICaptchaService
{
    public Task<bool> VerifyAsync(string token, string? remoteIp) => Task.FromResult(true);
}
