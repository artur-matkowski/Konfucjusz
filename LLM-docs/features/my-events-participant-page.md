# Feature: "My Events" Participant Page

**Created:** 2026-01-03  
**Status:** ✅ Deployed (Awaiting User Testing)  
**Route:** `/my-events`  
**Component:** `Components/Pages/Events/MyParticipations.razor`

---

## Overview

The "My Events" page provides regular users (participants) with a centralized view of all events they've enrolled in, with quick access to stream links and recordings.

### Problem Solved

Before this feature:
- Regular users had no way to view their enrolled events
- No easy access to stream links after enrollment
- No centralized participant experience
- EventList.razor was restricted to Administrators/Organizers only

After this feature:
- Users see all their participations in one place
- Status badges show enrollment state (Confirmed, Waitlisted, Pending)
- Quick access buttons for stream and recordings
- Cancel participation functionality with confirmation

---

## User Experience

### Navigation
- **Menu Item:** "My Events" (calendar-check icon)
- **Visible to:** All authenticated users (User, Administrator, Organizer roles)
- **Location:** Navigation menu between other items and "My Account"

### Page Layout

**Header:**
```
My Events
────────────────────────────────────────
```

**Empty State:**
```
ℹ️ You haven't enrolled in any events yet.
```

**Event Cards (when enrolled):**
```
┌─────────────────────────────────────────┐
│ Event Title                             │
│ 📅 Dec 31, 2025 10:00 AM - 12:00 PM   │
│ 📍 Location or "Online Event"          │
│                                         │
│ Status: [Confirmed] or [Waitlisted]    │
│                                         │
│ [Join Stream]  [View Recordings]       │
│ [Cancel Participation]                  │
└─────────────────────────────────────────┘
```

### Status Badges

- **Confirmed** - Green badge (`badge bg-success`)
- **Waitlisted** - Yellow badge (`badge bg-warning`)
- **PendingApproval** - Blue badge (`badge bg-info`)
- **Other** - Gray badge (`badge bg-secondary`)

### Action Buttons

1. **Join Stream** (Primary blue button)
   - Visible for: All confirmed participants
   - Action: Navigate to `/e/{slug}/stream`
   - Tooltip: "Access live audio stream"

2. **View Recordings** (Secondary outline button)
   - Visible for: All participants
   - Action: Navigate to `/e/{slug}/recordings`
   - Tooltip: "View past recordings"

3. **Cancel Participation** (Danger outline button)
   - Visible for: All participants
   - Action: Show confirmation modal
   - Requires: Typing event slug to confirm
   - Result: Removes participation, shows success message

---

## Technical Implementation

### Route & Authorization
```razor
@page "/my-events"
@rendermode InteractiveServer
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize(Roles = "User,Administrator,Organizer")]
```

### User ID Resolution Pattern

Uses the same claim resolution pattern established in Session 003:

```csharp
// Try multiple claim types to find email address
var emailClaim = user.FindFirst(ClaimTypes.Email) 
              ?? user.FindFirst("email") 
              ?? user.FindFirst("preferred_username")
              ?? user.FindFirst(ClaimTypes.Name);  // THIS ONE WORKS IN OUR SYSTEM

if (emailClaim != null)
{
    var userAccount = await DbContext.users
        .FirstOrDefaultAsync(u => u.userEmail == emailClaim.Value);
    
    if (userAccount != null)
    {
        currentUserId = userAccount.Id;
        Console.WriteLine($"[MyParticipations] User ID resolved: {currentUserId}");
    }
}
```

### Data Query

Retrieves all participations for current user:

```csharp
myParticipations = await DbContext.eventParticipants
    .Include(ep => ep.Event)
    .Where(ep => ep.UserId == currentUserId)
    .OrderByDescending(ep => ep.Event.EventStartDate)
    .ToListAsync();
```

**Query Details:**
- Includes related `Event` entity (eager loading)
- Filters by current user ID
- Orders by event start date (newest first)
- Returns all enrollment statuses

### Cancel Participation Flow

1. User clicks "Cancel Participation"
2. Modal appears with:
   - Event title display
   - Text input for slug confirmation
   - Warning message
3. User must type exact event slug
4. OnConfirmCancel called:
   ```csharp
   - Remove participant record from database
   - Remove from UI list
   - Show success message
   ```

---

## Database Schema

### Tables Used

