namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendVerificationCodeAsync(string toEmail, string fullName,string code);
        Task SendWelcomeEmailAsync(string toEmail, string fullName);
        Task SendPasswordResetTokenAsync(string toEmail, string token);
    }
}