# Session 004: Audio Streaming Sample Rate Fix

**Date:** 2026-01-05  
**Issue:** Progressive audio delay and pitch distortion in live streaming  
**Status:** Fixed - Ready for testing

---

## Problem Report

**User Symptoms:**
1. Audio starts with low lag
2. Delay progressively increases over time
3. Voice pitch sounds lower than normal
4. Glitchy playback with pauses between audio segments

**Test Environment:**
- **Broadcaster:** Android device, 48kHz microphone
- **Listener:** PC (Firefox), expecting 44.1kHz by default

---

## Diagnostic Results

### Metrics Collected
- **Broadcaster:** 1800+ chunks sent at 48kHz (85.33ms per chunk)
- **Listener:** Queue grew to 347 chunks (~29.6 seconds of audio)
- **Sample Rate Log:** Missing on listener side (critical clue!)

### Root Cause Identified

**Sample rate mismatch:**
- Broadcaster: 48000 Hz
- Listener: 44100 Hz (hardcoded default)
- **Drift rate:** 7.55ms per chunk (8.8% slower playback)

**Why it happened:**
Race condition in stream initialization - listeners joining after `NotifyStreamStarted` never received the sample rate parameter.

**Math:**
```
Broadcaster chunk time: 4096 samples ÷ 48000 Hz = 85.33ms
Listener playback time: 4096 samples ÷ 44100 Hz = 92.88ms
Drift per chunk: 92.88 - 85.33 = 7.55ms
After 1800 chunks: 1800 × 7.55ms = 13,590ms = 13.6 seconds of lag
```

This explains:
- ✅ Progressive delay (accumulates over time)
- ✅ Lower pitch (audio stretched by playing slower)
- ✅ Glitchy playback (processing overhead from huge queue)

---

## Solutions Implemented

### Fix 1: Sample Rate in JoinListener Response

**Change:** Modified SignalR hub method signature
```csharp
// Before
public async Task<bool> JoinListener(int eventId, string slug, string? token)

// After  
public async Task<(bool success, int sampleRate)> JoinListener(int eventId, string slug, string? token)
```

**Implementation:**
1. Added `ConcurrentDictionary<int, int> EventSampleRates` to track active streams
2. `NotifyStreamStarted(eventId, sampleRate)` stores sample rate for the event
3. `JoinListener()` returns stored sample rate (or 44100 default if not set)
4. `NotifyStreamEnded()` clears stored sample rate
5. JavaScript client receives sample rate immediately on join

**Benefits:**
- Eliminates race condition
- Works for late-joining listeners
- No timing dependency on `StreamStarted` event

**Files changed:**
- `Hubs/AudioStreamHub.cs`
- `wwwroot/js/audioStream.js`

---

### Fix 2: Queue Depth Limiting

**Problem:** Unbounded queue growth (347 chunks = 29.6 seconds buffered)

**Solution:** Added `maxQueueSize: 10` chunks limit (~850ms buffer)

**Behavior:**
```javascript
if (listen.queue.length >= listen.maxQueueSize) {
    const dropped = listen.queue.shift(); // Drop oldest chunk
    listen.totalChunksDropped++;
    console.warn(`Queue full, dropped oldest chunk`);
}
listen.queue.push(bytes);
```

**Trade-offs:**
- ✅ Prevents unbounded memory growth
- ✅ Maintains low latency (~850ms max)
- ✅ Self-corrects if sample rate mismatch occurs
- ⚠️ May cause brief audio gaps if network is very slow

**Tuning:**
- Current: 10 chunks (850ms at 48kHz)
- Can be increased for higher latency tolerance
- Can be decreased for lower latency (more aggressive)

**Files changed:**
- `wwwroot/js/audioStream.js`

---

## Enhanced Diagnostics

### Broadcaster Logs (Debug Panel)
```
[BROADCAST-DIAG] Detected sample rate: 48000 Hz
[BROADCAST-DIAG] Expected chunk interval: 85.33 ms
[BROADCAST-DIAG] Chunk #100: 8192B, interval: 85ms, sampleRate: 48000Hz
```

