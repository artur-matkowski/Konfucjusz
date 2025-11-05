

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;


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