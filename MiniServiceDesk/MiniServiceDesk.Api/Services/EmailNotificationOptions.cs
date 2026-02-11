namespace MiniServiceDesk.Api.Services;

public sealed class EmailNotificationOptions
{
    public bool Enabled { get; set; }
    public string From { get; set; } = "no-reply@miniservicedesk.local";
    public string? OverrideTo { get; set; }
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
}