**event_participants:**
- `id` (Primary Key)
- `event_id` (Foreign Key → events.id)
- `user_id` (Foreign Key → user_account.id)
- `status` (enum: Confirmed, Waitlisted, PendingApproval, Cancelled)
- `enlist_time` (timestamp)
- `is_anonymous` (boolean)
- `guest_name` (string, nullable)
- `guest_email` (string, nullable)

**events:**
- `id` (Primary Key)
- `title` (string)
- `slug` (string, unique)
- `event_start_date` (timestamp)
- `event_end_date` (timestamp)
- `location` (string, nullable)
- (other event fields...)

### Query Performance

- **Index needed:** `event_participants.user_id` (for WHERE clause)
- **Include optimization:** Uses EF Core `.Include()` for single query
- **Load time:** Depends on number of user participations (typically <100ms)

---

## Logging

All operations logged with `[MyParticipations]` prefix:

```
[MyParticipations] OnInitializedAsync started
[MyParticipations] User is authenticated
[MyParticipations] User ID resolved: 4
[MyParticipations] Found 3 participations for user 4
[MyParticipations] Cancel participation requested for event: test-event-2026
[MyParticipations] Cancellation confirmed
[MyParticipations] Successfully cancelled participation for event 42
```

**Log Locations:**
- Docker container: `docker logs konfucjusz_app --tail 100`
- Follows same logging pattern as StreamBroadcast and Enlist pages

---

## Navigation Menu Integration

### Location
`Components/Layout/NavMenu.razor` (lines 52-60)

### Implementation
```razor
<AuthorizeView Roles="User,Administrator,Organizer">
    <Authorized>
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="my-events">
                <span class="bi bi-calendar-check" aria-hidden="true"></span> My Events
            </NavLink>
        </div>
    </Authorized>
</AuthorizeView>
```

**Icon:** `bi-calendar-check` (Bootstrap Icons)  
**Active State:** NavLink automatically highlights when on `/my-events` route

---

## Related Components

### Dependencies

1. **StreamListen.razor** (`/e/{slug}/stream`)
   - Target of "Join Stream" button
   - Handles audio streaming via SignalR
   - Uses same authentication pattern

2. **EventRecordings.razor** (`/e/{slug}/recordings`)
   - Target of "View Recordings" button
   - Lists past recordings for event
   - Allows playback and download

3. **ParticipantService.cs**
   - Could be used for cancellation logic (currently direct DB access)
   - Future enhancement opportunity

### Component Hierarchy

```
NavMenu.razor
  ├─ NavLink href="my-events"
  │
MyParticipations.razor
  ├─ AuthorizeView (role check)
  ├─ Loading state
  ├─ Empty state
  ├─ Event cards (foreach)
  │   ├─ Event details
  │   ├─ Status badge
  │   ├─ Action buttons
  │   │   ├─ GoToStream() → /e/{slug}/stream
  │   │   ├─ GoToRecordings() → /e/{slug}/recordings
  │   │   └─ ShowCancelModal() → ConfirmTypedDialog
  │   │
  └─ ConfirmTypedDialog (cancel confirmation)
```

---

## Testing Checklist

### Manual Testing Required

1. **Page Access:**
   - [ ] Login as regular User
   - [ ] Navigate to `/my-events` via menu
   - [ ] Page loads without errors

2. **Display Validation:**
   - [ ] Empty state shows when no enrollments
   - [ ] Event cards display after enrollment
   - [ ] Status badges show correct colors
   - [ ] Event details display correctly (title, dates, location)

3. **Button Functionality:**
   - [ ] "Join Stream" navigates to `/e/{slug}/stream`
   - [ ] "View Recordings" navigates to `/e/{slug}/recordings`
   - [ ] "Cancel Participation" shows modal

4. **Cancellation Flow:**
   - [ ] Modal appears with event details
   - [ ] Confirm button disabled until slug typed
   - [ ] Typing wrong slug keeps button disabled
   - [ ] Typing correct slug enables button
   - [ ] Clicking cancel closes modal without action
   - [ ] Clicking confirm removes participation
   - [ ] Success message appears
   - [ ] Event card disappears from list
   - [ ] Database record deleted

5. **Error Scenarios:**
   - [ ] Anonymous user redirected to login
   - [ ] User with no participations sees empty state
   - [ ] Database errors logged and handled gracefully

6. **Logging Validation:**
   - [ ] Check Docker logs: `docker logs konfucjusz_app --tail 100`
   - [ ] Verify user ID resolution logged
   - [ ] Verify participation count logged
   - [ ] Verify cancellation operations logged

