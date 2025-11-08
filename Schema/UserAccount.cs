

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using System.Net.Http;


[Table("user_account")]
public class UserAccount
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_name")]
    [MaxLength(100)]
    public string? userName { get; set; }
    
    [Column("user_surname")]
    [MaxLength(100)]
    public string? surname { get; set; }

    [Column("user_password")]
    [MaxLength(256)]
    public string? userPassword { get; set; }

    [Column("user_email")]
    [MaxLength(100)]
    public string? userEmail { get; set; }

    [Column("user_role")]
    [MaxLength(100)]
    public string? userRole { get; set; }

    [Column("mail_validated")]
    public bool mailValidated { get; set; }

    [Column("user_creation_confirmed_by_admin")]
    public bool userCreationConfirmedByAdmin { get; set; }

    [Column("creation_timestamp")] //with timezone
    public DateTime creationTimestamp { get; set; }


    public string GetProtectedHash()
    {
        // create a protected token containing user id, username, password hash, email, role and timestamp
        var provider = DataProtectionProvider.Create("Konfucjusz");
        var protector = provider.CreateProtector("Konfucjusz.EmailVerification.v1");
        var payload = $"{this.Id}|{this.userName}|{this.userPassword}|{this.userEmail}|{this.userRole}";
        var tokenBytes = protector.Protect(Encoding.UTF8.GetBytes(payload));
        var token = Convert.ToBase64String(tokenBytes);

        // build a verification link (URL-encode the token)
        var encoded = WebUtility.UrlEncode(token);


        return encoded;
    }

    // Password reset token: payload id|email|expiryTicks (UTC). Protected with separate purpose string.
    public string GeneratePasswordResetToken(TimeSpan ttl)
    {
        if (string.IsNullOrEmpty(this.userEmail))
            throw new InvalidOperationException("User email is required to generate reset token.");

        var provider = DataProtectionProvider.Create("Konfucjusz");
        var protector = provider.CreateProtector("Konfucjusz.PasswordReset.v1");
        var expiryUtc = DateTime.UtcNow.Add(ttl).Ticks;
        var payload = $"{this.Id}|{this.userEmail}|{expiryUtc}";
        var tokenBytes = protector.Protect(Encoding.UTF8.GetBytes(payload));
        var token = Convert.ToBase64String(tokenBytes);
        return WebUtility.UrlEncode(token);
    }

    public static bool TryParsePasswordResetToken(string token, out int userId, out string? email, out DateTime expiryUtc)
    {
        userId = 0;
        email = null;
        expiryUtc = default;
        try
        {
            var provider = DataProtectionProvider.Create("Konfucjusz");
            var protector = provider.CreateProtector("Konfucjusz.PasswordReset.v1");
            // The ASP.NET Core query string binding already URL-decodes parameters.
            // The GeneratePasswordResetToken method URL-encodes the protected Base64 string before placing it in the link.
            // Therefore, at this point "token" should be the original Base64 text; do NOT UrlDecode again (would turn '+' into space).
            var bytes = Convert.FromBase64String(token);
            var original = Encoding.UTF8.GetString(protector.Unprotect(bytes));
            var parts = original.Split('|');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out userId)) return false;
            email = parts[1];
            if (!long.TryParse(parts[2], out var ticks)) return false;
            expiryUtc = new DateTime(ticks, DateTimeKind.Utc);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? ExtractMailFromToken(string token)
    {
        try
        {
            var provider = DataProtectionProvider.Create("Konfucjusz");
            var protector = provider.CreateProtector("Konfucjusz.EmailVerification.v1");

            var bytes = Convert.FromBase64String(token);
            var original = Encoding.UTF8.GetString(protector.Unprotect(bytes));

            var parts = original.Split('|');
            return parts[3];
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
    
    new public string ToString()
    {
        return $"{this.Id}|{this.userName}|{this.userPassword}|{this.userEmail}|{this.userRole}|{this.mailValidated}|{this.userCreationConfirmedByAdmin}";
    } 
}