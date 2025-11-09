using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

public class ParticipantService
{
        private readonly ApplicationDbContext _context;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly IDataProtector _emailConfirmProtector;
        private readonly IDataProtector _cancelProtector;
        private readonly IDataProtector _streamProtector;
        private readonly string _slugSecret;

        public ParticipantService(ApplicationDbContext context, string slugSecret)
        {
            _context = context;
            _slugSecret = slugSecret;
            
            // Initialize data protectors for tokens
            _dataProtectionProvider = DataProtectionProvider.Create("Konfucjusz");
            _emailConfirmProtector = _dataProtectionProvider.CreateProtector("Konfucjusz.EventEnlist.EmailConfirm.v1");
            _cancelProtector = _dataProtectionProvider.CreateProtector("Konfucjusz.EventEnlist.Cancel.v1");
            _streamProtector = _dataProtectionProvider.CreateProtector("Konfucjusz.EventStream.Access.v1");
        }

        /// <summary>
        /// Generate secure slug for an event using HMAC-SHA256
        /// </summary>
        public string GenerateSlug(int eventId, DateTime creationTimestamp)
        {
            return Event.GenerateSlug(eventId, creationTimestamp, _slugSecret);
        }

        /// <summary>
        /// Check if a user (logged-in or anonymous by email) is already enlisted
        /// </summary>
        public async Task<EventParticipant?> GetActiveParticipationAsync(int eventId, int? userId, string? email)
        {
            var query = _context.eventParticipants
                .Where(ep => ep.EventId == eventId && 
                            ParticipantStatus.ActiveStatuses.Contains(ep.Status));

            if (userId.HasValue)
            {
                query = query.Where(ep => ep.UserId == userId.Value);
            }
            else if (!string.IsNullOrEmpty(email))
            {
                var normalizedEmail = email.ToLowerInvariant();
                query = query.Where(ep => ep.NormalizedEmail == normalizedEmail && ep.UserId == null);
            }
            else
            {
                return null;
            }

            return await query.FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get participant counts for an event
        /// </summary>
        public async Task<ParticipantCounts> GetParticipantCountsAsync(int eventId)
        {
            var participants = await _context.eventParticipants
                .Where(ep => ep.EventId == eventId)
                .ToListAsync();

            return new ParticipantCounts
            {
                Confirmed = participants.Count(p => p.Status == ParticipantStatus.Confirmed),
                Waitlisted = participants.Count(p => p.Status == ParticipantStatus.Waitlisted),
                PendingEmail = participants.Count(p => p.Status == ParticipantStatus.PendingEmail),
                PendingApproval = participants.Count(p => p.Status == ParticipantStatus.PendingApproval),
                Declined = participants.Count(p => p.Status == ParticipantStatus.Declined),
                Cancelled = participants.Count(p => p.Status == ParticipantStatus.Cancelled),
                Removed = participants.Count(p => p.Status == ParticipantStatus.Removed)
            };
        }

        /// <summary>
        /// Enlist a logged-in user to an event
        /// </summary>
        public async Task<EnlistResult> EnlistLoggedInUserAsync(int eventId, int userId, Event eventDetails)
        {
            // Check if already enlisted
            var existing = await GetActiveParticipationAsync(eventId, userId, null);
            if (existing != null)
            {
                return new EnlistResult { Success = false, Message = "Already enlisted", Participant = existing };
            }

            // Get current counts
            var counts = await GetParticipantCountsAsync(eventId);
            var currentConfirmed = counts.Confirmed;

            // Determine status based on capacity and approval requirements
            string status;
            if (eventDetails.RequireOrganizerApproval)
            {
                status = ParticipantStatus.PendingApproval;
            }
            else if (eventDetails.MaxParticipants.HasValue && currentConfirmed >= eventDetails.MaxParticipants.Value)
            {
                status = eventDetails.EnableWaitlist ? ParticipantStatus.Waitlisted : ParticipantStatus.Declined;
                if (!eventDetails.EnableWaitlist)
                {
                    return new EnlistResult { Success = false, Message = "Event is full and waitlist is disabled" };
                }
            }
            else
            {
                status = ParticipantStatus.Confirmed;
            }

            var participant = new EventParticipant
            {
                EventId = eventId,
                UserId = userId,
                IsAnonymous = false,
                EmailConfirmed = true,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.eventParticipants.Add(participant);
            await _context.SaveChangesAsync();

            return new EnlistResult { Success = true, Participant = participant };
        }

        /// <summary>
        /// Enlist an anonymous user (always requires email confirmation)
        /// </summary>
        public async Task<EnlistResult> EnlistAnonymousUserAsync(
            int eventId, 
            string name, 
            string surname, 
            string email, 
            Event eventDetails,
            string consentText)
        {
            var normalizedEmail = email.ToLowerInvariant();

            // Check if already enlisted
            var existing = await GetActiveParticipationAsync(eventId, null, email);
            if (existing != null)
            {
                return new EnlistResult { Success = false, Message = "Email already enlisted", Participant = existing };
            }

            var participant = new EventParticipant
            {
                EventId = eventId,
                UserId = null,
                Name = name,
                Surname = surname,
                Email = email,
                NormalizedEmail = normalizedEmail,
                IsAnonymous = true,
                EmailConfirmed = false,
                Status = ParticipantStatus.PendingEmail,
                ConsentGiven = true,
                ConsentGivenAt = DateTime.UtcNow,
                ConsentTextSnapshot = consentText,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.eventParticipants.Add(participant);
            await _context.SaveChangesAsync();

            return new EnlistResult { Success = true, Participant = participant, RequiresEmailConfirmation = true };
        }

        /// <summary>
        /// Generate email confirmation token (24h expiry)
        /// </summary>
        public string GenerateEmailConfirmToken(int participantId, int eventId, string email)
        {
            var nonce = Guid.NewGuid().ToString("N");
            var issuedAt = DateTime.UtcNow.Ticks;
            var payload = $"{participantId}|{eventId}|{email}|{nonce}|{issuedAt}";
            return _emailConfirmProtector.Protect(payload);
        }

        /// <summary>
        /// Validate and parse email confirmation token
        /// </summary>
        public bool TryParseEmailConfirmToken(string token, out int participantId, out int eventId, out string email)
        {
            participantId = 0;
            eventId = 0;
            email = string.Empty;

            try
            {
                var payload = _emailConfirmProtector.Unprotect(token);
                var parts = payload.Split('|');
                if (parts.Length != 5)
                    return false;

                participantId = int.Parse(parts[0]);
                eventId = int.Parse(parts[1]);
                email = parts[2];
                var issuedAtTicks = long.Parse(parts[4]);
                var issuedAt = new DateTime(issuedAtTicks, DateTimeKind.Utc);

                // Check 24h expiry
                if (DateTime.UtcNow - issuedAt > TimeSpan.FromHours(24))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Confirm anonymous user's email and update status
        /// </summary>
        public async Task<bool> ConfirmAnonymousEmailAsync(int participantId, int eventId, string email)
        {
            var participant = await _context.eventParticipants
                .Include(ep => ep.Event)
                .FirstOrDefaultAsync(ep => ep.Id == participantId && ep.EventId == eventId);

            if (participant == null || participant.EmailConfirmed || 
                participant.NormalizedEmail != email.ToLowerInvariant())
                return false;

            participant.EmailConfirmed = true;

            // Determine next status based on event settings
            var counts = await GetParticipantCountsAsync(eventId);
            var currentConfirmed = counts.Confirmed;

            if (participant.Event!.RequireOrganizerApproval)
            {
                participant.Status = ParticipantStatus.PendingApproval;
            }
            else if (participant.Event.MaxParticipants.HasValue && 
                     currentConfirmed >= participant.Event.MaxParticipants.Value)
            {
                participant.Status = participant.Event.EnableWaitlist 
                    ? ParticipantStatus.Waitlisted 
                    : ParticipantStatus.Declined;
            }
            else
            {
                participant.Status = ParticipantStatus.Confirmed;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Generate cancellation token (no expiry, single-use enforced in handler)
        /// </summary>
        public string GenerateCancelToken(int participantId, int eventId, string email)
        {
            var nonce = Guid.NewGuid().ToString("N");
            var payload = $"{participantId}|{eventId}|{email}|{nonce}";
            return _cancelProtector.Protect(payload);
        }

        /// <summary>
        /// Validate and parse cancellation token
        /// </summary>
        public bool TryParseCancelToken(string token, out int participantId, out int eventId, out string email)
        {
            participantId = 0;
            eventId = 0;
            email = string.Empty;

            try
            {
                var payload = _cancelProtector.Unprotect(token);
                var parts = payload.Split('|');
                if (parts.Length != 4)
                    return false;

                participantId = int.Parse(parts[0]);
                eventId = int.Parse(parts[1]);
                email = parts[2];

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Cancel participation (logged-in user or via token for anonymous)
        /// For anonymous users, purge all data immediately
        /// </summary>
        public async Task<bool> CancelParticipationAsync(int participantId, int eventId)
        {
            var participant = await _context.eventParticipants
                .Include(ep => ep.Event)
                .FirstOrDefaultAsync(ep => ep.Id == participantId && ep.EventId == eventId);

            if (participant == null)
                return false;

            if (participant.IsAnonymous)
            {
                // Purge anonymous data immediately
                _context.eventParticipants.Remove(participant);
            }
            else
            {
                // Mark as cancelled for logged-in users
                participant.Status = ParticipantStatus.Cancelled;
            }

            await _context.SaveChangesAsync();

            // Promote next waitlisted if space opened and was confirmed
            if (participant.Status == ParticipantStatus.Confirmed || participant.IsAnonymous)
            {
                await PromoteNextFromWaitlistAsync(eventId);
            }

            return true;
        }

        /// <summary>
        /// Organizer declines a participant
        /// </summary>
        public async Task<bool> DeclineParticipantAsync(int participantId)
        {
            var participant = await _context.eventParticipants
                .FirstOrDefaultAsync(ep => ep.Id == participantId);

            if (participant == null)
                return false;

            participant.Status = ParticipantStatus.Declined;
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Organizer approves a pending participant
        /// </summary>
        public async Task<bool> ApproveParticipantAsync(int participantId)
        {
            var participant = await _context.eventParticipants
                .Include(ep => ep.Event)
                .FirstOrDefaultAsync(ep => ep.Id == participantId && 
                                          ep.Status == ParticipantStatus.PendingApproval);

            if (participant == null)
                return false;

            // Check capacity
            var counts = await GetParticipantCountsAsync(participant.EventId);
            var currentConfirmed = counts.Confirmed;

            if (participant.Event!.MaxParticipants.HasValue && 
                currentConfirmed >= participant.Event.MaxParticipants.Value)
            {
                participant.Status = participant.Event.EnableWaitlist 
                    ? ParticipantStatus.Waitlisted 
                    : ParticipantStatus.Declined;
            }
            else
            {
                participant.Status = ParticipantStatus.Confirmed;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Organizer removes a participant
        /// </summary>
        public async Task<bool> RemoveParticipantAsync(int participantId)
        {
            var participant = await _context.eventParticipants
                .FirstOrDefaultAsync(ep => ep.Id == participantId);

            if (participant == null)
                return false;

            var wasConfirmed = participant.Status == ParticipantStatus.Confirmed;

            if (participant.IsAnonymous)
            {
                // Purge anonymous data
                _context.eventParticipants.Remove(participant);
            }
            else
            {
                participant.Status = ParticipantStatus.Removed;
            }

            await _context.SaveChangesAsync();

            // Promote next waitlisted if space opened
            if (wasConfirmed)
            {
                await PromoteNextFromWaitlistAsync(participant.EventId);
            }

            return true;
        }

        /// <summary>
        /// Promote next participant from waitlist (FIFO)
        /// </summary>
        public async Task<EventParticipant?> PromoteNextFromWaitlistAsync(int eventId)
        {
            var nextWaitlisted = await _context.eventParticipants
                .Where(ep => ep.EventId == eventId && ep.Status == ParticipantStatus.Waitlisted)
                .OrderBy(ep => ep.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextWaitlisted == null)
                return null;

            nextWaitlisted.Status = ParticipantStatus.Confirmed;
            await _context.SaveChangesAsync();

            return nextWaitlisted;
        }

        /// <summary>
        /// Get all participants for an event with optional filtering
        /// </summary>
        public async Task<List<EventParticipant>> GetParticipantsForEventAsync(
            int eventId, 
            string? statusFilter = null)
        {
            var query = _context.eventParticipants
                .Include(ep => ep.User)
                .Where(ep => ep.EventId == eventId);

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(ep => ep.Status == statusFilter);
            }

            return await query
                .OrderBy(ep => ep.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Purge unconfirmed anonymous participants older than 24 hours
        /// </summary>
        public async Task<int> PurgeUnconfirmedParticipantsAsync()
        {
            var cutoff = DateTime.UtcNow.AddHours(-24);
            
            var unconfirmed = await _context.eventParticipants
                .Where(ep => ep.IsAnonymous && 
                            !ep.EmailConfirmed && 
                            ep.Status == ParticipantStatus.PendingEmail &&
                            ep.CreatedAt < cutoff)
                .ToListAsync();

            _context.eventParticipants.RemoveRange(unconfirmed);
            await _context.SaveChangesAsync();

            return unconfirmed.Count;
        }

        /// <summary>
        /// Generate stream access token for a participant (no expiry)
        /// </summary>
        public string GenerateStreamToken(int eventId, int? participantId, string? email)
        {
            var nonce = Guid.NewGuid().ToString("N");
            var payload = $"{eventId}|{participantId}|{email}|{nonce}";
            return _streamProtector.Protect(payload);
        }

        /// <summary>
        /// Validate and parse stream access token
        /// </summary>
        public bool TryParseStreamToken(string token, out int eventId, out int? participantId, out string? email)
        {
            eventId = 0;
            participantId = null;
            email = null;

            try
            {
                var payload = _streamProtector.Unprotect(token);
                var parts = payload.Split('|');
                if (parts.Length != 4)
                    return false;

                eventId = int.Parse(parts[0]);
                if (!string.IsNullOrEmpty(parts[1]) && int.TryParse(parts[1], out var pid))
                    participantId = pid;
                email = parts[2];

                return true;
            }
            catch
            {
                return false;
            }
        }
}

public class EnlistResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public EventParticipant? Participant { get; set; }
    public bool RequiresEmailConfirmation { get; set; }
}

public class ParticipantCounts
{
    public int Confirmed { get; set; }
    public int Waitlisted { get; set; }
    public int PendingEmail { get; set; }
    public int PendingApproval { get; set; }
    public int Declined { get; set; }
    public int Cancelled { get; set; }
    public int Removed { get; set; }
}
