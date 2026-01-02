# Event Deletion with Cleanup - Implementation Guide

## Overview
Comprehensive event deletion functionality that properly cleans up all associated data and recording files to prevent disk space waste.

## Features Implemented

### 1. UI Improvements - EventEdit Page
**Save/Cancel Buttons Relocated:**
- ✅ Moved from middle of page to bottom
- ✅ Positioned at bottom-left for better UX flow
- ✅ Delete button positioned at bottom-right (Admin only)
- ✅ Clear visual separation with horizontal rule

**Layout:**
```
┌─────────────────────────────────────┐
│ Event Form Fields                   │
│ ...                                 │
│ Participants Table                  │
├─────────────────────────────────────┤
│ [Save] [Cancel]    [Delete Event]   │
└─────────────────────────────────────┘
     ↑ Left                Right ↑
```

### 2. Delete Functionality - EventList Page
**New Delete Button:**
- ✅ Added trash icon button in actions column
- ✅ Available to all Organizers (for their events) and Admins
- ✅ Tooltip: "Delete event and all related data"

**Confirmation Dialog:**
- ✅ Modal overlay with warning message
- ✅ Lists all data that will be deleted
- ✅ Requires typing event title to confirm
- ✅ Shows event details (title, date, created timestamp)

### 3. Backend Cleanup Service
**EventService.DeleteEventWithCleanupAsync():**

```csharp
public async Task<(bool success, string message, int deletedRecordingFiles)> 
    DeleteEventWithCleanupAsync(int eventId, string recordingsBasePath = "/app/recordings")
```

**What It Does:**
1. Finds event by ID
2. Retrieves all recording records
3. Deletes recording files from disk
4. Logs file size for each deleted recording
5. Deletes event (cascade handles related data)
6. Returns detailed results

**Data Deleted (Cascade):**
- Event organizers (via FK cascade)
- Event participants (via FK cascade)
- Event recordings database records (via FK cascade)
- Recording files from disk (manual cleanup)

## Recording File Cleanup

### Storage Location
- Docker volume: `recordings-data`
- Container path: `/app/recordings`
- Files named as stored in `EventRecording.FileName`

### Cleanup Process
```
For each recording:
  1. Get file path: /app/recordings/{FileName}
  2. Check if file exists
  3. Get file size (for logging)
  4. Delete file
  5. Log: "Deleted recording file: {FileName} ({Size:F2} MB)"
  6. Increment deleted files counter
```

### Error Handling
- Continues deletion even if some files fail
- Logs warnings for missing files
- Logs errors for file deletion failures
- Always proceeds with database deletion

## Logging

### Server-Side Logs
All operations logged with context:
```
=== DeleteEventWithCleanupAsync START === Event ID: 5
Deleting event: My Event (ID: 5)
Found 3 recordings to delete
Deleted recording file: event5_rec1.webm (25.43 MB)
Deleted recording file: event5_rec2.webm (18.92 MB)
Deleted recording file: event5_rec3.webm (31.15 MB)
Deleting 2 organizers, 15 participants, 3 recording records
=== DeleteEventWithCleanupAsync END === Success: Event 'My Event' deleted successfully...
```

### User Feedback
Success message includes:
- Event title
- Count of organizers deleted
- Count of participants deleted
- Count of recording records deleted
- Count of files deleted from disk

Example:
```
Event 'My Conference' deleted successfully. 
Removed: 2 organizer(s), 15 participant(s), 3 recording(s) (3 file(s) deleted from disk).
```

## Security Considerations

### Authorization
- Only Administrators and Organizers can delete events
- Organizers can only delete their own events (via `CanUserManageEventAsync`)
- Admin can delete any event

### Confirmation Required
- Must type exact event title to confirm
- Delete button disabled until title matches
- Modal dialog prevents accidental clicks

### Data Integrity
- Database cascade deletes handle relationships
- File system cleanup is best-effort (logs failures but proceeds)
- Transaction ensures database consistency

