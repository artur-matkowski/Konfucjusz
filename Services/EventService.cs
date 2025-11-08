using Microsoft.EntityFrameworkCore;

public class EventService
{
    protected readonly ApplicationDbContext _db;

    public EventService(ApplicationDbContext db)
    {
        _db = db;
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
}
