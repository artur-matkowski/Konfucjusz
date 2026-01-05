# Audio Streaming Diagnostics Guide

## Test Results (2026-01-05)

### Observed Behavior
- **Broadcaster (Android)**: 48kHz sample rate detected, 1800+ chunks sent
- **Listener (PC)**: No sample rate log visible, queue grew to 347 chunks
- **Symptoms**: Pitch distortion (lower pitch), glitchy playback with pauses between segments
- **Root Cause**: Sample rate mismatch - broadcaster sending 48kHz, listener playing as 44.1kHz

### Analysis
**Math:**
- Broadcaster: 48000 Hz, 4096 samples = 85.33ms/chunk
- Listener (wrong): 44100 Hz playback = 92.88ms/chunk (8.8% slower)
- Drift: 7.55ms per chunk
- After 1800 chunks: 13.6 seconds accumulated lag
- Queue: 347 chunks = 29.6 seconds buffered audio

**Conclusion:** Listener never received sample rate from broadcaster due to race condition in stream initialization.

---

# Audio Streaming Diagnostics Guide

## Problem Description

**Symptoms:**
- Audio starts with low latency
- Delay progressively increases over time
- Voice pitch appears lower than normal

**Suspected Causes:**
1. Sample rate mismatch between broadcaster and listener
2. Buffer accumulation (chunks arriving faster than playback)
3. Different device audio capabilities (Android vs PC)

## Diagnostic Implementation

### Changes Made (2026-01-05)

#### 1. Broadcaster Side (audioStream.js)
- **Dynamic sample rate detection**: No longer hardcoded to 44100 Hz
- **Actual sample rate read from AudioContext**: `broadcast.sampleRate = broadcast.audioCtx.sampleRate`
- **Enhanced logging**: Chunk interval timing, detected sample rate
- **Debug panel output**: Real-time diagnostics visible in browser UI

**Key Logs:**
```
[BROADCAST-DIAG] Detected sample rate: XXXXX Hz
[BROADCAST-DIAG] Expected chunk interval: XX.XX ms
[BROADCAST-DIAG] Chunk #100: 8192B, interval: XXms, sampleRate: XXXXXHz
```

#### 2. Listener Side (audioStream.js)
- **Received sample rate from broadcaster**: Updates `listen.sampleRate` when stream starts
- **Queue depth tracking**: Monitors chunks received vs played
- **Timing diagnostics**: Measures chunk duration and playback gaps
- **Browser console logging**: F12 console shows detailed metrics

**Key Logs:**
```
[LISTEN-DIAG] StreamStarted event received - Sample rate: XXXXX Hz
[LISTEN-DIAG] Playing chunk #N, size: 8192 bytes, queue: N, gap: XXms, sampleRate: XXXXXHz
[LISTEN-DIAG] Chunk duration: XX.XXms, AudioContext.sampleRate: XXXXXHz
[LISTEN-DIAG] Received chunk #N, size: 8192B, queue: N→N+1, lag: N chunks
```

#### 3. SignalR Hub (AudioStreamHub.cs)
- **Sample rate parameter added** to `NotifyStreamStarted(int eventId, int sampleRate)`
- **Relays sample rate** from broadcaster to all listeners

#### 4. Blazor Component (StreamBroadcast.razor)
- **Reads detected sample rate** from JavaScript after broadcast starts
- **Sends sample rate** with stream start notification

## Testing Instructions

### Step 1: Start Broadcaster (Android)

1. Log in as organizer on Android device
2. Navigate to event streaming page
3. Click "Start Streaming"
4. **Open debug panel on page** (already visible in UI)
5. **Record the following from debug log:**
   - Detected sample rate (Hz)
   - Expected chunk interval (ms)
   - Actual chunk intervals from periodic logs

**Expected Debug Panel Output:**
```
[BROADCAST-DIAG] Detected sample rate: 48000 Hz
[BROADCAST-DIAG] Expected chunk interval: 85.33 ms
[BROADCAST-DIAG] Chunk #100: 8192B, interval: 85ms, sampleRate: 48000Hz
```

### Step 2: Start Listener (PC)

1. Log in as participant on PC
2. Navigate to stream listening page
3. Click "Join Stream"
4. **Open Firefox Developer Console** (F12 → Console tab)
5. **Record the following from console:**
   - StreamStarted sample rate received
   - Local AudioContext sample rate
   - Chunk receive intervals
   - Queue depth over time
   - Chunk lag (received - played)

