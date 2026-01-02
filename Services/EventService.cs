using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class EventService
{
    protected readonly ApplicationDbContext _db;
    private readonly string _slugSecret;
    private readonly ILogger<EventService> _logger;

    /// <summary>
    /// Initialize EventService with database context and slug generation secret.
    /// </summary>
    /// <param name="db">Database context</param>
    /// <param name="slugSecret">Secret for generating secure event slugs</param>
    /// <param name="logger">Logger for tracking service operations</param>
    public EventService(ApplicationDbContext db, string slugSecret, ILogger<EventService> logger)
    {
        _db = db;
        _slugSecret = slugSecret;
        _logger = logger;
    }

    /// <summary>
    /// Check if a user can manage a specific event (either Administrator or assigned organizer).
    /// </summary>
    public async Task<bool> CanUserManageEventAsync(int userId, int eventId, string userRole)
    {
        // Administrators can manage all events
        if (userRole.Contains("Administrator"))
            return true;

        // Check if user is assigned as organizer for this event
        return await _db.eventOrganizers
            .AnyAsync(eo => eo.UserId == userId && eo.EventId == eventId);
    }

    /// <summary>
    /// Get all events a user can manage (admins see all, organizers see only their assigned events).
    /// </summary>
    public async Task<List<Event>> GetEventsForUserAsync(int userId, string userRole)
    {
        if (userRole.Contains("Administrator"))
        {
            // Admin sees all events
            return await _db.events.OrderBy(e => e.EventStartDate).ToListAsync();
        }

        // Organizer sees only events they're assigned to
        var eventIds = await _db.eventOrganizers
            .Where(eo => eo.UserId == userId)
            .Select(eo => eo.EventId)
            .ToListAsync();

        return await _db.events
            .Where(e => eventIds.Contains(e.Id))
            .OrderBy(e => e.EventStartDate)
            .ToListAsync();
    }

    /// <summary>
    /// Get all organizers for a specific event.
    /// </summary>
    public async Task<List<UserAccount>> GetOrganizersForEventAsync(int eventId)
    {
        return await _db.eventOrganizers
            .Where(eo => eo.EventId == eventId)
            .Include(eo => eo.User)
            .Select(eo => eo.User!)
            .ToListAsync();
    }

    /// <summary>
    /// Add a user as organizer for an event (by email).
    /// Returns true if successful, false if user not found or already an organizer.
    /// </summary>
    public async Task<(bool success, string message)> AddOrganizerByEmailAsync(int eventId, string email)
    {
        var user = await _db.users.FirstOrDefaultAsync(u => u.userEmail == email);
        if (user == null)
            return (false, "User with this email not found.");

        // Check if already an organizer
        var existing = await _db.eventOrganizers
            .AnyAsync(eo => eo.EventId == eventId && eo.UserId == user.Id);
        
        if (existing)
            return (false, "User is already an organizer for this event.");

        var organizer = new EventOrganizer
        {
            EventId = eventId,
            UserId = user.Id
        };

        _db.eventOrganizers.Add(organizer);
        await _db.SaveChangesAsync();
        
        return (true, "Organizer added successfully.");
    }

    /// <summary>
    /// Remove a user from event organizers.
    /// </summary>
    public async Task<bool> RemoveOrganizerAsync(int eventId, int userId)
    {
        var organizer = await _db.eventOrganizers
            .FirstOrDefaultAsync(eo => eo.EventId == eventId && eo.UserId == userId);
        
        if (organizer == null)
            return false;

        _db.eventOrganizers.Remove(organizer);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Create a new event and optionally add the creator as first organizer.
    /// </summary>
    public async Task<Event> CreateEventAsync(Event newEvent, int? creatorUserId = null)
    {
        _db.events.Add(newEvent);
        await _db.SaveChangesAsync();

        // If creator is provided, add them as the first organizer
        if (creatorUserId.HasValue)
        {
            var organizer = new EventOrganizer
            {
                EventId = newEvent.Id,
                UserId = creatorUserId.Value
            };
            _db.eventOrganizers.Add(organizer);
            await _db.SaveChangesAsync();
        }

        return newEvent;
    }

    /// <summary>
    /// Create a new event with automatic slug generation and optionally add the creator as first organizer.
    /// This method ensures the event has a valid enlistment slug immediately after creation.
    /// </summary>
    /// <param name="newEvent">Event to create</param>
    /// <param name="creatorUserId">Optional user ID to add as first organizer</param>
    /// <returns>Created event with generated slug</returns>
    public async Task<Event> CreateEventWithSlugAsync(Event newEvent, int? creatorUserId = null)
    {
        _logger.LogInformation("=== CreateEventWithSlugAsync START ===");
        _logger.LogInformation("Event Title: {Title}, Description: {Description}", newEvent.Title, newEvent.Description);
        _logger.LogInformation("CreatorUserId: {CreatorUserId}", creatorUserId?.ToString() ?? "null");
        
        _db.events.Add(newEvent);
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Event saved to database. Event ID: {EventId}, CreationTimestamp: {CreationTimestamp}", 
            newEvent.Id, newEvent.CreationTimestamp);

        // Generate slug after ID is assigned by database
        if (string.IsNullOrEmpty(newEvent.Slug))
        {
            _logger.LogInformation("Slug is empty, generating new slug...");
            
            // Ensure CreationTimestamp has a value
            if (newEvent.CreationTimestamp == default)
            {
                newEvent.CreationTimestamp = DateTime.UtcNow;
                _logger.LogWarning("CreationTimestamp was default, set to UtcNow: {Timestamp}", newEvent.CreationTimestamp);
            }

            newEvent.Slug = Event.GenerateSlug(newEvent.Id, newEvent.CreationTimestamp, _slugSecret);
            _logger.LogInformation("Generated slug: {Slug}", newEvent.Slug);
            
            _db.events.Update(newEvent);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Slug saved to database successfully");
        }
        else
        {
            _logger.LogInformation("Event already has slug: {Slug}", newEvent.Slug);
        }

        // Add creator as organizer
        if (creatorUserId.HasValue)
        {
            _logger.LogInformation("Adding creator {UserId} as organizer", creatorUserId.Value);
            _db.eventOrganizers.Add(new EventOrganizer 
            { 
                EventId = newEvent.Id, 
                UserId = creatorUserId.Value 
            });
            await _db.SaveChangesAsync();
            _logger.LogInformation("Creator added as organizer successfully");
        }

        _logger.LogInformation("=== CreateEventWithSlugAsync END === Event ID: {EventId}, Slug: {Slug}", 
            newEvent.Id, newEvent.Slug);
        return newEvent;
    }

    /// <summary>
    /// Ensure an existing event has a slug. Generates one if missing.
    /// Useful for backfilling slugs on existing events or after save operations.
    /// </summary>
    /// <param name="evt">Event to check and update</param>
    /// <returns>True if slug was generated, false if already existed</returns>
    public async Task<bool> EnsureSlugAsync(Event evt)
    {
        _logger.LogInformation("=== EnsureSlugAsync START === Event ID: {EventId}, Current Slug: {Slug}", 
            evt.Id, evt.Slug ?? "(null)");
            
        if (string.IsNullOrEmpty(evt.Slug))
        {
            _logger.LogInformation("Slug is missing, generating...");
            
            // Ensure CreationTimestamp has a value
            if (evt.CreationTimestamp == default)
            {
                evt.CreationTimestamp = DateTime.UtcNow;
                _logger.LogWarning("CreationTimestamp was default, set to UtcNow: {Timestamp}", evt.CreationTimestamp);
            }

            evt.Slug = Event.GenerateSlug(evt.Id, evt.CreationTimestamp, _slugSecret);
            _logger.LogInformation("Generated slug: {Slug}", evt.Slug);
            
            _db.events.Update(evt);
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("=== EnsureSlugAsync END === Slug generated and saved: {Slug}", evt.Slug);
            return true;
        }
        
        _logger.LogInformation("=== EnsureSlugAsync END === Slug already exists: {Slug}", evt.Slug);
        return false;
    }

    /// <summary>
    /// Delete an event and all associated data including recordings, participants, and organizers.
    /// This operation removes recording files from disk to free up space.
    /// </summary>
    /// <param name="eventId">ID of event to delete</param>
    /// <param name="recordingsBasePath">Base path where recordings are stored (default: /app/recordings)</param>
    /// <returns>Tuple containing success status, message, and count of deleted recording files</returns>
    public async Task<(bool success, string message, int deletedRecordingFiles)> DeleteEventWithCleanupAsync(int eventId, string recordingsBasePath = "/app/recordings")
    {
        _logger.LogInformation("=== DeleteEventWithCleanupAsync START === Event ID: {EventId}", eventId);
        
        try
        {
            var evt = await _db.events.FindAsync(eventId);
            if (evt == null)
            {
                _logger.LogWarning("Event not found: {EventId}", eventId);
                return (false, "Event not found.", 0);
            }

            _logger.LogInformation("Deleting event: {Title} (ID: {EventId})", evt.Title, eventId);

            // Get recordings before deletion to clean up files
            var recordings = await _db.eventRecordings
                .Where(r => r.EventId == eventId)
                .ToListAsync();

            _logger.LogInformation("Found {Count} recordings to delete", recordings.Count);

            int deletedFiles = 0;
            foreach (var recording in recordings)
            {
                try
                {
                    var filePath = Path.Combine(recordingsBasePath, recording.FileName);
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
                        
                        File.Delete(filePath);
                        deletedFiles++;
                        
                        _logger.LogInformation("Deleted recording file: {FileName} ({Size:F2} MB)", 
                            recording.FileName, fileSizeMB);
                    }
                    else
                    {
                        _logger.LogWarning("Recording file not found (already deleted?): {FileName}", 
                            recording.FileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to delete recording file {FileName}: {Error}", 
                        recording.FileName, ex.Message);
                }
            }

            // Get counts of related data
            var organizersCount = await _db.eventOrganizers.CountAsync(eo => eo.EventId == eventId);
            var participantsCount = await _db.eventParticipants.CountAsync(ep => ep.EventId == eventId);

            _logger.LogInformation("Deleting {OrgCount} organizers, {PartCount} participants, {RecCount} recording records",
                organizersCount, participantsCount, recordings.Count);

            // Delete event (cascade will handle organizers, participants, recordings)
            _db.events.Remove(evt);
            await _db.SaveChangesAsync();

            var message = $"Event '{evt.Title}' deleted successfully. " +
                         $"Removed: {organizersCount} organizer(s), {participantsCount} participant(s), " +
                         $"{recordings.Count} recording(s) ({deletedFiles} file(s) deleted from disk).";

            _logger.LogInformation("=== DeleteEventWithCleanupAsync END === Success: {Message}", message);
            return (true, message, deletedFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError("=== DeleteEventWithCleanupAsync ERROR === Event ID: {EventId}, Error: {Error}", 
                eventId, ex.Message);
            return (false, $"Error deleting event: {ex.Message}", 0);
        }
    }

    /// <summary>
    /// Delete multiple events with cleanup of recording files.
    /// Processes each event individually and continues on errors to maximize deletion success.
    /// </summary>
    /// <param name="eventIds">Collection of event IDs to delete</param>
    /// <param name="recordingsBasePath">Base path where recordings are stored (default: /app/recordings)</param>
    /// <returns>Tuple containing success count, failure count, total deleted files, and summary message</returns>
    public async Task<(int successCount, int failureCount, int totalDeletedFiles, string message)> 
        DeleteMultipleEventsWithCleanupAsync(IEnumerable<int> eventIds, string recordingsBasePath = "/app/recordings")
    {
        _logger.LogInformation("=== DeleteMultipleEventsWithCleanupAsync START === Event IDs: [{EventIds}]", 
            string.Join(", ", eventIds));

        int successCount = 0;
        int failureCount = 0;
        int totalDeletedFiles = 0;
        var failedEvents = new List<string>();

        foreach (var eventId in eventIds)
        {
            var (success, message, deletedFiles) = await DeleteEventWithCleanupAsync(eventId, recordingsBasePath);
            
            if (success)
            {
                successCount++;
                totalDeletedFiles += deletedFiles;
                _logger.LogInformation("Successfully deleted event {EventId}", eventId);
            }
            else
            {
                failureCount++;
                failedEvents.Add($"Event {eventId}: {message}");
                _logger.LogWarning("Failed to delete event {EventId}: {Message}", eventId, message);
            }
        }

        var summaryMessage = $"Bulk deletion completed: {successCount} event(s) deleted successfully";
        
        if (totalDeletedFiles > 0)
        {
            summaryMessage += $", {totalDeletedFiles} recording file(s) removed from disk";
        }
        
        if (failureCount > 0)
        {
            summaryMessage += $". {failureCount} event(s) failed: {string.Join("; ", failedEvents)}";
        }
        else
        {
            summaryMessage += ".";
        }

        _logger.LogInformation("=== DeleteMultipleEventsWithCleanupAsync END === Success: {Success}, Failed: {Failed}, Files: {Files}", 
            successCount, failureCount, totalDeletedFiles);

        return (successCount, failureCount, totalDeletedFiles, summaryMessage);
    }
}
