# Adaptive Playback Speed Solution

**Date:** 2026-01-05  
**Issue:** Audio chunk dropping (309/1924 chunks) due to systematic playback lag  
**Solution:** Adaptive playback speed with emergency-only dropping

---

## Problem Analysis

### Test Results (Previous Iteration)
- **Broadcaster:** 86ms per chunk (4096 samples @ 48kHz)
- **Listener:** 95ms actual playback duration (10.5% slower than arrival)
- **Outcome:** 1924 received, 1615 played, **309 dropped** ❌

### Root Cause
Even with correct sample rate (48kHz), the **playback infrastructure has overhead**:
- JavaScript decoding/buffer creation
- AudioContext scheduling latency
- Browser audio pipeline delays

Result: **Systematic 9ms deficit per chunk** → unbounded queue growth → aggressive dropping

### Why Dropping Is Bad
- ❌ Causes audio gaps and glitches
- ❌ Loses content permanently
- ❌ Doesn't solve systematic lag (just masks it)
- ❌ User experience degradation

---

## Adaptive Speed Solution

### Strategy
Instead of dropping chunks, **dynamically adjust playback speed** to maintain target buffer depth.

### Parameters
```javascript
targetQueueSize: 10           // Ideal buffer (850ms latency)
speedupRate: 1.3              // Playback speed when catching up
emergencyDropThreshold: 100   // Only drop if queue exceeds this
```

### Algorithm

```
On each chunk playback:
  IF queue > targetQueueSize (10 chunks):
    → Play at 1.3x speed (catch up mode)
  ELSE:
    → Play at 1.0x speed (normal mode)

On each chunk received:
  IF queue >= emergencyDropThreshold (100 chunks):
    → Drop oldest chunk (emergency only!)
  ELSE:
    → Add to queue (normal)
```

### How It Works

**Scenario 1: Normal Operation**
```
Queue: [||||||||] (8 chunks)
Speed: 1.0x
Status: Normal ✓
```

**Scenario 2: Buffering Buildup**
```
Queue: [||||||||||||] (12 chunks)
Speed: 1.3x (catch up activated)
Status: Catching up ⚠️
```

**Scenario 3: Caught Up**
```
Queue: [||||||||] (8 chunks)
Speed: 1.0x (back to normal)
Status: Normal ✓
```

**Scenario 4: Extreme Emergency**
```
Queue: [||||...||||] (100+ chunks)
Action: Drop oldest chunk
Status: Emergency drop 🚨
```

---

## Implementation Details

### JavaScript Changes (audioStream.js)

#### 1. Adaptive Playback Rate
```javascript
const shouldSpeedUp = queueLengthAfter > listen.targetQueueSize;
listen.currentPlaybackRate = shouldSpeedUp ? listen.speedupRate : 1.0;

// Apply to buffer source
src.playbackRate.value = listen.currentPlaybackRate;
```

**Effect:**
- At 1.3x: 86ms chunk plays in ~66ms (saves 20ms per chunk)
- Queue of 12 chunks catches up to 10 in ~10 chunks
- Pitch shift at 1.3x is barely noticeable for voice

#### 2. Emergency-Only Dropping
```javascript
if (listen.queue.length >= listen.emergencyDropThreshold) {
    listen.queue.shift(); // Drop oldest
    listen.totalChunksDropped++;
    console.error('[EMERGENCY] Queue exceeded 100 chunks!');
}
```

**Rationale:**
- 100 chunks = 8.5 seconds of buffered audio
- Should never reach this with adaptive speed
- Safety net for extreme network issues

#### 3. Visual Feedback
```javascript
function updateStreamQualityUI(queueDepth, playbackSpeed) {
    // Updates real-time indicators:
    // - Buffer depth
    // - Current playback speed (1.0x or 1.3x)
    // - Status (Normal / Catching up / Buffering)
}
```

### UI Changes (StreamListen.razor)

Added **Stream Quality** card showing:
- **Buffer:** Current queue depth in chunks
- **Speed:** Current playback rate (color-coded)
- **Status:** 
  - 🟢 Normal (queue ≤ 10)
  - 🟠 Catching up (queue > 10, speed 1.3x)
  - 🔴 Buffering (queue = 0)

---

## Expected Behavior

### Healthy Stream
```
[LISTEN-DIAG] Received #50, queue: 8 ✓ OK, lag: 10 chunks, dropped: 0
[LISTEN-DIAG] Playing chunk #40, queue: 8, speed: 1.0x
[LISTEN-DIAG] Received #100, queue: 9 ✓ OK, lag: 11 chunks, dropped: 0
[LISTEN-DIAG] Playing chunk #90, queue: 9, speed: 1.0x
```

### Catching Up
```
[LISTEN-DIAG] Received #150, queue: 12 ⚠️ OVER TARGET, lag: 18 chunks, dropped: 0
[LISTEN-DIAG] Playing chunk #132, queue: 12, speed: 1.3x
[LISTEN-DIAG] Playing chunk #133, queue: 11, speed: 1.3x
[LISTEN-DIAG] Playing chunk #134, queue: 10, speed: 1.3x
[LISTEN-DIAG] Playing chunk #135, queue: 9, speed: 1.0x  ← Back to normal
```

### Emergency (Should Never Happen)
```
[LISTEN-DIAG] EMERGENCY: Queue exceeded 100 chunks! Dropped oldest. Total dropped: 1
```

---

## Performance Characteristics

