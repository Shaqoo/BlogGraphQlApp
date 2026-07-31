namespace BlogGraphQlApp.Config
{
    public class EmailSettings
    {
        public const string SectionName = nameof(EmailSettings);

        public required string SmtpServer { get; set; }
        public int Port { get; set; }
        public required string SenderName { get; set; }
        public required string SenderEmail { get; set; }
        public required string Password { get; set; }
    }
}