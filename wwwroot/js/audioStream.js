// Audio streaming client helpers using SignalR
// Note: requires https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.5/signalr.min.js

window.konfAudio = (function(){
    let listen = {
        connection: null,
        audioCtx: null,
        queue: [],
        playing: false,
        source: null,
        sampleRate: 44100,
        eventId: null,
        slug: null,
        token: null
    };

    let broadcast = {
        connection: null,
        stream: null,
        audioCtx: null,
        processor: null,
        sampleRate: 44100,
        sending: false,
        eventId: null
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
            const bytes = listen.queue.shift();
            console.log(`[appendAndPlay] Processing chunk, size: ${bytes.length} bytes`);
            
            const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
            const samples = new Float32Array(bytes.byteLength / 2);
            for (let i = 0, o = 0; i < bytes.byteLength; i += 2, o++) {
                const s = view.getInt16(i, true);
                samples[o] = s / 0x8000;
            }
            
            ensureAudioContext(listen);
            console.log(`[appendAndPlay] AudioContext state: ${listen.audioCtx.state}`);
            
            const buffer = listen.audioCtx.createBuffer(1, samples.length, listen.sampleRate);
            buffer.getChannelData(0).set(samples);
            const src = listen.audioCtx.createBufferSource();
            src.buffer = buffer;
            src.connect(listen.audioCtx.destination);
            src.onended = function(){
                console.log('[appendAndPlay] Audio buffer finished playing');
                listen.playing = false;
                appendAndPlay();
            };
            src.start();
            listen.source = src;
            console.log('[appendAndPlay] Audio buffer started');
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
        startListening: async function(hubUrl, eventId, slug, token) {
            console.log(`[startListening] Starting for event ${eventId}, slug: ${slug}, token: ${token}`);
            listen.eventId = eventId; listen.slug = slug; listen.token = token || null;
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
                    console.log(`[ReceiveAudio] Received chunk, type: ${typeof chunk}, length: ${chunk?.length || 0}`);
                    
                    // SignalR sends byte[] as Base64 string in JSON
                    let bytes;
                    if (typeof chunk === 'string') {
                        console.log('[ReceiveAudio] Converting from Base64 string');
                        const binaryString = atob(chunk);
                        bytes = new Uint8Array(binaryString.length);
                        for (let i = 0; i < binaryString.length; i++) {
                            bytes[i] = binaryString.charCodeAt(i);
                        }
                    } else {
                        bytes = new Uint8Array(chunk);
                    }
                    
                    console.log(`[ReceiveAudio] Processed chunk, size: ${bytes.length} bytes, queue length: ${listen.queue.length}`);
                    listen.queue.push(bytes);
                    appendAndPlay();
                } catch (error) {
                    console.error('[ReceiveAudio] ERROR:', error);
                }
            });
            
            console.log('[startListening] Starting SignalR connection...');
            await listen.connection.start();
            console.log('[startListening] SignalR connected, calling JoinListener...');
            
            const ok = await listen.connection.invoke("JoinListener", eventId, slug, token || null);
            console.log(`[startListening] JoinListener returned: ${ok}`);
            
            if (!ok) {
                console.warn("JoinListener denied");
                return false;
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
            listen.connection = null;
            listen.queue = [];
            listen.playing = false;
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
                console.log('[startBroadcast] AudioContext created, state:', broadcast.audioCtx.state);
                const source = broadcast.audioCtx.createMediaStreamSource(stream);
                const processor = broadcast.audioCtx.createScriptProcessor(4096, 1, 1);
                console.log('[startBroadcast] Audio processor created');
                
                let chunkCount = 0;
                processor.onaudioprocess = async (e) => {
                    chunkCount++;
                    if (chunkCount === 1) {
                        console.log('[broadcast] First audio process event triggered!');
                    }
                    if (!broadcast.connection || broadcast.connection.state !== 'Connected') {
                        if (chunkCount === 1) {
                            console.log('[broadcast] Connection not ready, state:', broadcast.connection?.state);
                        }
                        return;
                    }
                    const data = e.inputBuffer.getChannelData(0);
                    const bytes = pcmFloatTo16BitPCM(data);
                    
                    if (chunkCount % 100 === 1) {
                        console.log(`[broadcast] Sending chunk #${chunkCount}, size: ${bytes.length} bytes`);
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
        }
    };
})();

