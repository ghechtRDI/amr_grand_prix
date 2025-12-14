using AmrGrandPrix.API.Services;

namespace AmrGrandPrix.API.Tests.Infrastructure;

/// <summary>
/// Fake email service for testing that doesn't actually send emails
/// </summary>
public class FakeEmailService : IEmailService
{
    public List<SentEmail> SentEmails { get; } = new();

    public Task SendEmailAsync(string toEmail, string subject, string body)
    {
        SentEmails.Add(new SentEmail
        {
            To = toEmail,
            Subject = subject,
            Body = body,
            SentAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink)
    {
        SentEmails.Add(new SentEmail
        {
            To = toEmail,
            Subject = "Confirm your email",
            Body = $"Please confirm your email by clicking this link: {confirmationLink}",
            SentAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string resetLink)
    {
        SentEmails.Add(new SentEmail
        {
            To = toEmail,
            Subject = "Reset your password",
            Body = $"Reset your password by clicking this link: {resetLink}",
            SentAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public void Clear()
    {
        SentEmails.Clear();
    }
}

public class SentEmail
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