### Latency
- **Target:** 10 chunks × 86ms = **860ms**
- **Acceptable range:** 5-15 chunks (430ms - 1290ms)
- **Maximum:** 100 chunks (8.6 seconds before dropping)

### Audio Quality
- **1.0x speed:** Perfect quality, no artifacts
- **1.3x speed:** 
  - Slight pitch increase (~30% higher)
  - Barely noticeable for short catch-up periods
  - Much better than dropped chunks/gaps
  
### Chunk Drop Rate
- **Expected:** 0 drops under normal conditions
- **Emergency only:** If sustained network/CPU issues
- **Previous:** 309 drops → **Target: <5 drops** in 5-minute stream

---

## Testing Checklist

### Functional Tests
- [ ] Stream starts with queue ~2-5 chunks
- [ ] Queue stabilizes at 8-10 chunks
- [ ] Playback speed shows 1.0x when queue ≤ 10
- [ ] Playback speed shows 1.3x when queue > 10
- [ ] Queue returns to <10 after speedup activation
- [ ] No chunks dropped in normal operation
- [ ] Audio continuity maintained (no gaps)

### Performance Tests
- [ ] 5-minute stream with <5 total drops
- [ ] Queue never exceeds 20 chunks for extended periods
- [ ] Speedup activates/deactivates smoothly
- [ ] UI indicators update in real-time
- [ ] Browser console logs show stable lag values

### Edge Cases
- [ ] Network interruption (brief disconnection)
- [ ] High CPU load on listener device
- [ ] Broadcaster pause/resume
- [ ] Multiple listeners simultaneously
- [ ] Mobile device (Android/iOS) as listener

---

## Tuning Parameters

### If Queue Still Grows
**Increase speedup rate:**
```javascript
speedupRate: 1.5  // More aggressive catch-up (was 1.3)
```

**Or decrease target:**
```javascript
targetQueueSize: 5  // Tighter buffer control (was 10)
```

### If Audio Quality Issues at 1.3x
**Decrease speedup rate:**
```javascript
speedupRate: 1.2  // Gentler catch-up (was 1.3)
```

**Or increase target:**
```javascript
targetQueueSize: 15  // More tolerance before speedup (was 10)
```

### If Emergency Drops Still Occur
**Lower emergency threshold:**
```javascript
emergencyDropThreshold: 50  // Earlier intervention (was 100)
```

**Or increase speedup:**
```javascript
speedupRate: 1.5  // Catch up faster (was 1.3)
```

---

## Comparison: Before vs After

| Metric | Before (Drop Strategy) | After (Adaptive Speed) |
|--------|------------------------|------------------------|
| **Chunks dropped** | 309/1924 (16%) | Expected: 0-5 (<1%) |
| **Audio continuity** | Frequent gaps | Smooth playback |
| **Latency** | Unbounded (grew to 347 chunks) | Bounded (~10 chunks) |
| **User experience** | Glitchy, choppy | Natural, fluid |
| **Pitch stability** | Correct but interrupted | Slight variation during catch-up |
| **Buffer strategy** | Hard limit (drop) | Soft limit (adapt) |

---

## Monitoring Commands

### Browser Console (F12)
```javascript
// Check current state
console.log(window.konfAudio);

// Watch queue in real-time (paste in console)
setInterval(() => {
    const q = window.konfAudio?.listen?.queue?.length || 0;
    const speed = window.konfAudio?.listen?.currentPlaybackRate || 1.0;
    console.log(`Queue: ${q}, Speed: ${speed}x`);
}, 1000);
```

### Log Patterns to Watch

**Good:**
```
[LISTEN-DIAG] Received #100, queue: 9 ✓ OK, lag: 12 chunks, dropped: 0
[LISTEN-DIAG] Received #150, queue: 8 ✓ OK, lag: 13 chunks, dropped: 0
```

**Needs attention:**
```
[LISTEN-DIAG] Playing chunk #100, queue: 15, speed: 1.3x  ← Frequent speedups
[LISTEN-DIAG] Playing chunk #120, queue: 18, speed: 1.3x  ← Queue not decreasing
```

**Critical:**
```
[EMERGENCY] Queue exceeded 100 chunks! Dropped oldest. Total dropped: 1
```

---

## Rollback Instructions

If adaptive speed causes issues:

```bash
# Revert to previous dropping strategy
git diff HEAD -- wwwroot/js/audioStream.js Components/Pages/Events/StreamListen.razor
git checkout HEAD~1 -- wwwroot/js/audioStream.js Components/Pages/Events/StreamListen.razor
dotnet build && dotnet run
```

---

## References

- **Web Audio API playbackRate:** https://developer.mozilla.org/en-US/docs/Web/API/AudioBufferSourceNode/playbackRate
- **Session 004:** `/LLM-docs/sessions/session-004-audio-streaming-fix.md`
- **Diagnostics Guide:** `/LLM-docs/audio-streaming-diagnostics-guide.md`

---

## Future Enhancements

### Adaptive Target Buffer
Adjust `targetQueueSize` based on network jitter:
```javascript
// If network is stable: target = 5 chunks (low latency)
// If network is jittery: target = 15 chunks (high stability)
```

### Gradual Speed Adjustment
Instead of binary 1.0x/1.3x, use gradient:
```javascript
// Queue 10: 1.0x
// Queue 15: 1.15x
// Queue 20: 1.3x
```

### Pitch Correction
Apply pitch-preserving time-stretching algorithm to maintain voice naturalness during speedup (requires additional audio processing library).