## Testing Checklist

### UI Testing
- [ ] EventEdit: Verify buttons at bottom left/right
- [ ] EventEdit: Save/Cancel buttons work correctly
- [ ] EventEdit: Delete button only shows for Admins
- [ ] EventList: Delete button appears in actions column
- [ ] EventList: Delete dialog shows warning and details
- [ ] EventList: Must type title to enable delete button

### Functional Testing
- [ ] Create event with no recordings → Delete → Verify success
- [ ] Create event with recordings → Delete → Verify files removed
- [ ] Check docker logs for deletion details
- [ ] Verify participants deleted from database
- [ ] Verify organizers deleted from database
- [ ] Verify recording records deleted from database

### Database Verification
```sql
-- Before deletion
SELECT id, title FROM events WHERE id = 5;
SELECT COUNT(*) FROM event_organizers WHERE event_id = 5;
SELECT COUNT(*) FROM event_participants WHERE event_id = 5;
SELECT COUNT(*) FROM event_recordings WHERE event_id = 5;

-- After deletion (should all return 0)
SELECT id, title FROM events WHERE id = 5;
SELECT COUNT(*) FROM event_organizers WHERE event_id = 5;
SELECT COUNT(*) FROM event_participants WHERE event_id = 5;
SELECT COUNT(*) FROM event_recordings WHERE event_id = 5;
```

### File System Verification
```bash
# Check recordings directory before deletion
docker exec konfucjusz_app ls -lh /app/recordings/

# Delete event with recordings

# Check recordings directory after deletion
docker exec konfucjusz_app ls -lh /app/recordings/

# Verify files are gone
```

## Disk Space Management

### Why This Matters
- Audio recordings can be 10-50 MB per recording
- Multiple recordings per event
- Without cleanup, deleted events still consume disk space
- Over time, this can fill up the volume

### Monitoring
Check disk usage of recordings volume:
```bash
# Check volume size
docker volume ls
docker system df -v

# Check actual files
docker exec konfucjusz_app du -sh /app/recordings
docker exec konfucjusz_app ls -lh /app/recordings | wc -l
```

### Manual Cleanup (If Needed)
If orphaned files exist:
```bash
# Find recordings in DB
docker exec konfucjusz_db psql -U konfucjusz -d konfucjusz_db -c \
  "SELECT file_name FROM event_recordings;"

# List all files
docker exec konfucjusz_app ls /app/recordings/

# Compare and manually delete orphans if needed
```

## Architecture Decisions

### Why Delete Files Manually?
- EF Core cascade only handles database relationships
- File system cleanup must be explicit
- Allows logging of freed disk space
- Provides error handling for missing files

### Why Continue on File Errors?
- Database consistency is more important
- Missing files might already be deleted
- File errors shouldn't block database cleanup
- Failures are logged for investigation

### Why Log File Sizes?
- Shows disk space freed
- Helps monitor storage usage trends
- Useful for capacity planning
- Helps identify large recordings

## Future Enhancements

### Potential Improvements
1. **Async File Deletion:** Queue file deletions for background processing
2. **Soft Delete:** Mark events as deleted instead of hard delete
3. **Backup Before Delete:** Archive event data before deletion
4. **Bulk Delete:** Delete multiple events at once
5. **Scheduled Cleanup:** Periodic scan for orphaned recording files
6. **Storage Metrics:** Dashboard showing disk usage by event

### Configuration Options
Future config for deletion behavior:
```json
{
  "EventDeletion": {
    "SoftDelete": false,
    "BackupBeforeDelete": true,
    "BackupPath": "/app/backups",
    "OrphanedFileCleanupDays": 30
  }
}
```

## Rollback Plan

If issues arise after deployment:
1. Revert to previous container version
2. Check docker logs for deletion errors
3. Manually verify database state
4. Restore from backup if needed
5. File deletions cannot be undone (ensure backups exist)
