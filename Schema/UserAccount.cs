

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    [Column("user_password")]
    [MaxLength(256)]
    public string? userPassword { get; set; }

    [Column("user_email")]
    [MaxLength(100)]
    public string? userEmail { get; set; }

    [Column("user_role")]
    [MaxLength(30)]
    public string? userRole { get; set; }
}