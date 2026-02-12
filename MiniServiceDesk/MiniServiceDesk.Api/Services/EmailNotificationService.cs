using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MiniServiceDesk.Api.Services;

public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailNotificationOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(IOptions<EmailNotificationOptions> options, ILogger<EmailNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var effectiveRecipient = string.IsNullOrWhiteSpace(_options.OverrideTo)
            ? toEmail
            : _options.OverrideTo!;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.From),
                Subject = subject,
                Body = body
            };
            message.To.Add(effectiveRecipient);

            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password ?? string.Empty);
            }

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email notification failed for recipient {Recipient}", effectiveRecipient);
        }
    }
}
