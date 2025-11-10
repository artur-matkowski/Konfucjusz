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
        const bytes = listen.queue.shift();
        const float32 = new Float32Array(bytes.buffer, bytes.byteOffset, bytes.byteLength / 2);
        // reconstruct from PCM16 -> Float32
        const len = float32.length; // wrong length; reconstruct properly
        const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
        const samples = new Float32Array(bytes.byteLength / 2);
        for (let i = 0, o = 0; i < bytes.byteLength; i += 2, o++) {
            const s = view.getInt16(i, true);
            samples[o] = s / 0x8000;
        }
        ensureAudioContext(listen);
        const buffer = listen.audioCtx.createBuffer(1, samples.length, listen.sampleRate);
        buffer.getChannelData(0).set(samples);
        const src = listen.audioCtx.createBufferSource();
        src.buffer = buffer;
        src.connect(listen.audioCtx.destination);
        src.onended = function(){
            listen.playing = false;
            appendAndPlay();
        };
        src.start();
        listen.source = src;
    }

    return {
        startListening: async function(hubUrl, eventId, slug, token) {
            listen.eventId = eventId; listen.slug = slug; listen.token = token || null;
            ensureAudioContext(listen);
            if (listen.connection) await listen.connection.stop();
            listen.connection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl)
                .withAutomaticReconnect()
                .build();
            listen.connection.on("ReceiveAudio", (chunk) => {
                listen.queue.push(new Uint8Array(chunk));
                appendAndPlay();
            });
            await listen.connection.start();
            const ok = await listen.connection.invoke("JoinListener", eventId, slug, token || null);
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
            if (typeof signalR === 'undefined') {
                throw new Error('SignalR is not loaded. Please ensure the SignalR script is included before audioStream.js');
            }
            broadcast.eventId = eventId;
            if (broadcast.connection) await broadcast.connection.stop();
            broadcast.connection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl)
                .withAutomaticReconnect()
                .build();
            await broadcast.connection.start();

            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            broadcast.stream = stream;
            broadcast.audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            const source = broadcast.audioCtx.createMediaStreamSource(stream);
            const processor = broadcast.audioCtx.createScriptProcessor(4096, 1, 1);
            processor.onaudioprocess = async (e) => {
                if (!broadcast.connection) return;
                const data = e.inputBuffer.getChannelData(0);
                const bytes = pcmFloatTo16BitPCM(data);
                try {
                    await broadcast.connection.invoke("BroadcastAudioChunk", broadcast.eventId, bytes);
                } catch (err) {
                    console.error("broadcast error", err);
                }
            };
            source.connect(processor);
            processor.connect(broadcast.audioCtx.destination);
            broadcast.processor = processor;
            return true;
        },
        stopBroadcast: async function(){
            try {
                if (broadcast.processor) broadcast.processor.disconnect();
                if (broadcast.stream) {
                    broadcast.stream.getTracks().forEach(t => t.stop());
                }
                if (broadcast.connection) await broadcast.connection.stop();
            } catch {}
            broadcast.connection = null;
            broadcast.processor = null;
            broadcast.stream = null;
        },
        invoke: async function(hubUrl, method, ...args){
            if (!broadcast.connection) {
                broadcast.connection = new signalR.HubConnectionBuilder()
                    .withUrl(hubUrl)
                    .withAutomaticReconnect()
                    .build();
                await broadcast.connection.start();
            }
            return await broadcast.connection.invoke(method, ...args);
        }
    };
})();
