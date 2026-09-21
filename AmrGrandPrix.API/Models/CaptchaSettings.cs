namespace AmrGrandPrix.API.Models;

public class CaptchaSettings
{
    /// <summary>
    /// Cloudflare Turnstile secret key, used server-side to verify a widget response token.
    /// Set via user-secrets in development: `dotnet user-secrets set "Captcha:SecretKey" "..."`.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// When true, verification always succeeds without calling Cloudflare — used for local
    /// development/testing when no Turnstile keys are configured.
    /// </summary>
    public bool Disabled { get; set; }
}
