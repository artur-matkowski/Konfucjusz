# Feature: Administrator Bulk Event Deletion

## Overview

**Feature Name:** Bulk Event Deletion for Administrators  
**Component:** EventManagement.razor (Admin-only page)  
**Status:** ✅ Implemented  
**Date:** 2026-01-02  
**Author:** tdd-developer  

---

## Purpose

Enable administrators to efficiently delete multiple events simultaneously using checkbox selection and a single confirmation dialog. This feature simplifies event management by:

- Allowing batch operations instead of one-by-one deletion
- Removing the need for typed confirmation (admin-only feature, simplified workflow)
- Automatically cleaning up all related data (organizers, participants, recordings, files)
- Providing clear feedback on deletion results

---

## User Workflow

### 1. Selection Phase
```
1. Administrator navigates to /eventManagement
2. Checkboxes appear in the first column of the events table
3. Administrator checks one or more events to delete
4. "Select All" checkbox in header toggles all events
5. Selection toolbar appears showing count: "X event(s) selected"
```

### 2. Deletion Phase
```
6. Administrator clicks "Delete Selected (X)" button
7. Simple confirmation dialog appears with warning
8. Administrator reviews what will be deleted
9. Administrator clicks "Delete X Event(s)" (single click, no typing)
10. System processes deletion with file cleanup
11. Success message shows results
12. Table refreshes automatically
13. Selection cleared
```

---

## Architecture

### Service Layer

**File:** `Services/EventService.cs`

**New Method:**
```csharp
public async Task<(int successCount, int failureCount, int totalDeletedFiles, string message)> 
    DeleteMultipleEventsWithCleanupAsync(IEnumerable<int> eventIds, string recordingsBasePath = "/app/recordings")
```

