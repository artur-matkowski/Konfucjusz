// node ff-bidi-console.js
const WebSocket = require('ws');

const WS_URL = 'ws://127.0.0.1:9222';
let nextId = 0;
const waiters = new Map();

function send(ws, method, params = {}) {
  const id = ++nextId;
  const msg = { id, method, params };
  return new Promise((resolve, reject) => {
    waiters.set(id, { resolve, reject });
    ws.send(JSON.stringify(msg));
  });
}

function iso() { return new Date().toISOString(); }
function fmtArgs(args) {
  if (!Array.isArray(args)) return '';
  return args.map(a => (a && ('value' in a ? a.value : a.text)) ?? JSON.stringify(a)).join(' ');
}

(async () => {
  const ws = new WebSocket(WS_URL);

  ws.on('open', async () => {
    try {
      await send(ws, 'session.new', { capabilities: {} });
      await send(ws, 'session.subscribe', { events: ['log.entryAdded'], contexts: [] });

      const tree = await send(ws, 'browsingContext.getTree', {});
      const ctxs = (tree.result.contexts || []).map(c => `${c.context}:${c.url || ''}`).join(' | ');
      console.error('Attached to contexts:', ctxs || '(none)');
      console.error('Listening. Use console.log() in any tab. Ctrl+C to exit.');
    } catch (e) {
      console.error('Init error:', e);
      process.exit(1);
    }
  });

  ws.on('message', (data) => {
    const msg = JSON.parse(data);
    if (msg.id && waiters.has(msg.id)) {
      const { resolve, reject } = waiters.get(msg.id);
      waiters.delete(msg.id);
      return msg.error ? reject(new Error(String(msg.error))) : resolve(msg);
    }
    if (msg.method === 'log.entryAdded') {
      const e = msg.params;
      const url = e.stackTrace?.callFrames?.[0]?.url || '';
      const line = e.stackTrace?.callFrames?.[0]?.lineNumber ?? '';
      if (e.source === 'console-api') {
        const text = e.text || fmtArgs(e.args);
        console.log(`[CONSOLE ${e.level?.toUpperCase() || 'LOG'}] ${iso()} ${text}${url ? ` (${url}:${line})` : ''}`);
      } else if (e.source === 'javascript') {
        console.log(`[JS ${e.level?.toUpperCase() || 'ERROR'}] ${iso()} ${e.text || ''}${url ? ` (${url}:${line})` : ''}`);
      } else {
        console.log(`[${(e.source || 'log').toUpperCase()}] ${iso()} ${e.text || ''}`);
      }
    }
  });

  ws.on('error', err => console.error('WS error:', err));
})();
