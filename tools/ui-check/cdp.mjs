// Minimal Chrome DevTools Protocol driver. Exists so pointer-media emulation and real touch
// drags can be asserted rather than assumed; nothing else available here can change what
// (pointer: coarse) reports.
import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
// Overridable so two checkouts can each drive their own Chrome at once.
const PORT = Number(process.env.CDP_LAUNCH_PORT ?? 9333);

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function endpoint() {
  for (let i = 0; i < 100; i++) {
    try {
      const r = await fetch(`http://127.0.0.1:${PORT}/json/version`);
      return (await r.json()).webSocketDebuggerUrl;
    } catch {
      await sleep(100);
    }
  }
  throw new Error('chrome did not open a debugging port');
}

export async function launch({ headless = false } = {}) {
  const profile = mkdtempSync(join(tmpdir(), 'pf-cdp-'));
  const args = [
    `--remote-debugging-port=${PORT}`,
    `--user-data-dir=${profile}`,
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-extensions',
    '--allow-file-access-from-files',
    'about:blank',
  ];
  if (headless) args.unshift('--headless=new');
  const proc = spawn(CHROME, args, { stdio: 'ignore', detached: false });

  const ws = new WebSocket(await endpoint());
  await new Promise((res, rej) => {
    ws.addEventListener('open', res, { once: true });
    ws.addEventListener('error', rej, { once: true });
  });

  let next = 1;
  const pending = new Map();
  const events = [];
  ws.addEventListener('message', (e) => {
    const msg = JSON.parse(e.data);
    if (msg.id && pending.has(msg.id)) {
      const { resolve, reject } = pending.get(msg.id);
      pending.delete(msg.id);
      msg.error ? reject(new Error(`${msg.error.message} (${JSON.stringify(msg.error.data ?? '')})`)) : resolve(msg.result);
    } else if (msg.method) {
      events.push(msg);
    }
  });

  const send = (method, params = {}, sessionId) =>
    new Promise((resolve, reject) => {
      const id = next++;
      pending.set(id, { resolve, reject });
      ws.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }));
    });

  return {
    send,
    events,
    async close() {
      try { ws.close(); } catch {}
      try { proc.kill(); } catch {}
      await sleep(300);
      try { rmSync(profile, { recursive: true, force: true }); } catch {}
    },
  };
}

export async function openPage(browser, url) {
  const { targetId } = await browser.send('Target.createTarget', { url: 'about:blank' });
  const { sessionId } = await browser.send('Target.attachToTarget', { targetId, flatten: true });
  const s = (method, params) => browser.send(method, params, sessionId);

  await s('Page.enable');
  await s('Runtime.enable');
  await s('Log.enable');

  const page = {
    sessionId,
    send: s,
    consoleErrors: () =>
      browser.events
        .filter((e) => e.sessionId === sessionId)
        .filter((e) => e.method === 'Log.entryAdded' && e.params.entry.level === 'error')
        .map((e) => e.params.entry.text),
    async goto(target) {
      await s('Page.navigate', { url: target });
      await page.settle();
    },
    async settle() {
      for (let i = 0; i < 120; i++) {
        const r = await s('Runtime.evaluate', { expression: 'document.readyState', returnByValue: true });
        if (r.result.value === 'complete') { await sleep(250); return; }
        await sleep(100);
      }
      throw new Error('page never reached readyState complete');
    },
    async eval(expression) {
      const r = await s('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
      if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? 'evaluate threw');
      return r.result.value;
    },
    async key(key, code, vk) {
      for (const type of ['rawKeyDown', 'keyUp']) {
        await s('Input.dispatchKeyEvent', { type, key, code, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk });
      }
      await sleep(40);
    },
    async touchDrag(from, to, steps = 12) {
      await s('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: from.x, y: from.y }] });
      for (let i = 1; i <= steps; i++) {
        const x = from.x + ((to.x - from.x) * i) / steps;
        const y = from.y + ((to.y - from.y) * i) / steps;
        await s('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x, y }] });
        await sleep(16);
      }
      await s('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
      await sleep(120);
    },
    async mouseDown(x, y) {
      await s('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 1, buttons: 1 });
    },
    async mouseUp(x, y) {
      await s('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 1, buttons: 0 });
    },
    async coarse(on) {
      if (on) {
        await s('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 5 });
        await s('Emulation.setEmitTouchEventsForMouse', { enabled: true, configuration: 'mobile' });
      } else {
        await s('Emulation.setEmitTouchEventsForMouse', { enabled: false, configuration: 'desktop' });
        await s('Emulation.setTouchEmulationEnabled', { enabled: false });
      }
      await sleep(150);
    },
    async viewport(width, height, mobile) {
      await s('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile });
      await sleep(150);
    },
  };

  if (url) await page.goto(url);
  return page;
}

export function reporter() {
  let failures = 0;
  const check = (label, ok, detail = '') => {
    if (!ok) failures++;
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${label.padEnd(58)} ${detail}`);
  };
  check.eq = (label, actual, expected, detail) =>
    check(label, String(actual) === String(expected), detail ?? `got ${actual}, want ${expected}`);
  check.done = () => {
    console.log(`\n${failures} failure(s)`);
    return failures;
  };
  return check;
}

export { sleep };
