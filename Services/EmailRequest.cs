using MailKit.Net.Smtp;
using MimeKit;

public class EmailRequest
{
    public string? To { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }

    public void SendEmail()
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse("artur.matkowski.zan+konfucjusz@gmail.com"));
        email.To.Add(MailboxAddress.Parse(To));
        email.Subject = Subject;
        email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
        {
            Text = this.Body
        };

        using var stmp = new SmtpClient();
        stmp.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
        stmp.Authenticate("artur.matkowski.zan@gmail.com", "feja mskj fxru sbuq");
        stmp.Send(email);
        stmp.Disconnect(true);
    }
}