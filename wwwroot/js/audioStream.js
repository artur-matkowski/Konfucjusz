// Audio streaming client helpers using SignalR
// Note: requires https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.5/signalr.min.js

window.konfAudio = (function(){
    let listen = {
        connection: null,
        audioCtx: null,
        queue: [],
        playing: false,
        source: null,
        sampleRate: 44100, // Will be updated from broadcaster
        eventId: null,
        slug: null,
        token: null,
        dotNetRef: null,
        lastPlayTime: 0,
        totalChunksReceived: 0,
        totalChunksPlayed: 0,
        totalChunksDropped: 0,
        maxQueueSize: 10 // Maximum chunks to buffer (roughly 850ms at 48kHz)
    };

    let broadcast = {
        connection: null,
        stream: null,
        audioCtx: null,
        processor: null,
        sampleRate: null, // Will be detected from AudioContext
        sending: false,
        eventId: null,
        chunksSent: 0
    };

    function ensureAudioContext(obj){
        if (!obj.audioCtx) {
            obj.audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        }
    }

    function pcmFloatTo16BitPCM(float32Array) {
        const buffer = new ArrayBuffer(float32Array.length * 2);
        const view = new DataView(buffer);
        let offset = 0;
        for (let i = 0; i < float32Array.length; i++, offset += 2) {
            let s = Math.max(-1, Math.min(1, float32Array[i]));
            view.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7FFF, true);
        }
        return new Uint8Array(buffer);
    }

    function appendAndPlay() {
        if (listen.playing || listen.queue.length === 0) return;
        listen.playing = true;
        
        try {
            const now = Date.now();
            const timeSinceLastPlay = listen.lastPlayTime ? (now - listen.lastPlayTime) : 0;
            
            const bytes = listen.queue.shift();
            const queueLengthAfter = listen.queue.length;
            listen.totalChunksPlayed++;
            
            console.log(`[LISTEN-DIAG] Playing chunk #${listen.totalChunksPlayed}, size: ${bytes.length} bytes, queue: ${queueLengthAfter}, gap: ${timeSinceLastPlay}ms, sampleRate: ${listen.sampleRate}Hz`);
            
            const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
            const samples = new Float32Array(bytes.byteLength / 2);
            for (let i = 0, o = 0; i < bytes.byteLength; i += 2, o++) {
                const s = view.getInt16(i, true);
                samples[o] = s / 0x8000;
            }
            
            ensureAudioContext(listen);
            const chunkDurationMs = (samples.length / listen.sampleRate) * 1000;
            console.log(`[LISTEN-DIAG] Chunk duration: ${chunkDurationMs.toFixed(2)}ms, AudioContext.sampleRate: ${listen.audioCtx.sampleRate}Hz`);
            
            const buffer = listen.audioCtx.createBuffer(1, samples.length, listen.sampleRate);
            buffer.getChannelData(0).set(samples);
            const src = listen.audioCtx.createBufferSource();
            src.buffer = buffer;
            src.connect(listen.audioCtx.destination);
            src.onended = function(){
                const playbackDuration = Date.now() - now;
                console.log(`[LISTEN-DIAG] Chunk finished playing, actual duration: ${playbackDuration}ms, expected: ${chunkDurationMs.toFixed(2)}ms`);
                listen.playing = false;
                listen.lastPlayTime = Date.now();
                appendAndPlay();
            };
            src.start();
            listen.source = src;
        } catch (error) {
            console.error('[appendAndPlay] ERROR:', error);
            listen.playing = false;
            // Try to continue with next chunk
            if (listen.queue.length > 0) {
                setTimeout(() => appendAndPlay(), 100);
            }
        }
    }

    return {
        startListening: async function(hubUrl, eventId, slug, token, dotNetRef) {
            console.log(`[startListening] Starting for event ${eventId}, slug: ${slug}, token: ${token}`);
            console.log(`[startListening] dotNetRef parameter received:`, dotNetRef);
            console.log(`[startListening] dotNetRef type:`, typeof dotNetRef);
            listen.eventId = eventId; listen.slug = slug; listen.token = token || null;
            listen.dotNetRef = dotNetRef || null;
            console.log(`[startListening] Stored dotNetRef in listen object:`, listen.dotNetRef);
            ensureAudioContext(listen);
            
            // Resume AudioContext if suspended (required by browser autoplay policies)
            if (listen.audioCtx.state === 'suspended') {
                console.log('[startListening] AudioContext suspended, attempting to resume...');
                await listen.audioCtx.resume();
                console.log(`[startListening] AudioContext state after resume: ${listen.audioCtx.state}`);
            }
            
            if (listen.connection) {
                console.log('[startListening] Stopping existing connection');
                await listen.connection.stop();
            }
            
            listen.connection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl)
                .withAutomaticReconnect()
                .build();
            
            listen.connection.on("ReceiveAudio", (chunk) => {
                try {
                    listen.totalChunksReceived++;
                    
                    // SignalR sends byte[] as Base64 string in JSON
                    let bytes;
                    if (typeof chunk === 'string') {
                        const binaryString = atob(chunk);
                        bytes = new Uint8Array(binaryString.length);
                        for (let i = 0; i < binaryString.length; i++) {
                            bytes[i] = binaryString.charCodeAt(i);
                        }
                    } else {
                        bytes = new Uint8Array(chunk);
                    }
                    
                    const queueLengthBefore = listen.queue.length;
                    
                    // Drop old chunks if queue is too full (prevent unbounded growth)
                    if (listen.queue.length >= listen.maxQueueSize) {
                        const dropped = listen.queue.shift(); // Drop oldest chunk
                        listen.totalChunksDropped++;
                        console.warn(`[LISTEN-DIAG] Queue full (${listen.maxQueueSize}), dropped oldest chunk. Total dropped: ${listen.totalChunksDropped}`);
                    }
                    
                    listen.queue.push(bytes);
                    
                    // Log every 50th chunk to avoid console spam
                    if (listen.totalChunksReceived % 50 === 0) {
                        const lag = listen.totalChunksReceived - listen.totalChunksPlayed - listen.totalChunksDropped;
                        console.log(`[LISTEN-DIAG] Received chunk #${listen.totalChunksReceived}, size: ${bytes.length}B, queue: ${queueLengthBefore}→${listen.queue.length}, lag: ${lag} chunks, dropped: ${listen.totalChunksDropped}`);
                    }
                    
                    appendAndPlay();
                } catch (error) {
                    console.error('[ReceiveAudio] ERROR:', error);
                }
            });
            
            // Handle stream lifecycle events
            listen.connection.on("StreamStarted", (sampleRate) => {
                console.log(`[LISTEN-DIAG] StreamStarted event received - Sample rate: ${sampleRate}Hz`);
                console.log(`[LISTEN-DIAG] Current local sample rate: ${listen.sampleRate}Hz`);
                
                // Update sample rate from broadcaster
                if (sampleRate && sampleRate > 0) {
                    listen.sampleRate = sampleRate;
                    console.log(`[LISTEN-DIAG] Updated sample rate to: ${listen.sampleRate}Hz`);
                }
                
                console.log('[StreamStarted] dotNetRef exists:', !!listen.dotNetRef);
                if (listen.dotNetRef) {
                    console.log('[StreamStarted] Calling dotNetRef.invokeMethodAsync with state: active');
                    listen.dotNetRef.invokeMethodAsync('OnStreamStateChanged', 'active')
                        .then(() => {
                            console.log('[StreamStarted] Successfully invoked OnStreamStateChanged');
                        })
                        .catch(err => {
                            console.error('[StreamStarted] ERROR invoking OnStreamStateChanged:', err);
                        });
                } else {
                    console.error('[StreamStarted] ERROR: dotNetRef is null or undefined!');
                }
            });
            
            listen.connection.on("StreamEnded", () => {
                console.log('[StreamEnded] Event received - Stream has finished');
                console.log('[StreamEnded] dotNetRef exists:', !!listen.dotNetRef);
                if (listen.dotNetRef) {
                    console.log('[StreamEnded] Calling dotNetRef.invokeMethodAsync with state: ended');
                    listen.dotNetRef.invokeMethodAsync('OnStreamStateChanged', 'ended')
                        .then(() => {
                            console.log('[StreamEnded] Successfully invoked OnStreamStateChanged');
                        })
                        .catch(err => {
                            console.error('[StreamEnded] ERROR invoking OnStreamStateChanged:', err);
                        });
                } else {
                    console.error('[StreamEnded] ERROR: dotNetRef is null or undefined!');
                }
            });
            
            console.log('[startListening] Starting SignalR connection...');
            await listen.connection.start();
            console.log('[startListening] SignalR connected, calling JoinListener...');
            
            const result = await listen.connection.invoke("JoinListener", eventId, slug, token || null);
            console.log(`[startListening] JoinListener returned:`, result);
            
            // Handle both old (boolean) and new (tuple) return format
            let ok, sampleRate;
            if (typeof result === 'boolean') {
                ok = result;
                sampleRate = 44100; // Fallback
            } else {
                ok = result.success;
                sampleRate = result.sampleRate;
            }
            
            if (!ok) {
                console.warn("JoinListener denied");
                return false;
            }
            
            // Set sample rate from server response
            if (sampleRate && sampleRate > 0) {
                listen.sampleRate = sampleRate;
                console.log(`[LISTEN-DIAG] Sample rate set from JoinListener: ${listen.sampleRate}Hz`);
            } else {
                console.warn(`[LISTEN-DIAG] Invalid sample rate from server: ${sampleRate}, using default 44100Hz`);
            }
            
            return true;
        },
        stopListening: async function(){
            try {
                if (listen.connection) {
                    await listen.connection.invoke("LeaveListener", listen.eventId);
                    await listen.connection.stop();
                }
            } catch {}
            
            // Log final statistics
            console.log(`[LISTEN-DIAG] Session ended - Received: ${listen.totalChunksReceived}, Played: ${listen.totalChunksPlayed}, Dropped: ${listen.totalChunksDropped}`);
            
            listen.connection = null;
            listen.queue = [];
            listen.playing = false;
            listen.dotNetRef = null;
            listen.totalChunksReceived = 0;
            listen.totalChunksPlayed = 0;
            listen.totalChunksDropped = 0;
        },
        startBroadcast: async function(hubUrl, eventId) {
            try {
                if (typeof signalR === 'undefined') {
                    throw new Error('SignalR is not loaded. Please ensure the SignalR script is included before audioStream.js');
                }
                console.log(`[startBroadcast] Starting for event ${eventId}, hubUrl: ${hubUrl}`);
                console.log(`[startBroadcast] Current location: ${window.location.href}`);
                
                // Clean up any existing audio pipeline first
                if (broadcast.processor) {
                    console.log('[startBroadcast] Disconnecting existing processor');
                    broadcast.processor.disconnect();
                    broadcast.processor.onaudioprocess = null;
                    broadcast.processor = null;
                }
                if (broadcast.stream) {
                    console.log('[startBroadcast] Stopping existing media stream');
                    broadcast.stream.getTracks().forEach(track => track.stop());
                    broadcast.stream = null;
                }
                if (broadcast.audioCtx) {
                    console.log('[startBroadcast] Closing existing AudioContext');
                    await broadcast.audioCtx.close();
                    broadcast.audioCtx = null;
                }
                
                broadcast.eventId = eventId;
                if (broadcast.connection) {
                    console.log('[startBroadcast] Stopping existing connection');
                    await broadcast.connection.stop();
                }
                
                broadcast.connection = new signalR.HubConnectionBuilder()
                    .withUrl(hubUrl)
                    .withAutomaticReconnect()
                    .build();
                
                console.log('[startBroadcast] Starting SignalR connection...');
                await broadcast.connection.start();
                console.log('[startBroadcast] SignalR connection established, state:', broadcast.connection.state);

                console.log('[startBroadcast] Requesting microphone access...');
                const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                console.log('[startBroadcast] Microphone access granted');
                
                broadcast.stream = stream;
                broadcast.audioCtx = new (window.AudioContext || window.webkitAudioContext)();
                
                // Detect actual sample rate from AudioContext (device-dependent)
                broadcast.sampleRate = broadcast.audioCtx.sampleRate;
                console.log('[BROADCAST-DIAG] AudioContext created');
                console.log('[BROADCAST-DIAG] Detected sample rate:', broadcast.sampleRate, 'Hz');
                console.log('[BROADCAST-DIAG] AudioContext state:', broadcast.audioCtx.state);
                
                const source = broadcast.audioCtx.createMediaStreamSource(stream);
                const processor = broadcast.audioCtx.createScriptProcessor(4096, 1, 1);
                
                const chunkDurationMs = (4096 / broadcast.sampleRate) * 1000;
                console.log('[BROADCAST-DIAG] Audio processor created, buffer size: 4096 samples');
                console.log('[BROADCAST-DIAG] Expected chunk interval:', chunkDurationMs.toFixed(2), 'ms');
                
                let chunkCount = 0;
                let lastChunkTime = null;
                processor.onaudioprocess = async (e) => {
                    chunkCount++;
                    broadcast.chunksSent++;
                    
                    const now = Date.now();
                    const intervalMs = lastChunkTime ? (now - lastChunkTime) : 0;
                    lastChunkTime = now;
                    
                    if (chunkCount === 1) {
                        console.log('[BROADCAST-DIAG] First audio process event triggered!');
                        console.log('[BROADCAST-DIAG] Sample rate:', broadcast.sampleRate, 'Hz');
                    }
                    if (!broadcast.connection || broadcast.connection.state !== 'Connected') {
                        if (chunkCount === 1) {
                            console.log('[broadcast] Connection not ready, state:', broadcast.connection?.state);
                        }
                        return;
                    }
                    const data = e.inputBuffer.getChannelData(0);
                    const bytes = pcmFloatTo16BitPCM(data);
                    
                    // Log every 100th chunk
                    if (chunkCount % 100 === 0) {
                        console.log(`[BROADCAST-DIAG] Chunk #${chunkCount}: ${bytes.length}B, interval: ${intervalMs}ms, sampleRate: ${broadcast.sampleRate}Hz`);
                    }
                    try {
                        // Convert Uint8Array to Base64 string for SignalR JSON serialization
                        const base64 = btoa(String.fromCharCode.apply(null, bytes));
                        await broadcast.connection.invoke("BroadcastAudioChunk", broadcast.eventId, base64);
                    } catch (err) {
                        console.error("[broadcast] Error sending chunk:", err);
                        if (chunkCount === 1) {
                            console.error("[broadcast] Full error details:", err);
                        }
                    }
                };
                
                source.connect(processor);
                processor.connect(broadcast.audioCtx.destination);
                broadcast.processor = processor;
                console.log('[startBroadcast] Audio pipeline connected successfully');
                return true;
            } catch (error) {
                console.error('[startBroadcast] ERROR:', error);
                console.error('[startBroadcast] Error stack:', error.stack);
                throw error;
            }
        },
        startRecording: async function(eventId) {
            console.log(`[startRecording] Called for event ${eventId}`);
            if (!broadcast.connection || broadcast.connection.state !== 'Connected') {
                console.error('[startRecording] No active broadcast connection!');
                throw new Error('Must start broadcast before recording');
            }
            console.log('[startRecording] Invoking StartRecording on broadcast connection...');
            const result = await broadcast.connection.invoke("StartRecording", eventId);
            console.log(`[startRecording] Result: ${result}`);
            return result;
        },
        stopRecording: async function(eventId) {
            console.log(`[stopRecording] Called for event ${eventId}`);
            if (!broadcast.connection || broadcast.connection.state !== 'Connected') {
                console.error('[stopRecording] No active broadcast connection!');
                return null;
            }
            console.log('[stopRecording] Invoking StopRecording on broadcast connection...');
            const result = await broadcast.connection.invoke("StopRecording", eventId);
            console.log(`[stopRecording] Result: ${result}`);
            return result;
        },
        stopBroadcast: async function(){
            try {
                if (broadcast.processor) broadcast.processor.disconnect();
                if (broadcast.stream) {
                    broadcast.stream.getTracks().forEach(t => t.stop());
                }
                // Note: Don't stop connection immediately - let invoke handle it
                // This allows StopRecording to be called while connection is still active
            } catch {}
            broadcast.processor = null;
            broadcast.stream = null;
        },
        closeConnection: async function(){
            console.log('[closeConnection] Called, processor active:', !!broadcast.processor);
            
            // Don't close connection if we're actively broadcasting
            if (broadcast.processor) {
                console.log('[closeConnection] Ignoring - broadcast is active');
                return;
            }
            
            try {
                if (broadcast.connection && 
                    broadcast.connection.state !== 'Disconnected' && 
                    broadcast.connection.state !== 'Disconnecting') {
                    console.log('[closeConnection] Stopping connection');
                    await broadcast.connection.stop();
                }
            } catch (err) {
                // Suppress connection close errors - they're expected during cleanup
                console.log("Connection close (expected):", err.message);
            }
            broadcast.connection = null;
        },
        invoke: async function(hubUrl, method, ...args){
            // Use existing broadcast connection if available and connected
            console.log(`[invoke] Called for ${method}, broadcast.connection exists: ${!!broadcast.connection}, state: ${broadcast.connection?.state}`);
            
            if (broadcast.connection && broadcast.connection.state === 'Connected') {
                console.log(`[invoke] Using existing broadcast connection for ${method}`);
                return await broadcast.connection.invoke(method, ...args);
            }
            
            // Otherwise create a temporary connection
            console.log(`[invoke] Creating temporary connection for ${method}`);
            const tempConnection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl)
                .withAutomaticReconnect()
                .build();
            await tempConnection.start();
            console.log(`[invoke] Temporary connection started for ${method}`);
            try {
                const result = await tempConnection.invoke(method, ...args);
                console.log(`[invoke] ${method} completed successfully`);
                return result;
            } finally {
                console.log(`[invoke] Stopping temporary connection for ${method}`);
                await tempConnection.stop();
            }
        },
        
        // Manager connection for tracking listeners
        joinManager: async function(hubUrl, eventId, dotNetRef) {
            try {
                console.log(`[joinManager] Joining for event ${eventId}, hubUrl: ${hubUrl}`);
                if (broadcast.managerConnection) {
                    console.log('[joinManager] Stopping existing manager connection');
                    await broadcast.managerConnection.stop();
                }
                
                broadcast.managerConnection = new signalR.HubConnectionBuilder()
                    .withUrl(hubUrl)
                    .withAutomaticReconnect()
                    .build();
                
                // Handle listener events
                broadcast.managerConnection.on("ListenersSnapshot", (listeners) => {
                    console.log(`[joinManager] Received ListenersSnapshot:`, listeners);
                    dotNetRef.invokeMethodAsync('UpdateListeners', listeners);
                });
                
                broadcast.managerConnection.on("ListenerJoined", (cid, display) => {
                    console.log(`[joinManager] Listener joined: ${display} (${cid})`);
                    dotNetRef.invokeMethodAsync('AddListener', cid, display);
                });
                
                broadcast.managerConnection.on("ListenerLeft", (cid) => {
                    console.log(`[joinManager] Listener left: ${cid}`);
                    dotNetRef.invokeMethodAsync('RemoveListener', cid);
                });
                
                // Start connection and wait for it to be ready
                console.log('[joinManager] Starting SignalR connection...');
                await broadcast.managerConnection.start();
                console.log('[joinManager] SignalR connection established, state:', broadcast.managerConnection.state);
                
                // Now join the manager group
                console.log('[joinManager] Invoking JoinManager...');
                await broadcast.managerConnection.invoke("JoinManager", eventId);
                console.log('[joinManager] Successfully joined manager group');
            } catch (err) {
                console.error("[joinManager] ERROR:", err);
                console.error("[joinManager] Error stack:", err.stack);
            }
        },
        
        leaveManager: async function() {
            if (broadcast.managerConnection) {
                try {
                    await broadcast.managerConnection.stop();
                } catch {}
                broadcast.managerConnection = null;
            }
        },
        
        // Helper to get current broadcast sample rate
        getSampleRate: function() {
            return broadcast.sampleRate || 44100;
        }
    };
})();

// Expose helper globally for Blazor eval access
window.konfAudio_getSampleRate = function() {
    return window.konfAudio.getSampleRate();
};