---

## Known Limitations

### Current Implementation

1. **No pagination:** All participations loaded at once
   - Acceptable for typical users (<100 enrollments)
   - Could be slow for power users with 500+ enrollments

2. **No filtering/sorting UI:**
   - Only sorted by event start date (descending)
   - No filter by status (Confirmed/Waitlisted/Pending)
   - No search by event name

3. **No "past events" separation:**
   - Shows all events (past, present, future)
   - Could add tab navigation or separate sections

4. **Stream button always visible:**
   - Shows even for past events
   - Could hide for events that ended >24 hours ago

### Security Considerations

✅ **Authorization:**
- Page requires authentication
- User ID validated from claims
- Database queries filtered by user ID

✅ **Cancellation:**
- Requires typing event slug (prevents accidental clicks)
- Only removes user's own participation (no privilege escalation)

⚠️ **Potential Issues:**
- No rate limiting on cancellation
- No audit trail of cancellations
- Direct database access (bypasses service layer)

---

## Future Enhancements

### Priority: High
- [ ] Add "Stream Starting Soon" indicator for events <1 hour away
- [ ] Show countdown timer for upcoming events
- [ ] Hide "Join Stream" for past events

### Priority: Medium
- [ ] Add tabs: "Upcoming" | "Past" | "All"
- [ ] Add status filter dropdown
- [ ] Add search/filter by event name
- [ ] Add pagination (50 events per page)
- [ ] Move cancellation logic to ParticipantService

### Priority: Low
- [ ] Add calendar export (.ics download)
- [ ] Add "Share" button for events
- [ ] Show organizer contact info
- [ ] Add participant count and capacity info
- [ ] Add event description preview

---

## Deployment History

**Initial Deployment:**
- Date: 2026-01-03
- Commit: (pending)
- Build: Successful
- Docker Image: `ghcr.io/artur-matkowski/konfucjusz:dev`
- Container: `konfucjusz_app` (restarted)

**Files Created/Modified:**
1. ✅ Created: `Components/Pages/Events/MyParticipations.razor` (380 lines)
2. ✅ Modified: `Components/Layout/NavMenu.razor` (added menu item)

**Build Output:**
```
✅ Build: Successful
⚠️  Warnings: 3 (pre-existing)
❌ Errors: 0
⏱️  Build Time: ~5 seconds
🐳 Docker Build: ~13 seconds
```

---

## Support & Troubleshooting

### Common Issues

**Issue: Page shows "Loading..." forever**
- Check Docker logs for database connection errors
- Verify user is authenticated (check network panel)
- Check browser console for JavaScript errors

**Issue: No events showing despite enrollment**
- Check database: `SELECT * FROM event_participants WHERE user_id = X;`
- Verify user ID resolution in logs
- Check for database migration issues

**Issue: "Join Stream" goes to 404**
- Verify event has slug: `SELECT slug FROM events WHERE id = X;`
- Check StreamListen.razor route is `/e/{slug}/stream`
- Verify slug parameter is being passed correctly

**Issue: Cancel confirmation not working**
- Verify slug is typed exactly (case-sensitive)
- Check JavaScript console for errors
- Check modal component is included

### Debug Commands

```bash
# Check application logs
docker logs konfucjusz_app --tail 100

# Check database participations
docker exec konfucjusz_db psql -U konfucjusz -d konfucjusz_db -c \
  "SELECT ep.id, ep.status, e.title, e.slug, u.user_email 
   FROM event_participants ep 
   JOIN events e ON ep.event_id = e.id 
   JOIN user_account u ON ep.user_id = u.id 
   WHERE u.user_email = 'artur.matkowski.zan+spam3@gmail.com';"

# Rebuild and redeploy
dotnet build --no-incremental
docker build -t ghcr.io/artur-matkowski/konfucjusz:dev .
docker compose down app && docker compose up -d app
```

---

## Related Documentation

- **Session Summary:** `LLM-docs/sessions/session-003-summary.md`
- **Task Backlog:** `LLM-docs/task-backlog.md`
- **Architecture Overview:** `LLM-docs/architecture-overview.md`
- **User Issues:** `LLM-docs/user-reported-issues-2026-01-02.md`
- **Main Index:** `.project-knowledge.md`

---

**Status:** ✅ Deployed - Awaiting User Testing  
**Next Steps:** User to test functionality and provide feedback  
**Blockers:** None
