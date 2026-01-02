# User-Reported Issues & Feature Requests

**Session:** 2026-01-02  
**Status:** Documented for future sessions  

---

## Issue 1: 'Start Recording' Permission Error

### Description
When clicking "Start Recording" button, user receives error message:
> "You have no permission to access that"

### Expected Behavior
- Authorized users (Organizers, Administrators) should be able to start recording during stream broadcast
- Recording should begin without permission errors

### Investigation Starting Points

**Files to Check:**
- `Components/Pages/Events/StreamBroadcast.razor` - Button click handler
- `Hubs/AudioStreamHub.cs` - SignalR hub with recording logic
- `Services/EventService.cs` - Authorization checks (`CanUserManageEventAsync()`)

**Potential Causes:**
1. **Authorization Check Failure:**
   - User role not properly passed to hub method
   - `CanUserManageEventAsync()` returning false incorrectly
   - Missing EventOrganizer record in database

2. **Hub Authorization:**
   - SignalR hub not receiving user context
   - `[Authorize]` attribute too restrictive
   - Connection established without authentication

3. **State Issues:**
   - Event ID not passed correctly to hub method
   - User ID mismatch between session and database
   - Event not found when checking permissions

**Debug Steps:**
1. Add logging to `StreamBroadcast.razor` button click handler
2. Add logging to hub method entry point
3. Log user ID, event ID, and role being checked
4. Verify EventOrganizer record exists for user+event
5. Check SignalR connection authentication state

**Related Code Locations:**
```csharp
// StreamBroadcast.razor
private async Task StartRecording() { ... }

// AudioStreamHub.cs
public async Task StartRecording(int eventId) { ... }

// EventService.cs
public async Task<bool> CanUserManageEventAsync(int userId, int eventId, string userRole) { ... }
```

---

## Issue 2: Stream Link Not Available Before Stream Starts

### Description
Stream link is only available during active broadcast, not before the event starts.

### Expected Behavior
- Stream link should be visible/shareable before event begins
- Participants can access the link early and bookmark it
- Page shows "Stream not started yet" message when accessed early

### Investigation Starting Points

**Files to Check:**
- `Components/Pages/Events/EventEdit.razor` - Enlist link section (might need stream link section)
- `Components/Pages/Events/StreamListen.razor` - Stream listener page
- `Components/Pages/Events/EventList.razor` - Event display for participants

**Potential Solutions:**

1. **Add "Stream Link" Section to EventEdit:**
   - Similar to "Enlist Link" section
   - Generate URL: `/events/stream/listen/{eventId}` or `/events/stream/listen/{slug}`
   - Display before stream starts
   - Show QR code (see Issue 4)

2. **Update StreamListen Page:**
   - Show "waiting" state when stream not active
   - Display event details (title, start time, description)
   - Show countdown to event start
   - Auto-refresh when stream becomes available

3. **Add to Email Notifications:**
   - Include stream link in confirmation emails
   - Send reminder with stream link before event

**Related Code Locations:**
```razor
<!-- EventEdit.razor (around line 100-150, near Enlist Link section) -->
@if (!string.IsNullOrEmpty(evt?.Slug))
{
    <div class="card mb-3">
        <div class="card-header">Stream Link</div>
        <div class="card-body">
            <p>Share this link for participants to watch the stream:</p>
            <code>@streamUrl</code>
        </div>
    </div>
}
```

---

## Issue 3: 'Enlist Now' Button Does Nothing

### Description
When clicking "Enlist Now" button, nothing happens - no feedback, no enlistment.

### Expected Behavior
- Button click should enlist user in the event
- Success message should appear
- User should be redirected or see confirmation
- Participant should appear in event participants list

### Investigation Starting Points

**Files to Check:**
- `Components/Pages/Events/Enlist.razor` - Enlistment page with button
- `Services/ParticipantService.cs` - Enlistment logic
- `Schema/EventParticipant.cs` - Participant entity

**Potential Causes:**

1. **Missing Event Handler:**
   - Button `@onclick` not wired up
   - Method name typo
   - Handler method not implemented

2. **Silent JavaScript Error:**
   - Blazor interop failure
   - Form validation blocking submission
   - Exception swallowed without logging

3. **Database Issues:**
   - Duplicate participant check failing
   - Foreign key constraint violation
   - Transaction not committed

4. **Authorization Issues:**
   - User not authenticated
   - Anonymous enlistment not allowed but user is anonymous
   - Event enlistment period expired

**Debug Steps:**
1. Add console logging to button click handler
2. Check browser console for JavaScript errors
3. Add try-catch with logging in enlistment method
4. Verify database insert with SQL query
5. Check event enlistment date range
6. Test with both authenticated and anonymous users

**Related Code Locations:**
```razor
<!-- Enlist.razor -->
<button class="btn btn-primary" @onclick="EnlistNowAsync">Enlist Now</button>

@code {
    private async Task EnlistNowAsync()
    {
        // Check if this method exists and has implementation
    }
}
```

**Quick Test:**
```csharp
// Add temporary logging
private async Task EnlistNowAsync()
{
    Console.WriteLine("=== EnlistNowAsync START ===");
    try
    {
        // ... existing code
        Console.WriteLine("=== EnlistNowAsync SUCCESS ===");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"=== EnlistNowAsync ERROR: {ex.Message} ===");
    }
}
```

---

## Feature Request 4: QR Codes for Enlist and Stream Links

### Description
Add QR codes alongside enlist and stream links for easy mobile access.

### Expected Behavior
- Enlist link section shows QR code that encodes the enlistment URL
- Stream link section shows QR code for streaming URL
- Users can scan with mobile devices to quickly access pages
- QR codes are generated server-side or client-side

