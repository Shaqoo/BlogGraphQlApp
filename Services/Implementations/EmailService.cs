using System.Net;
using System.Net.Mail;
using BlogGraphQlApp.Config;
using BlogGraphQlApp.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            _logger.LogInformation("Attempting to send email to {ToEmail} with subject '{Subject}'", toEmail, subject);
            using (var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
            {
                Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.Password),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            })
            {
                var message = new MailMessage
                {
                    From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);

                try
                {
                    await client.SendMailAsync(message);
                    _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
                    throw;
                }
            }
        }

        public async Task SendVerificationCodeAsync(string toEmail,string fullName, string code)
        {
            var subject = "Your BlogApp Verification Code";
            var body = GetVerificationEmailBody(code,fullName);

            await SendEmailAsync(toEmail, subject, body);
        }

        private static string GetVerificationEmailBody(string code,string fullName)
        {
            return $@"
                <div style='font-family: Arial, sans-serif; line-height: 1.6;'>
                    <h2>Welcome to BlogApp, {fullName}!</h2>
                    <p>We're thrilled to have you on board. To complete your registration and secure your account, please use the verification code below.</p>
                    <p>This code will expire in 10 minutes.</p>
                    <div style='text-align: center; margin: 20px 0;'>
                        <span style='font-size: 24px; font-weight: bold; letter-spacing: 5px; padding: 10px; background-color: #f2f2f2; border-radius: 5px;'>
                            {code}
                        </span>
                    </div>
                    <p>If you did not sign up for a BlogApp account, you can safely ignore this email.</p>
                    <p>Best regards,<br/>The BlogApp Team</p>
                    <hr/>
                    <p style='font-size: 12px; color: #888;'>
                        This is an automated message. Please do not reply to this email.
                    </p>
                </div>";
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string fullName)
        {
            var subject = $"Welcome to BlogApp, {fullName}!";
            var body = GetWelcomeEmailBody(fullName);
            await SendEmailAsync(toEmail, subject, body);
        }

        private static string GetWelcomeEmailBody(string fullName)
        {
            return $@"
                <div style='font-family: Arial, sans-serif; line-height: 1.6;'>
                    <h2>Welcome to BlogApp, {fullName}!</h2>
                    <p>We're thrilled to have you on board. Your email has been successfully verified.</p>
                    <p>You can now start exploring, creating posts, and connecting with others.</p>
                    <p>Happy blogging!</p>
                    <p>Best regards,<br/>The BlogApp Team</p>
                </div>";
        }

        public async Task SendPasswordResetTokenAsync(string toEmail, string token)
        {
            var subject = "Your Password Reset Request";
            var body = GetPasswordResetEmailBody(token);
            await SendEmailAsync(toEmail, subject, body);
        }

        private static string GetPasswordResetEmailBody(string token)
        {
            // In a real app, you'd use a URL like: $"https://yourapp.com/reset-password?token={token}"
            return $@"
                <div style='font-family: Arial, sans-serif; line-height: 1.6;'>
                    <h2>Password Reset Request</h2>
                    <p>We received a request to reset your password. Use the token below to set a new password.</p>
                    <p>This token is valid for 15 minutes.</p>
                    <div style='text-align: center; margin: 20px 0;'>
                        <span style='font-size: 20px; font-weight: bold; letter-spacing: 2px; padding: 10px; background-color: #f2f2f2; border-radius: 5px;'>
                            {token}
                        </span>
                    </div>
                    <p>If you did not request a password reset, please ignore this email or contact support if you have concerns.</p>
                    <p>Best regards,<br/>The BlogApp Team</p>
                </div>";
        }
    }
}
