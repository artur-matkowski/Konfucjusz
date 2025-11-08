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
        // Validate required settings
        if (string.IsNullOrEmpty(_opts.Host))
            throw new InvalidOperationException("SMTP host is not configured. Check Smtp:Host in configuration.");
        if (string.IsNullOrEmpty(_opts.From))
            throw new InvalidOperationException("From address is not configured. Check Smtp:From in configuration.");
        if (string.IsNullOrEmpty(To))
            throw new ArgumentException("To address is not set.");
        if (string.IsNullOrEmpty(Subject))
            throw new ArgumentException("Email subject is not set.");
        if (string.IsNullOrEmpty(Body))
            throw new ArgumentException("Email body is not set.");

        try
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_opts.From));
            email.To.Add(MailboxAddress.Parse(To));
            email.Subject = Subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = Body
            };

            using var smtp = new SmtpClient();
            
            // Log connection attempt
            Console.WriteLine($"Connecting to SMTP server {_opts.Host}:{_opts.Port} (UseStartTls: {_opts.UseStartTls})");
            
            var secure = _opts.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            smtp.Connect(_opts.Host, _opts.Port, secure);

            if (!string.IsNullOrEmpty(_opts.Username))
            {
                Console.WriteLine($"Authenticating as {_opts.Username}");
                smtp.Authenticate(_opts.Username, _opts.Password ?? string.Empty);
            }

            Console.WriteLine("Sending email...");
            smtp.Send(email);
            smtp.Disconnect(true);
            Console.WriteLine("Email sent successfully!");
        }
        catch (Exception ex)
        {
            var error = $"Failed to send email: {ex.Message}";
            if (ex.InnerException != null)
                error += $"\nInner error: {ex.InnerException.Message}";
            
            Console.WriteLine(error);
            throw new Exception(error, ex);
        }
    }
}