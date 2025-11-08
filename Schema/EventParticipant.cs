using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("event_participants")]
public class EventParticipant
{
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("event_id")]
        public int EventId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        // For anonymous participants only (nullable for logged-in)
        [Column("name")]
        [MaxLength(100)]
        public string? Name { get; set; }

        [Column("surname")]
        [MaxLength(100)]
        public string? Surname { get; set; }

        [Column("email")]
        [MaxLength(255)]
        public string? Email { get; set; }

        [Column("normalized_email")]
        [MaxLength(255)]
        public string? NormalizedEmail { get; set; }

        [Column("email_confirmed")]
        public bool EmailConfirmed { get; set; }

        [Column("is_anonymous")]
        public bool IsAnonymous { get; set; }

        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = ParticipantStatus.PendingEmail;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("consent_given")]
        public bool ConsentGiven { get; set; }

        [Column("consent_given_at")]
        public DateTime? ConsentGivenAt { get; set; }

        [Column("consent_text_snapshot")]
        [MaxLength(4000)]
        public string? ConsentTextSnapshot { get; set; }

        // Navigation properties
        [ForeignKey("EventId")]
        public Event? Event { get; set; }

        [ForeignKey("UserId")]
        public UserAccount? User { get; set; }

        /// <summary>
        /// Get display name. For logged-in participants, derive from User; otherwise use stored values.
        /// </summary>
        public string GetDisplayName()
        {
            if (UserId.HasValue && User != null)
            {
                return $"{User.userName}";
            }
            return $"{Name} {Surname}".Trim();
        }

        /// <summary>
        /// Get display email. For logged-in participants, derive from User; otherwise use stored value.
        /// </summary>
        public string GetDisplayEmail()
        {
            if (UserId.HasValue && User != null)
            {
                return User.userEmail ?? string.Empty;
            }
            return Email ?? string.Empty;
        }
    }

    public static class ParticipantStatus
    {
        public const string PendingEmail = "PendingEmail";
        public const string PendingApproval = "PendingApproval";
        public const string Confirmed = "Confirmed";
        public const string Waitlisted = "Waitlisted";
        public const string Declined = "Declined";
        public const string Cancelled = "Cancelled";
        public const string Removed = "Removed";

        public static readonly string[] ActiveStatuses = new[]
        {
            PendingEmail,
            PendingApproval,
            Confirmed,
            Waitlisted
        };

        public static readonly string[] InactiveStatuses = new[]
        {
            Declined,
            Cancelled,
            Removed
        };
    }
