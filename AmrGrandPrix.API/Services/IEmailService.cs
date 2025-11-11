namespace AmrGrandPrix.API.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body);
    Task SendEmailConfirmationAsync(string toEmail, string confirmationLink);
}
