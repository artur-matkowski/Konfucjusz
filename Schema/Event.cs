using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

[Table("events")]
public class Event
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("creation_timestamp")]
    public DateTime CreationTimestamp { get; set; }

    [Column("event_start_date")]
    public DateTime EventStartDate { get; set; }

    [Column("event_end_date")]
    public DateTime EventEndDate { get; set; }

    [Column("enlisting_start_date")]
    public DateTime EnlistingStartDate { get; set; }

    [Column("enlisting_end_date")]
    public DateTime EnlistingEndDate { get; set; }

    [Column("allowed_anonymous_enlisting")]
    public bool AllowedAnonymousEnlisting { get; set; }

    [Column("allow_anonymous_streaming")]
    public bool AllowAnonymousStreaming { get; set; }

    [Column("searchable")]
    public bool Searchable { get; set; }

    [Column("title")]
    [MaxLength(200)]
    public string? Title { get; set; }

    [Column("description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    // New enlistment fields
    [Column("slug")]
    [MaxLength(64)]
    public string? Slug { get; set; }

    [Column("max_participants")]
    public int? MaxParticipants { get; set; }

    [Column("require_organizer_approval")]
    public bool RequireOrganizerApproval { get; set; }

    [Column("enable_waitlist")]
    public bool EnableWaitlist { get; set; }

    [Column("consent_text")]
    [MaxLength(4000)]
    public string? ConsentText { get; set; }

    /// <summary>
    /// Generate a secure slug based on event ID, creation timestamp, and a secret.
    /// Uses HMAC-SHA256 to ensure uniqueness and prevent enumeration.
    /// Call this after the event is created (Id and CreationTimestamp are set).
    /// </summary>
    public static string GenerateSlug(int eventId, DateTime creationTimestamp, string secret)
    {
        var payload = $"{eventId}|{creationTimestamp:O}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        
        // Use Base64Url encoding (URL-safe, no padding)
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "")
            .Substring(0, 16); // Take first 16 chars for brevity
    }
}