### Listener Logs (F12 Console)
```
[LISTEN-DIAG] Sample rate set from JoinListener: 48000Hz
[LISTEN-DIAG] Playing chunk #1, size: 8192 bytes, queue: 0, gap: 0ms, sampleRate: 48000Hz
[LISTEN-DIAG] Chunk duration: 85.33ms, AudioContext.sampleRate: 48000Hz
[LISTEN-DIAG] Received chunk #50, size: 8192B, queue: 2→3, lag: 5 chunks, dropped: 0
```

### New Metrics Tracked
- `totalChunksReceived`: Total chunks received from network
- `totalChunksPlayed`: Total chunks played through speakers
- `totalChunksDropped`: Total chunks dropped due to queue overflow
- `lag`: Calculated as `received - played - dropped`
- `queue`: Current buffer depth

---

## Testing Instructions

### Step 1: Rebuild Application
```bash
dotnet build
dotnet run
```

### Step 2: Test Sample Rate Detection

**On Android (Broadcaster):**
1. Log in as organizer
2. Navigate to `/events/{id}/stream-broadcast`
3. Start streaming
4. **Verify in debug panel:** "Detected sample rate: 48000 Hz" (or device-specific value)

**On PC (Listener):**
1. Log in as participant
2. Navigate to `/e/{slug}/stream`
3. Open Firefox F12 console
4. Join stream
5. **Verify in console:** `[LISTEN-DIAG] Sample rate set from JoinListener: 48000Hz`

### Step 3: Monitor Queue Depth

**Expected behavior:**
- Queue should stabilize at 2-5 chunks
- No dropped chunks if sample rates match
- Lag should remain constant (not growing)
- Voice pitch should be correct

**If sample rates match:**
```
[LISTEN-DIAG] Received chunk #150, lag: 3 chunks, dropped: 0
[LISTEN-DIAG] Received chunk #200, lag: 3 chunks, dropped: 0
[LISTEN-DIAG] Received chunk #250, lag: 4 chunks, dropped: 0
```

**If queue limiter activates (network issues):**
```
[LISTEN-DIAG] Queue full (10), dropped oldest chunk. Total dropped: 1
[LISTEN-DIAG] Queue full (10), dropped oldest chunk. Total dropped: 2
```

### Step 4: Long-Duration Test

Run stream for 5+ minutes and verify:
- ✅ Queue depth stays bounded (≤10 chunks)
- ✅ No progressive delay increase
- ✅ Voice pitch remains correct
- ✅ Audio quality is clear (no distortion)
- ✅ Minimal chunks dropped (should be 0 if rates match)

---

## Success Criteria

### Must Have (Critical)
- [x] Sample rate correctly detected on broadcaster
- [x] Sample rate transmitted to listener on join
- [x] Listener uses correct sample rate for playback
- [x] Queue depth limited to prevent unbounded growth
- [ ] **Test passes:** 5-minute stream with stable queue and correct pitch

### Should Have (Important)
- [x] Comprehensive diagnostic logging
- [x] Statistics tracking (received/played/dropped)
- [ ] Visual warning in UI if sample rate mismatch detected
- [ ] Documentation updated

### Nice to Have (Future)
- [ ] Adaptive queue size based on network conditions
- [ ] Jitter buffer for network variability
- [ ] Sample rate conversion if mismatch unavoidable

---

## Known Limitations

1. **AudioContext sample rate is fixed** - Cannot change after creation
2. **Queue limiter may cause gaps** - If network is consistently slow
3. **No automatic retry** - If sample rate transmission fails
4. **Platform-dependent sample rates** - Android often uses 48kHz, desktop often 44.1kHz

---

## Rollback Plan

If issues occur in production:

```bash
git checkout HEAD~1 -- Hubs/AudioStreamHub.cs wwwroot/js/audioStream.js
dotnet build
dotnet run
```

This reverts to the previous version without the fixes.

---

## References

- **Diagnostic Guide:** `/LLM-docs/audio-streaming-diagnostics-guide.md`
- **Architecture Overview:** `/LLM-docs/architecture-overview.md`
- **Web Audio API Docs:** https://developer.mozilla.org/en-US/docs/Web/API/Web_Audio_API

---

## Next Steps

1. **Test the fix** with Android + PC setup
2. **Monitor diagnostics** for sample rate confirmation
3. **Verify queue stability** over 5+ minute stream
4. **Add UI warning** if sample rate mismatch detected (optional)
5. **Document in user guide** if testing successful
