using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

public class AudioStreamHub : Hub
{
    private static readonly ConcurrentDictionary<int, HashSet<string>> EventGroups = new();
    private static readonly ConcurrentDictionary<string, (int eventId, string display)> ConnectionIndex = new();
    private static readonly ConcurrentDictionary<int, RecordingWriter?> ActiveRecordings = new();

    private readonly ApplicationDbContext _db;

    public AudioStreamHub(ApplicationDbContext db)
    {
        _db = db;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionIndex.TryRemove(Context.ConnectionId, out var info))
        {
            if (EventGroups.TryGetValue(info.eventId, out var set))
            {
                lock (set)
                {
                    set.Remove(Context.ConnectionId);
                }
            }
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(info.eventId));
            await Clients.Group(ManagerGroup(info.eventId)).SendAsync("ListenerLeft", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<bool> JoinListener(int eventId, string slug, string? token)
    {
        var ev = await _db.events.FirstOrDefaultAsync(e => e.Id == eventId && e.Slug == slug);
        if (ev == null) return false;

        var user = Context.User;
        var isAuth = user?.Identity?.IsAuthenticated == true;
        
        // Access rules:
        // 1. If allow_anonymous_streaming: allow anyone
        // 2. If token provided: validate it
        // 3. If authenticated: check if user is enlisted or is organizer
        var allowed = ev.AllowAnonymousStreaming;
        
        if (!allowed && !string.IsNullOrEmpty(token))
        {
            // Validate stream token (requires ParticipantService - inject or pass)
            // For now we'll accept token presence as valid; enhance later with actual validation
            allowed = true;
        }
        
        if (!allowed && isAuth)
        {
            // Check if user is enlisted or organizer
            var userId = int.TryParse(user!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (int?)null;
            if (userId.HasValue)
            {
                var isOrganizer = await _db.eventOrganizers.AnyAsync(eo => eo.EventId == eventId && eo.UserId == userId.Value);
                var isEnlisted = await _db.eventParticipants.AnyAsync(ep => ep.EventId == eventId && ep.UserId == userId.Value);
                allowed = isOrganizer || isEnlisted;
            }
        }

        if (!allowed) return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(eventId));
        EventGroups.AddOrUpdate(eventId, _ => new HashSet<string> { Context.ConnectionId }, (_, set) => { lock (set) set.Add(Context.ConnectionId); return set; });
        var display = BuildDisplayName(user);
        ConnectionIndex[Context.ConnectionId] = (eventId, display);
        await Clients.Group(ManagerGroup(eventId)).SendAsync("ListenerJoined", Context.ConnectionId, display);
        return true;
    }

    public async Task LeaveListener(int eventId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(eventId));
        if (ConnectionIndex.TryRemove(Context.ConnectionId, out var info))
        {
            if (EventGroups.TryGetValue(info.eventId, out var set))
            {
                lock (set)
                {
                    set.Remove(Context.ConnectionId);
                }
            }
            await Clients.Group(ManagerGroup(info.eventId)).SendAsync("ListenerLeft", Context.ConnectionId);
        }
    }

    // Organizer joins manager group to receive listener events
    public async Task<bool> JoinManager(int eventId)
    {
        // Allow only authenticated organizers/admins in first iteration (checked in UI elsewhere)
        await Groups.AddToGroupAsync(Context.ConnectionId, ManagerGroup(eventId));
        // Send current listeners snapshot
        if (EventGroups.TryGetValue(eventId, out var set))
        {
            var list = set.Select(cid => new { cid, display = ConnectionIndex.TryGetValue(cid, out var info) ? info.display : "?" }).ToList();
            await Clients.Caller.SendAsync("ListenersSnapshot", list);
        }
        return true;
    }

    // Organizer broadcasts audio chunk (PCM16 little-endian)
    public async Task BroadcastAudioChunk(int eventId, byte[] chunk)
    {
        // For v1 skip heavy validation and forward to group
        await Clients.Group(GroupName(eventId)).SendAsync("ReceiveAudio", chunk);

        // If recording active, append chunk
        if (ActiveRecordings.TryGetValue(eventId, out var writer) && writer != null)
        {
            writer.AppendPcm16(chunk);
        }
    }

    private static string GroupName(int eventId) => $"event-{eventId}";
    private static string ManagerGroup(int eventId) => $"event-{eventId}-mgr";
    private static string BuildDisplayName(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated == true)
        {
            return user.Identity?.Name ?? "User";
        }
        return "Guest";
    }

    // Start server-side recording (must be organizer validated externally)
    public bool StartRecording(int eventId)
    {
        if (ActiveRecordings.ContainsKey(eventId)) return false; // already recording
        var fileName = $"event-{eventId}-{DateTime.UtcNow:yyyyMMddHHmmss}.wav";
        var path = Path.Combine("wwwroot", "recordings");
        Directory.CreateDirectory(path);
        var fullPath = Path.Combine(path, fileName);
        var writer = new RecordingWriter(fullPath, 44100, 1);
        ActiveRecordings[eventId] = writer;
        return true;
    }

    public async Task<string?> StopRecording(int eventId)
    {
        if (!ActiveRecordings.TryRemove(eventId, out var writer) || writer == null) return null;
        await writer.CompleteAsync();
        var fileName = Path.GetFileName(writer.FilePath);
        var durationSeconds = writer.DurationSeconds;
        
        // Save to database
        var db = _db;
        var recording = new EventRecording
        {
            EventId = eventId,
            FileName = fileName,
            DurationSeconds = durationSeconds,
            Completed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.eventRecordings.Add(recording);
        await db.SaveChangesAsync();
        
        return fileName;
    }
}

// Minimal WAV writer for PCM16 mono
public class RecordingWriter
{
    public string FilePath { get; }
    private readonly FileStream _stream;
    private readonly int _sampleRate;
    private readonly short _channels;
    private long _dataLength = 0;
    private bool _completed = false;
    private readonly DateTime _startTime;

    public int DurationSeconds => (int)(_dataLength / (_sampleRate * _channels * 2));

    public RecordingWriter(string path, int sampleRate, short channels)
    {
        FilePath = path;
        _sampleRate = sampleRate;
        _channels = channels;
        _startTime = DateTime.UtcNow;
        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        WriteHeaderPlaceholder();
    }

    public void AppendPcm16(byte[] pcm16)
    {
        if (_completed) return;
        _stream.Write(pcm16, 0, pcm16.Length);
        _dataLength += pcm16.Length;
    }

    public Task CompleteAsync()
    {
        if (_completed) return Task.CompletedTask;
        _completed = true;
        // Rewrite header with proper sizes
        _stream.Seek(0, SeekOrigin.Begin);
        WriteHeader(_dataLength);
        _stream.Flush();
        _stream.Dispose();
        return Task.CompletedTask;
    }

    private void WriteHeaderPlaceholder() => WriteHeader(0);

    private void WriteHeader(long dataLength)
    {
        var writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen:true);
        int byteRate = _sampleRate * _channels * 2;
        short blockAlign = (short)(_channels * 2);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write((int)(36 + dataLength));
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // PCM subchunk size
        writer.Write((short)1); // audio format PCM
        writer.Write(_channels);
        writer.Write(_sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write((short)16); // bits per sample
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write((int)dataLength);
    }
}