**Expected Console Output:**
```
[LISTEN-DIAG] StreamStarted event received - Sample rate: 48000Hz
[LISTEN-DIAG] Current local sample rate: 44100Hz
[LISTEN-DIAG] Updated sample rate to: 48000Hz
[LISTEN-DIAG] Playing chunk #1, size: 8192 bytes, queue: 0, gap: 0ms, sampleRate: 48000Hz
[LISTEN-DIAG] Chunk duration: 85.33ms, AudioContext.sampleRate: 48000Hz
```

### Step 3: Monitor Over Time (5+ minutes)

**On Broadcaster (Android debug panel):**
- Chunk send intervals should remain consistent
- Note any drift or irregularity

**On Listener (PC F12 console):**
- Watch "lag" value in periodic logs (every 50 chunks)
- Watch "queue" depth in playback logs
- Watch "gap" timing between playback events

**Critical Metrics to Record:**

| Time | Broadcaster Sample Rate | Listener Sample Rate | Queue Depth | Lag (chunks) | Gap (ms) |
|------|------------------------|---------------------|-------------|--------------|----------|
| 0:30 |                        |                     |             |              |          |
| 1:00 |                        |                     |             |              |          |
| 2:00 |                        |                     |             |              |          |
| 5:00 |                        |                     |             |              |          |

## Diagnostic Scenarios

### Scenario A: Sample Rate Mismatch
**Symptoms:**
- Broadcaster: 48000 Hz
- Listener: 44100 Hz
- Queue depth steadily increases
- Lag grows over time

**Analysis:** Listener is playing chunks slower than they arrive because it's using wrong sample rate for buffer creation. Each chunk is stretched in time.

**Solution:** Ensure listener uses broadcaster's sample rate (now implemented).

---

### Scenario B: Network Burst Delivery
**Symptoms:**
- Queue depth fluctuates (0 → 5 → 2 → 0)
- Lag is minimal (<3 chunks average)
- Gap timing varies widely

**Analysis:** Network delivering chunks in bursts rather than steady stream. Normal behavior, but large queues might cause perceived lag.

**Solution:** Implement queue threshold management (drop old chunks if queue exceeds limit).

---

### Scenario C: Processing Bottleneck
**Symptoms:**
- Broadcaster intervals consistent
- Listener queue grows steadily
- Listener chunk duration > expected duration

**Analysis:** Listener device cannot decode/play audio fast enough.

**Solution:** Reduce chunk size or implement chunk skipping for real-time streaming.

---

## Fixes Implemented (2026-01-05)

### Fix 1: Sample Rate in JoinListener Response
**Problem:** Listeners joining after `NotifyStreamStarted` missed the sample rate.

**Solution:** 
- Changed `JoinListener` return type from `bool` to `(bool success, int sampleRate)`
- Hub stores sample rate in `ConcurrentDictionary<int, int> EventSampleRates`
- Listener receives sample rate immediately on join, regardless of timing
- JavaScript updated to handle tuple response and set `listen.sampleRate`

**Code changes:**
- `Hubs/AudioStreamHub.cs`: Lines 16, 58-149, 244-251, 259-266
- `wwwroot/js/audioStream.js`: Lines 174-198

### Fix 2: Queue Depth Limiting
**Problem:** Unbounded queue growth (347 chunks observed) causes memory issues and processing overhead.

**Solution:**
- Added `maxQueueSize: 10` (roughly 850ms buffer at 48kHz)
- Automatically drops oldest chunks when queue exceeds limit
- Tracks `totalChunksDropped` for diagnostics
- Logs warnings when dropping chunks

**Code changes:**
- `wwwroot/js/audioStream.js`: Lines 5-16, 113-145, 186-197

**Trade-off:** May cause brief audio gaps if network is very slow, but prevents unbounded lag accumulation.

---

## Next Steps Based on Results

### If Sample Rates Match
- Problem is likely buffer management
- Implement scheduled playback with latency target
- Add queue depth limiting

### If Sample Rates Differ
- Current fix should resolve issue
- Verify listener correctly updates sample rate from broadcaster
- Check if AudioContext.sampleRate matches listen.sampleRate during playback

### If Irregular Timing Detected
- Investigate network conditions
- Consider adding jitter buffer
- Implement adaptive buffering strategy

## Code Locations

- **Broadcaster JS**: `/wwwroot/js/audioStream.js` (lines 198-290)
- **Listener JS**: `/wwwroot/js/audioStream.js` (lines 45-136)
- **SignalR Hub**: `/Hubs/AudioStreamHub.cs` (line 239-251)
- **Blazor Component**: `/Components/Pages/Events/StreamBroadcast.razor` (line 279-286)

## Rollback Instructions

If diagnostics cause issues, revert to previous version:
```bash
git checkout HEAD -- wwwroot/js/audioStream.js Hubs/AudioStreamHub.cs Components/Pages/Events/StreamBroadcast.razor
```
