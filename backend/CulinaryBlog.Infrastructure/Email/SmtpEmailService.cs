using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace CulinaryBlog.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public string From { get; init; } = "noreply@culinaryblog.local";
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public bool EnableSsl { get; init; }
}

public sealed class SmtpEmailService(IOptions<SmtpOptions> options) : IEmailService
{
    public async Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(settings.UserName))
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);

        var safeName = HtmlEncoder.Default.Encode(fullName);
        using var message = new MailMessage(settings.From, email)
        {
            Subject = "Chào mừng bạn đến Culinary Blog",
            Body = $"<p>Chào {safeName},</p><p>Cảm ơn bạn đã tham gia Culinary Blog.</p>",
            IsBodyHtml = true
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
