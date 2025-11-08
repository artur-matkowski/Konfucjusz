using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

// Simple POCO to bind SMTP settings from configuration.
public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
}

// EmailRequest is registered as a DI service and reads SMTP settings from configuration.
public class EmailRequest
{
    private SmtpOptions _opts;

    // Parameterless ctor kept for places that instantiate EmailRequest directly.
    public EmailRequest()
    {
        _opts = new SmtpOptions();
    }

    public EmailRequest(IOptions<SmtpOptions> opts)
    {
        _opts = opts.Value ?? new SmtpOptions();
    }

    // Per-call properties (can be set by the caller before sending)
    public string? To { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }

    public void SendEmail()
    {
        var email = new MimeMessage();
        var fromAddress = _opts.From ?? string.Empty;
        email.From.Add(MailboxAddress.Parse(fromAddress));
        email.To.Add(MailboxAddress.Parse(To ?? string.Empty));
        email.Subject = Subject;
        email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
        {
            Text = this.Body
        };

        using var stmp = new SmtpClient();
        var secure = _opts.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        stmp.Connect(_opts.Host ?? string.Empty, _opts.Port, secure);

        if (!string.IsNullOrEmpty(_opts.Username))
        {
            stmp.Authenticate(_opts.Username, _opts.Password ?? string.Empty);
        }

        stmp.Send(email);
        stmp.Disconnect(true);
    }
}