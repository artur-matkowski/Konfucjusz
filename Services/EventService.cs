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
}
