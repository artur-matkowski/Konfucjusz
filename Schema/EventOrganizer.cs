using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("event_organizers")]
public class EventOrganizer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("event_id")]
    public int EventId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    [ForeignKey(nameof(UserId))]
    public UserAccount? User { get; set; }
}
