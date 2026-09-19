// Records a scripted walkthrough of the running app as frames, through the Chrome DevTools
// Protocol. Frames are captured under the same emulation override tools/ui-check/measure.mjs
// uses, so the footage and the measurements cannot disagree about what the viewport was.
//
//   node tools/ui-check/record.mjs <outputDir> [scenario...]
//
// Needs a Chrome started with --remote-debugging-port=9222 and the app running.

import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [outDir, ...only] = process.argv.slice(2);
if (!outDir) throw new Error('usage: node tools/ui-check/record.mjs <outputDir> [scenario...]');

const port = process.env.CDP_PORT ?? '9222';
const client = process.env.CLIENT_URL ?? 'http://localhost:5173';
const gallery = process.env.GALLERY_URL;
const WIDTH = Number(process.env.WIDTH ?? 390);
const HEIGHT = Number(process.env.HEIGHT ?? 844);

// Structural, so a renamed component cannot make the recorder claim the app never rendered.
const NAV = 'nav button, nav a, [role=navigation] button, [role=navigation] a';

const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
const target = targets.find(t => t.type === 'page');
if (!target) throw new Error('no page target; start chrome with --remote-debugging-port');

const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise(resolve => socket.addEventListener('open', resolve, { once: true }));

let nextId = 0;
const pending = new Map();
socket.addEventListener('message', event => {
  const message = JSON.parse(event.data);
  const resolve = pending.get(message.id);
  if (resolve) {
    pending.delete(message.id);
    resolve(message);
  }
});

const send = (method, params = {}) => new Promise(resolve => {
  const id = ++nextId;
  pending.set(id, resolve);
  socket.send(JSON.stringify({ id, method, params }));
});

async function evaluate(expression) {
  const response = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
  if (response.error) throw new Error(`CDP ${response.error.message}`);
  const { result, exceptionDetails } = response.result;
  if (exceptionDetails) throw new Error(exceptionDetails.exception?.description ?? exceptionDetails.text);
  return result.value;
}

const sleep = ms => new Promise(r => setTimeout(r, ms));

await send('Runtime.enable');
await send('Page.enable');
await send('Emulation.setDeviceMetricsOverride', {
  width: WIDTH, height: HEIGHT, deviceScaleFactor: 1, mobile: WIDTH < 1024,
});
await send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 5 });

mkdirSync(outDir, { recursive: true });

let frameNumber = 0;
const frames = [];

async function frame(caption) {
  const response = await send('Page.captureScreenshot', { format: 'jpeg', quality: 78 });
  // Navigating between origins can swap the CDP target out from under the session, and the
  // capture then comes back empty. Losing a frame must not lose the whole recording.
  if (!response.result?.data) {
    console.error(`dropped a frame: ${response.error?.message ?? 'empty capture'}`);
    return;
  }
  const name = `f${String(frameNumber++).padStart(4, '0')}.jpg`;
  writeFileSync(join(outDir, name), Buffer.from(response.result.data, 'base64'));
  frames.push({ name, caption });
}

// Several frames across a span, so a transition or a list settling is visible rather than jumping.
async function frames_over(ms, caption, count = 4) {
  const step = Math.max(40, Math.round(ms / count));
  for (let i = 0; i < count; i++) {
    await sleep(step);
    await frame(caption);
  }
}

// Everything below finds elements by what they say and where they sit, never by a class name.
// A recorder keyed on classes breaks the moment a component is swapped, and then reports a
// failure about itself rather than about the app. The kit migration renamed every one of them.

const clickNav = text => evaluate(`(() => {
  const nav = document.querySelector('nav, [role=navigation]');
  const el = [...nav.querySelectorAll('button, a')]
    .find(e => e.textContent.trim().toLowerCase().startsWith(${JSON.stringify(text.toLowerCase())}));
  if (!el) throw new Error('no nav item starting with ' + ${JSON.stringify(text)});
  el.click();
  return true;
})()`);

// The first list on the page that is not the navigation, whatever markup it happens to use.
const clickRow = text => evaluate(`(() => {
  const nav = document.querySelector('nav, [role=navigation]');
  const rows = [...document.querySelectorAll('button, a')].filter(e => !nav?.contains(e));
  const el = ${JSON.stringify(text)}
    ? rows.find(e => e.textContent.trim().toLowerCase().includes(${JSON.stringify(String(text).toLowerCase())}))
    : rows[0];
  if (!el) throw new Error('no row' + (${JSON.stringify(text)} ? ' containing ' + ${JSON.stringify(text)} : ''));
  el.scrollIntoView({ block: 'center' });
  el.click();
  return true;
})()`);