### Implementation Options

**Option A: Server-Side QR Generation (Recommended)**

**Library:** QRCoder (most popular .NET QR library)
```bash
dotnet add package QRCoder
```

**Service:**
```csharp
// Services/QRCodeService.cs
public class QRCodeService
{
    public string GenerateQRCodeBase64(string url)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);
        return Convert.ToBase64String(qrCodeImage);
    }
}
```

**Usage in Component:**
```razor
@inject QRCodeService QRService

<!-- In Enlist Link section -->
<div class="card mb-3">
    <div class="card-header">Enlist Link</div>
    <div class="card-body">
        <div class="row">
            <div class="col-md-8">
                <p>Share this link:</p>
                <code>@enlistUrl</code>
            </div>
            <div class="col-md-4 text-center">
                <img src="data:image/png;base64,@qrCodeBase64" alt="QR Code" class="img-fluid" style="max-width:200px;" />
                <p class="small text-muted mt-2">Scan to enlist</p>
            </div>
        </div>
    </div>
</div>
```

**Option B: Client-Side QR Generation**

**Library:** QRCode.js via JavaScript interop
- Lighter weight (no server processing)
- Generated in browser
- Requires JavaScript interop

**Option C: External QR API**

**Service:** Google Charts API, QR Code Generator API
- No dependencies
- Requires internet connection
- Privacy concerns (URLs sent to third party)
- Example: `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={url}`

### Implementation Plan

**Files to Modify:**
- `Services/QRCodeService.cs` - New service (create)
- `Program.cs` - Register QRCodeService
- `Components/Pages/Events/EventEdit.razor` - Add QR codes to Enlist Link section
- `Components/Pages/Events/EventEdit.razor` - Add QR codes to Stream Link section (if added)
- `Konfucjusz.csproj` - Add QRCoder package reference

**Steps:**
1. Install QRCoder package
2. Create QRCodeService
3. Register service in DI container
4. Inject into EventEdit component
5. Generate QR codes for enlist URL
6. Generate QR codes for stream URL (after Issue 2 resolved)
7. Style with Bootstrap cards and responsive layout
8. Test scanning with mobile devices

**Styling Considerations:**
- QR code size: 200x200 pixels (scannable but not too large)
- Center aligned below or beside URL
- Label: "Scan to enlist" / "Scan to watch stream"
- Print-friendly (black and white)
- Responsive on mobile (stack vertically)

**Error Handling:**
- Handle QR generation failures gracefully
- Show URL even if QR code fails
- Log errors for debugging

---

## Priority & Estimation

### Priority Order (Recommended)
1. **HIGH:** Issue 3 - Fix 'Enlist Now' button (blocking core functionality)
2. **HIGH:** Issue 1 - Fix 'Start Recording' permission error (blocking core functionality)
3. **MEDIUM:** Issue 2 - Stream link pre-availability (UX improvement)
4. **MEDIUM:** Feature 4 - QR codes (nice-to-have enhancement)

### Estimated Effort
- **Issue 1 (Recording permission):** 1-2 hours (debug + fix)
- **Issue 3 (Enlist button):** 1-2 hours (debug + fix)
- **Issue 2 (Stream link):** 2-3 hours (implementation + UI)
- **Feature 4 (QR codes):** 2-4 hours (library integration + UI)

### Dependencies
- Issue 2 should be completed before Feature 4 (stream QR code needs stream link)
- Issue 3 should be fixed before Feature 4 (enlist QR code needs working enlistment)

---

## Testing Checklist (For Future Implementation)

### Issue 1 Testing
- [ ] Login as Organizer of event
- [ ] Navigate to stream broadcast page
- [ ] Click "Start Recording"
- [ ] Verify recording starts without permission error
- [ ] Check recording appears in event recordings table

### Issue 3 Testing
- [ ] Navigate to enlist page for public event
- [ ] Fill in enlistment form
- [ ] Click "Enlist Now"
- [ ] Verify success message appears
- [ ] Verify participant appears in event edit page
- [ ] Test with authenticated and anonymous users

### Issue 2 Testing
- [ ] Create event with future date
- [ ] Verify stream link visible before event starts
- [ ] Access stream link URL directly
- [ ] Verify "waiting" state message shows
- [ ] Start stream broadcast
- [ ] Refresh listener page
- [ ] Verify stream plays automatically

### Feature 4 Testing
- [ ] View event edit page
- [ ] Verify QR codes appear for enlist and stream links
- [ ] Scan QR code with mobile device
- [ ] Verify correct page opens
- [ ] Test QR codes print correctly (black & white)
- [ ] Verify fallback if QR generation fails

---

## Notes for Future Sessions

**Context to Remember:**
- User is working as both Administrator and Organizer
- Events have recording functionality via SignalR (AudioStreamHub)
- Enlistment system supports both authenticated and anonymous users
- System uses event slugs for public URLs
- Docker deployment with PostgreSQL database

**Files Likely to Be Modified:**
- `Components/Pages/Events/Enlist.razor`
- `Components/Pages/Events/StreamBroadcast.razor`
- `Components/Pages/Events/StreamListen.razor`
- `Components/Pages/Events/EventEdit.razor`
- `Hubs/AudioStreamHub.cs`
- `Services/ParticipantService.cs`
- `Services/QRCodeService.cs` (new)

**Research Keywords:**
- QRCoder .NET library
- Blazor SignalR authorization
- Blazor form submission debugging
- Event participant enlistment flow

---

**Last Updated:** 2026-01-02  
**Status:** Documented and ready for future sessions  
**Author:** tdd-developer
