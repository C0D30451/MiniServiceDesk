namespace MiniServiceDesk.Api.Services;

public interface IEmailNotificationService
{
    Task SendAsync(string toEmail, string subject, string body);
}