const searchBox = () =>
  `document.querySelector('input[type=search]') ?? document.querySelector('input[inputmode=search]')`;

// One character at a time through real key events, so the debounce and the last-keystroke-wins
// cancellation are exercised rather than bypassed by assigning .value.
async function typeSearch(value, caption) {
  await evaluate(`(${searchBox()})?.focus()`);
  for (const character of value) {
    await send('Input.dispatchKeyEvent', { type: 'keyDown', text: character });
    await send('Input.dispatchKeyEvent', { type: 'keyUp' });
    await sleep(90);
    await frame(caption);
  }
}

const waitFor = (selector, timeout = 20000) => evaluate(`new Promise(done => {
  const ready = () => document.querySelector(${JSON.stringify(selector)});
  if (ready()) return done(true);
  const observer = new MutationObserver(() => { if (ready()) { observer.disconnect(); done(true); } });
  observer.observe(document.documentElement, { childList: true, subtree: true });
  setTimeout(() => done(false), ${timeout});
})`);

async function goto(url, settleSelector) {
  await send('Page.navigate', { url });
  if (settleSelector) {
    const ok = await waitFor(settleSelector);
    if (!ok) throw new Error(`${url} never rendered ${settleSelector}`);
  }
  await sleep(400);
}

const scenarios = {
  async browse() {
    await goto(`${client}/`, NAV);
    await frames_over(400, 'Six groups. On a phone they sit in the thumb arc.', 2);
    await clickNav('feats');
    await frames_over(700, 'The Feats group and its categories.', 4);
    await clickRow('feats');
    await sleep(900);
    await frames_over(900, 'All 6,390 feats, paged fifty at a time.', 5);
    await typeSearch('power', 'Typing filters as you go, one keystroke at a time.');
    await frames_over(1400, 'The last keystroke wins; earlier requests are cancelled.', 6);
  },

  async detail() {
    await goto(`${client}/`, NAV);
    await clickNav('gear');
    await frames_over(600, 'The Gear group.', 3);
    await clickRow('weapon');
    await sleep(900);
    await frames_over(700, 'Weapons.', 4);
    await clickRow('club');
    await frames_over(1300, 'A record opens with its mechanics as labelled pairs and a link to its source. On a wide screen it docks beside the list so you keep your place.', 8);
  },

  async conditions() {
    await goto(`${client}/conditions`, NAV);
    await frames_over(800, 'The fourteen conditions the rules engine can actually compute.', 4);
    for (const [top, caption] of [
      [380, 'Clumsy is every Dexterity-based statistic, not a list of five.'],
      [760, 'Drained is Constitution-based, and also costs hit points per level.'],
      [1140, 'Stupefied reaches Intelligence, Wisdom and Charisma.'],
    ]) {
      await evaluate(`window.scrollTo({ top: ${top}, behavior: 'instant' })`);
      await frames_over(420, caption, 3);
    }
  },

  async sliders() {
    if (!gallery) return;
    await goto(gallery, 'input[type=range]');
    await evaluate(`document.querySelector('input[type=range]').closest('section, div').scrollIntoView({ block: 'center' })`);
    await frame('Slider and RangeSlider, from the component kit.');
    const rails = await evaluate(`document.querySelectorAll('input[type=range]').length`);
    for (let value = 3; value <= 18; value += 3) {
      await evaluate(`(() => {
        const rail = document.querySelector('input[type=range]');
        rail.value = ${value};
        rail.dispatchEvent(new Event('input', { bubbles: true }));
        return rail.value;
      })()`);
      await sleep(120);
      await frame(`Dragging the slider. ${rails} rails on the page.`);
    }
    await evaluate(`document.documentElement.setAttribute('data-theme','dark')`);
    await frames_over(500, 'The same components in dark.', 3);
  },
};

const chosen = only.length ? only : Object.keys(scenarios);
const manifest = [];

for (const name of chosen) {
  if (!scenarios[name]) throw new Error(`unknown scenario ${name}`);
  const start = frames.length;
  try {
    await scenarios[name]();
  } catch (error) {
    console.error(`scenario ${name} failed: ${error.message}`);
    await frame(`FAILED: ${error.message}`);
  }
  manifest.push({ name, from: start, to: frames.length });
  console.log(`${name}: ${frames.length - start} frames`);
}

writeFileSync(join(outDir, 'manifest.json'), JSON.stringify({
  width: WIDTH, height: HEIGHT, frames, scenarios: manifest,
}, null, 1));

console.log(`\n${frames.length} frames in ${outDir}`);
socket.close();