**Implementation Details:**
- Iterates through each event ID
- Calls existing `DeleteEventWithCleanupAsync()` for each (reuses code)
- Continues on errors (doesn't abort entire operation if one fails)
- Aggregates results: success count, failure count, total files deleted
- Returns comprehensive summary message
- Comprehensive logging for audit trail

**Error Handling:**
- Individual event failures don't stop the batch
- Failed events are logged with reasons
- Summary includes both successes and failures
- Recording file errors are logged but don't block database deletion

---

### UI Layer

**File:** `Components/Pages/Admin/EventManagement.razor`

**New UI Components:**

#### 1. Checkbox Column (First Column)
- Header: "Select All" checkbox
- Rows: Individual event checkboxes
- Bound to `selectedEventIds` dictionary
- Width: 40px

#### 2. Selection Toolbar (Conditional)
- Shows only when `SelectedCount > 0`
- Displays count: "X event(s) selected"
- Contains action buttons:
  - "Delete Selected (X)" - Red danger button
  - "Clear Selection" - Gray secondary button
- Bootstrap alert-info styling
- Flexbox layout with space-between

#### 3. Simplified Confirmation Dialog
- Modal overlay with backdrop (rgba 0,0,0,0.5)
- Card-based dialog (500px width, centered)
- Title: "Confirm Deletion" (red color)
- Warning message with count
- Bullet list of what will be deleted:
  - All event organizers
  - All event participants
  - All event recordings (database records)
  - All recording files from disk
- Bold warning: "This action cannot be undone"
- Action buttons:
  - "Cancel" - Secondary button
  - "Delete X Event(s)" - Danger button

#### 4. Actions Column Simplified
- **Edit Mode:** Save, Cancel buttons only
- **View Mode:** Edit button only
- **Removed:** Individual "Remove" buttons (forced bulk workflow)

---

### State Management

**New Properties:**
```csharp
// Selection tracking
private Dictionary<int, bool> selectedEventIds = new();
private bool selectAll = false;

// Computed properties
private int SelectedCount => selectedEventIds.Count(kvp => kvp.Value);
private List<int> GetSelectedEventIds() => selectedEventIds.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

// Dialog state
private bool showBulkDeleteDialog = false;
```

**New Methods:**
```csharp
private void ToggleSelectAll()
private void ClearSelection()
private void PromptBulkDelete()
private void CancelBulkDelete()
private async Task ConfirmBulkDeleteAsync()
```

**Updated Methods:**
```csharp
private async Task LoadEventsAsync()
  - Now initializes selectedEventIds for new events
  - Preserves existing selections when reloading
```

**Removed Methods:**
```csharp
- RemoveAsync() - Old single-event deletion without cleanup
- PromptRemoveEvent() - Old typed confirmation flow
- CancelDeleteEvent() - Old dialog management
- ConfirmDeleteEventAsync() - Old single-delete logic
- pendingEventForDeletion - Old state tracking
- deleteEventExpectedText - Old typed confirmation state
- deleteEventPendingId - Old state tracking
- typedDeleteEventText - Old input binding
```

---

## Data Flow

```
┌─────────────────────────────────────────────────────────┐
│ 1. User selects checkboxes                             │
│    └─> selectedEventIds[eventId] = true               │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│ 2. User clicks "Delete Selected (X)"                    │
│    └─> PromptBulkDelete()                              │
│    └─> showBulkDeleteDialog = true                     │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│ 3. Confirmation dialog displayed                        │
│    Shows count and warning                              │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│ 4. User clicks "Delete X Event(s)"                      │
│    └─> ConfirmBulkDeleteAsync()                        │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│ 5. Get selected IDs: GetSelectedEventIds()              │
│    └─> Returns List<int> of checked event IDs          │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│ 6. Call EventService.DeleteMultipleEventsWithCleanupAsync│
│    └─> Processes each event                            │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│ 7. For each event ID:                                   │
│    ├─> Call DeleteEventWithCleanupAsync(eventId)        │
│    ├─> Delete recording files from /app/recordings      │
│    ├─> Delete event (cascade: organizers, participants) │
│    ├─> Log results                                      │
│    └─> Continue on error (don't abort batch)            │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│ 8. Return aggregate statistics                          │
│    ├─> successCount: events successfully deleted        │
│    ├─> failureCount: events that failed                │
│    ├─> totalDeletedFiles: recording files removed       │
│    └─> message: detailed summary                        │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│ 9. Update UI                                            │
│    ├─> Hide dialog                                      │
│    ├─> Clear selection                                  │
│    ├─> Reload events table                              │
│    ├─> Show status message (success/warning)            │
│    └─> Log to console                                   │
└─────────────────────────────────────────────────────────┘
```

---

## Database Operations

### What Gets Deleted (Per Event)

**1. Recording Files (Manual Cleanup)**
```sql
-- Files are NOT managed by EF Core, must be deleted manually
Path: /app/recordings/{filename}
Source: event_recordings.file_name
```

**2. Event Recordings (Database)**
```sql
DELETE FROM event_recordings WHERE event_id = @eventId;
-- Cascade configured in schema
```

**3. Event Organizers (Database)**
```sql
DELETE FROM event_organizers WHERE event_id = @eventId;
-- Cascade configured in schema
```

**4. Event Participants (Database)**
```sql
DELETE FROM event_participants WHERE event_id = @eventId;
-- Cascade configured in schema
```

**5. Event (Database)**
```sql
DELETE FROM events WHERE id = @eventId;
-- Triggers cascade deletion of related records
```

### Transaction Behavior

- Each event deletion is processed individually (separate transactions)
- Recording files deleted before database operations
- Database cascade handles related records automatically
- Errors in one event don't rollback others (batch continues)

---

## Security

### Authorization

- **Page Level:** `@attribute [Authorize(Roles = "Administrator")]`
- **Access:** Only users with Administrator role can access /eventManagement
- **No additional checks:** Service method assumes caller is authorized

### Audit Trail

**Server Logging:**
```csharp
_logger.LogInformation("=== DeleteMultipleEventsWithCleanupAsync START === Event IDs: [{EventIds}]", ...)
_logger.LogInformation("Successfully deleted event {EventId}", ...)
_logger.LogWarning("Failed to delete event {EventId}: {Message}", ...)
_logger.LogInformation("=== DeleteMultipleEventsWithCleanupAsync END === Success: {Success}, Failed: {Failed}, Files: {Files}", ...)
```

**Client Logging:**
```javascript
console.log("=== CLIENT: Bulk delete START === Deleting X event(s)")
console.log("CLIENT: Bulk delete completed. Success: X, Failed: Y, Files: Z")
console.log("=== CLIENT: Bulk delete END ===")
```

---

## Error Handling

### Event-Level Errors

**Scenario:** Individual event deletion fails (e.g., event not found, database error)

**Behavior:**
- Error logged with details
- Failure count incremented
- Error message added to summary
- **Batch continues** processing remaining events

**Example:**
```
Bulk deletion completed: 4 event(s) deleted successfully, 50 recording file(s) removed from disk. 
1 event(s) failed: Event 999: Event not found.
```

### File Deletion Errors

**Scenario:** Recording file doesn't exist or deletion fails

**Behavior:**
- Warning logged (file already deleted or missing)
- File deletion failure doesn't block database deletion
- Event still removed from database
- File count doesn't include failed files

**Example Log:**
```
Recording file not found (already deleted?): recording-abc123.webm
```

### Complete Failure

**Scenario:** Exception in batch processing

**Behavior:**
- Exception caught in `ConfirmBulkDeleteAsync()`
- Error message displayed to user
- Logged to server and browser console
- Dialog closed, selection preserved

---

## UI/UX Design

### Visual States

**1. No Selection:**
```
┌─────────────────────────────────────────────────────┐
│ Event Management                                    │
├─────────────────────────────────────────────────────┤
│ [☐] Id  Created  Title  ...  Actions               │
├─────────────────────────────────────────────────────┤
│ [☐] 1   2026-01  Event A  ...  [Edit]              │
│ [☐] 2   2026-01  Event B  ...  [Edit]              │
├─────────────────────────────────────────────────────┤
│ [Add new event]                                     │
└─────────────────────────────────────────────────────┘
```

**2. With Selection:**
```
┌─────────────────────────────────────────────────────┐
│ Event Management                                    │
├─────────────────────────────────────────────────────┤
│ ℹ️ 3 event(s) selected  [Delete Selected (3)] [Clear]│
├─────────────────────────────────────────────────────┤
│ [☑] Id  Created  Title  ...  Actions               │
├─────────────────────────────────────────────────────┤
│ [☑] 1   2026-01  Event A  ...  [Edit]              │
│ [☐] 2   2026-01  Event B  ...  [Edit]              │
│ [☑] 3   2026-01  Event C  ...  [Edit]              │
│ [☑] 4   2026-01  Event D  ...  [Edit]              │
├─────────────────────────────────────────────────────┤
│ [Add new event]                                     │
└─────────────────────────────────────────────────────┘
```

**3. Confirmation Dialog:**
```
┌──────────────────────────────────────┐
│ Confirm Deletion                     │
├──────────────────────────────────────┤
│ You are about to permanently delete  │
│ 3 event(s).                          │
│                                      │
│ This will also delete:               │
│  • All event organizers              │
│  • All event participants            │
│  • All event recordings (database)   │
│  • All recording files from disk     │
│                                      │
│ This action cannot be undone.        │
│                                      │
│              [Cancel] [Delete 3...] │
└──────────────────────────────────────┘
```

### Bootstrap Classes Used

- `alert alert-info` - Selection toolbar background
- `btn btn-danger btn-sm` - Delete button
- `btn btn-secondary btn-sm` - Cancel/Clear buttons
- `d-flex justify-content-between align-items-center` - Toolbar layout
- `table table-striped table-hover align-middle` - Table styling
- `card` - Dialog container
- `card-body` - Dialog content
- `text-danger` - Warning text
- `gap-2` - Button spacing

---

## Testing Checklist

### Functional Tests

- [ ] **Checkbox Selection**
  - [ ] Individual checkboxes toggle correctly
  - [ ] "Select All" checkbox toggles all events
  - [ ] Selection count updates correctly
  - [ ] Selection toolbar appears/disappears

- [ ] **Bulk Deletion**
  - [ ] Dialog shows correct count
  - [ ] Delete button works (single click)
  - [ ] Success message shows correct statistics
  - [ ] Table refreshes after deletion
  - [ ] Selection clears after deletion

- [ ] **Single Event Deletion**
  - [ ] Can still delete single event via checkbox
  - [ ] Same workflow as bulk (no special case)

- [ ] **Error Handling**
  - [ ] Partial failures show warning message
  - [ ] Failed events listed in message
  - [ ] Success count correct even with failures
  - [ ] Database consistent after partial failure

- [ ] **Recording File Cleanup**
  - [ ] Recording files deleted from disk
  - [ ] File count accurate in success message
  - [ ] Missing files don't block deletion
  - [ ] Disk space freed (check docker volume)

### Edge Cases

- [ ] **Empty Selection**
  - [ ] Toolbar doesn't appear with 0 selected
  - [ ] Delete button doesn't trigger with 0 selected

- [ ] **All Events Selected**
  - [ ] Can delete all events
  - [ ] Table empty after deletion
  - [ ] No errors when table becomes empty

- [ ] **Concurrent Modifications**
  - [ ] Handles event deleted by another user
  - [ ] Graceful error if event missing

- [ ] **Large Batch**
  - [ ] Can select and delete many events (50+)
  - [ ] Performance acceptable
  - [ ] Memory usage reasonable

### Security Tests

- [ ] **Authorization**
  - [ ] Non-admin users cannot access /eventManagement
  - [ ] Organizer role cannot access page

- [ ] **Audit Trail**
  - [ ] Server logs show all deletions
  - [ ] Client logs show operation details
  - [ ] Failed deletions logged with reasons

---

## Performance Considerations

### Time Complexity

- **Selection:** O(1) per checkbox toggle
- **Select All:** O(n) where n = number of events
- **Deletion:** O(m) where m = number of selected events
  - Each event deletion: O(1) database operations (cascade)
  - File I/O: O(k) where k = recordings per event

### Optimization Opportunities

**Current Implementation:**
- Sequential processing (one event at a time)
- Separate database transaction per event

**Future Improvements (if needed):**
- Parallel file deletion (Task.WhenAll)
- Batched database operations
- Progress indicator for large deletions
- Background job for very large batches

---

## Known Limitations

1. **No Undo:** Deletion is permanent, cannot be reversed
2. **No Soft Delete:** Events are hard-deleted from database
3. **No Backup:** Recording files deleted without backup
4. **No Progress Bar:** No visual feedback during long operations
5. **Sequential Processing:** Events deleted one at a time (not parallel)
6. **No Confirmation List:** Dialog shows count, not individual event titles

---

## Future Enhancements

### Potential Improvements

1. **Soft Delete**
   - Add `IsDeleted` flag to events table
   - "Archive" instead of delete
   - Admin page to view/restore archived events

2. **Backup Before Delete**
   - Copy recording files to archive directory
   - Export event data to JSON before deletion
   - Configurable retention period

3. **Enhanced Confirmation**
   - Expandable list showing event titles
   - Display total disk space to be freed
   - Show participant counts per event

4. **Progress Feedback**
   - Progress bar for large batches
   - Real-time status updates (SignalR)
   - Cancel operation mid-process

5. **Batch Export**
   - Export selected events to CSV/JSON
   - Backup before bulk delete
   - Restore from backup

6. **Advanced Filters**
   - Select by date range
   - Select by status (past events)
   - Select by participant count

---

## Related Features

- **EventList Deletion** (`/events`) - Organizer single-event deletion with typed confirmation
- **EventEdit Deletion** (`/events/edit/{id}`) - Organizer single-event deletion with typed confirmation
- **EventService.DeleteEventWithCleanupAsync()** - Core deletion logic with file cleanup
- **EventService.DeleteMultipleEventsWithCleanupAsync()** - Bulk deletion wrapper

---

## Code Locations

**Service Layer:**
- `Services/EventService.cs` (lines 323-366) - `DeleteMultipleEventsWithCleanupAsync()`

**UI Components:**
- `Components/Pages/Admin/EventManagement.razor`
  - Lines 14-35: Selection toolbar
  - Lines 26-29: Checkbox column header
  - Lines 44-46: Checkbox column cells
  - Lines 58-64: Removed individual delete buttons
  - Lines 117-141: Simplified confirmation dialog
  - Lines 157-162: Selection state properties
  - Lines 169-179: Selection initialization in LoadEventsAsync
  - Lines 187-246: Selection and deletion methods

**Documentation:**
- `LLM-docs/features/admin-bulk-event-deletion.md` - This file

---

## Changelog

### 2026-01-02 - Initial Implementation
- Added `DeleteMultipleEventsWithCleanupAsync()` to EventService
- Added checkbox selection to EventManagement table
- Added selection toolbar with bulk actions
- Created simple confirmation dialog (no typing required)
- Removed individual "Remove" buttons from Actions column
- Removed old single-event deletion code
- Added comprehensive logging
- Updated documentation

---

## Maintenance Notes

### When Modifying This Feature

1. **Service Method Changes:**
   - Update logging statements if changing behavior
   - Maintain error resilience (continue on individual failures)
   - Keep return type consistent (tuple with statistics)

2. **UI Changes:**
   - Test checkbox state management carefully
   - Ensure selection count updates correctly
   - Maintain accessibility (ARIA labels, keyboard navigation)

3. **Security Changes:**
   - Keep authorization check at page level
   - Add audit logging for compliance
   - Consider soft delete for sensitive data

4. **Performance Changes:**
   - Monitor batch processing time
   - Add progress feedback for large batches
   - Consider background jobs for very large deletions

### Testing After Changes

1. Run full functional test checklist
2. Check server logs for errors
3. Verify recording files deleted from disk
4. Test edge cases (empty selection, all events, concurrent mods)
5. Verify authorization still works

---

## Support Information

**Primary Contact:** Development Team  
**Escalation:** System Administrator  
**Related Documentation:**
- `LLM-docs/features/event-deletion-cleanup.md` - Single-event deletion
- `LLM-docs/architecture-overview.md` - Overall system architecture
- `SECURITY-NOTES.md` - Security considerations

**Common Issues:**

1. **Checkboxes not updating:** Check browser console for JS errors
2. **Deletion fails silently:** Check server logs for exceptions
3. **Recording files not deleted:** Verify `/app/recordings` path and permissions
4. **Authorization error:** Ensure user has Administrator role

---

## References

- EF Core Cascade Delete: https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete
- Blazor Forms and Validation: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms-validation
- Bootstrap Components: https://getbootstrap.com/docs/5.3/components/
- File I/O Best Practices: https://learn.microsoft.com/en-us/dotnet/standard/io/

---

**Last Updated:** 2026-01-02  
**Version:** 1.0  
**Status:** ✅ Production Ready (after testing)
