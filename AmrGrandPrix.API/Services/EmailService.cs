using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            using var smtpClient = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
            {
                EnableSsl = _emailSettings.EnableSsl
            };

            // Only use credentials if provided
            if (!string.IsNullOrEmpty(_emailSettings.SmtpUsername) &&
                !string.IsNullOrEmpty(_emailSettings.SmtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(
                    _emailSettings.SmtpUsername,
                    _emailSettings.SmtpPassword
                );
            }

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
            throw;
        }
    }

    public async Task SendEmailConfirmationAsync(string toEmail, string confirmationLink)
    {
        var subject = "Confirm your email address";
        var body = $@"
            <html>
            <body>
                <h2>Welcome to Alaska Mountain Runners Grand Prix!</h2>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href='{confirmationLink}'>Confirm Email</a></p>
                <p>If you did not create an account, please ignore this email.</p>
                <br/>
                <p>Best regards,<br/>Alaska Mountain Runners Grand Prix Team</p>
            </body>
            </html>
        ";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetLink)
    {
        var subject = "Reset your password - Alaska Mountain Runners Grand Prix";
        var body = $@"
            <html>
            <body>
                <h2>Reset your password</h2>
                <p>We received a request to reset the password for your Alaska Mountain Runners Grand Prix account.
                   Click the link below to choose a new one:</p>
                <p><a href='{resetLink}'>Reset Password</a></p>
                <p>This link expires in 24 hours. If you did not request a password reset, you can ignore this email;
                   your password won't change.</p>
                <br/>
                <p>Best regards,<br/>Alaska Mountain Runners Grand Prix Team</p>
            </body>
            </html>
        ";

        await SendEmailAsync(toEmail, subject, body);
    }
}
