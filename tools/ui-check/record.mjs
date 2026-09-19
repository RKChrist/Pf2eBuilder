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
const WIDTH = 390;
const HEIGHT = 844;

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
  width: WIDTH, height: HEIGHT, deviceScaleFactor: 1, mobile: true,
});
await send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 5 });

mkdirSync(outDir, { recursive: true });

let frameNumber = 0;
const frames = [];

async function frame(caption) {
  const { result } = await send('Page.captureScreenshot', { format: 'jpeg', quality: 78 });
  const name = `f${String(frameNumber++).padStart(4, '0')}.jpg`;
  writeFileSync(join(outDir, name), Buffer.from(result.data, 'base64'));
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

const click = selector => evaluate(`(() => {
  const el = document.querySelector(${JSON.stringify(selector)});
  if (!el) throw new Error('no element for ' + ${JSON.stringify(selector)});
  el.scrollIntoView({ block: 'center' });
  el.click();
  return true;
})()`);

const clickText = (selector, text) => evaluate(`(() => {
  const el = [...document.querySelectorAll(${JSON.stringify(selector)})]
    .find(e => e.textContent.trim().toLowerCase().includes(${JSON.stringify(text.toLowerCase())}));
  if (!el) throw new Error('no ' + ${JSON.stringify(selector)} + ' containing ' + ${JSON.stringify(text)});
  el.scrollIntoView({ block: 'center' });
  el.click();
  return true;
})()`);

async function typeInto(selector, value) {
  // One character at a time through real key events, so the debounce is exercised rather than
  // bypassed by setting .value directly.
  await evaluate(`document.querySelector(${JSON.stringify(selector)}).focus()`);
  for (const character of value) {
    await send('Input.dispatchKeyEvent', { type: 'keyDown', text: character });
    await send('Input.dispatchKeyEvent', { type: 'keyUp' });
    await sleep(60);
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
    await goto(`${client}/`, 'nav.bar button');
    await frame('Six groups in the thumb bar. Build opens the character-creation categories.');
    await clickText('nav.bar button', 'feats');
    await frames_over(500, 'Feats group. Two categories.');
    await clickText('li button, .categories button', 'feats');
    await waitFor('.results, [class*=row], article');
    await frames_over(900, 'All 6,390 feats, paged.', 5);
    await typeInto('input[type=search], .pf-search__input', 'power');
    await frames_over(1200, 'Typing filters as you go. The last keystroke wins.', 6);
  },

  async detail() {
    await goto(`${client}/`, 'nav.bar button');
    await clickText('nav.bar button', 'gear');
    await sleep(400);
    await clickText('li button, .categories button', 'weapons');
    await waitFor('.results, [class*=row], article');
    await frames_over(800, 'Weapons.', 4);
    await evaluate(`(() => {
      const row = document.querySelector('.results button, [class*=row] button, li button');
      row.scrollIntoView({ block: 'center' }); row.click(); return true;
    })()`);
    await frames_over(900, 'The record opens as a bottom sheet with its mechanics as labelled pairs.', 6);
  },

  async conditions() {
    await goto(`${client}/conditions`, '.pf-card, article, section');
    await frames_over(700, 'The fourteen conditions the rules engine computes.', 4);
    await evaluate('window.scrollTo({ top: 420, behavior: "instant" })');
    await frames_over(500, 'Clumsy is Dexterity-based, not a list of five statistics.', 3);
    await evaluate('window.scrollTo({ top: 900, behavior: "instant" })');
    await frames_over(500, 'Stupefied reaches Intelligence, Wisdom and Charisma.', 3);
  },

  async sliders() {
    if (!gallery) return;
    await goto(gallery, '.pf-slider');
    await evaluate(`document.querySelector('.pf-slider').scrollIntoView({ block: 'center' })`);
    await frame('Slider and RangeSlider, from the component kit.');
    const rails = await evaluate(`[...document.querySelectorAll('.pf-slider input[type=range]')].length`);
    for (let value = 3; value <= 18; value += 3) {
      await evaluate(`(() => {
        const rail = document.querySelector('.pf-slider input[type=range]');
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
