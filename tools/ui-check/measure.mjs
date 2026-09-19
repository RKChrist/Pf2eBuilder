// Measures the running client's real layout through the Chrome DevTools Protocol.
// Node 24 ships a WebSocket client, so this needs no dependency and no browser driver.
//
//   node tools/ui-check/measure.mjs [url] [width] [height]
//
// Set INJECT_CSS to a stylesheet string to reinstate a rule before measuring. That is how a
// layout fix is shown to matter: measure once as shipped, once with the old rule put back, and
// compare. Exit code is the number of problems found, so this works as a check.

const [url = 'http://localhost:5173/', width = '390', height = '844'] = process.argv.slice(2);
const port = process.env.CDP_PORT ?? '9222';

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
  const response = await send('Runtime.evaluate', {
    expression, returnByValue: true, awaitPromise: true,
  });
  if (response.error) throw new Error(`CDP ${response.error.message}`);
  const { result, exceptionDetails } = response.result;
  if (exceptionDetails) {
    throw new Error(exceptionDetails.exception?.description ?? exceptionDetails.text);
  }
  return result.value;
}

await send('Runtime.enable');
await send('Page.enable');
await send('Emulation.setDeviceMetricsOverride', {
  width: Number(width), height: Number(height), deviceScaleFactor: 1, mobile: true,
});
await send('Page.navigate', { url });

// Blazor boots its runtime before anything renders, so wait for real nodes rather than for load.
const booted = await evaluate(`new Promise(done => {
  const ready = () => document.querySelectorAll('nav.bar button').length > 0;
  if (ready()) return done(true);
  const observer = new MutationObserver(() => { if (ready()) { observer.disconnect(); done(true); } });
  observer.observe(document.documentElement, { childList: true, subtree: true });
  setTimeout(() => done(false), 25000);
})`);
if (!booted) throw new Error('the client never rendered its navigation');

if (process.env.INJECT_CSS) {
  await evaluate(`(() => {
    const style = document.createElement('style');
    style.textContent = ${JSON.stringify(process.env.INJECT_CSS)};
    document.head.append(style);
    return true;
  })()`);
}

const report = await evaluate(`(() => {
  const bar = document.querySelector('nav.bar');
  const viewport = document.documentElement.clientWidth;
  const items = [...bar.querySelectorAll('button')].map(item => {
    const box = item.getBoundingClientRect();
    return {
      label: item.querySelector('.label')?.textContent?.trim(),
      left: Math.round(box.left),
      right: Math.round(box.right),
      width: Math.round(box.width),
      onScreen: box.left >= -0.5 && box.right <= viewport + 0.5,
    };
  });
  const targets = [...document.querySelectorAll('button, a, input, select')]
    .map(el => el.getBoundingClientRect())
    .filter(box => box.width > 0 && box.height > 0);

  // Content clipped by an ancestor's overflow never widens scrollWidth, so it has to be found
  // element by element. Fixed bars legitimately span the viewport, hence the tolerance.
  const clipped = [...document.querySelectorAll('body *')]
    .filter(el => {
      const box = el.getBoundingClientRect();
      if (box.width === 0 || box.height === 0) return false;
      return box.right > viewport + 1 || box.left < -1;
    })
    .slice(0, 12)
    .map(el => ({
      tag: el.tagName.toLowerCase(),
      cls: (typeof el.className === 'string' ? el.className : '').trim().split(/\\s+/)[0] || '',
      right: Math.round(el.getBoundingClientRect().right),
      text: (el.textContent ?? '').trim().slice(0, 40),
    }));

  return {
    viewport,
    barWidth: Math.round(bar.getBoundingClientRect().width),
    scrollWidth: document.documentElement.scrollWidth,
    items,
    clipped,
    smallestTarget: Math.round(Math.min(...targets.map(b => Math.min(b.width, b.height)))),
  };
})()`);

const offscreen = report.items.filter(item => !item.onScreen).map(item => item.label);
const scrollsSideways = report.scrollWidth > report.viewport;
const tooSmall = report.smallestTarget < 44;

console.log(`viewport ${report.viewport}  bar ${report.barWidth}  nav items ${report.items.length}`);
console.log(report.items.map(i => `${i.label} ${i.left}..${i.right}`).join('  |  '));
console.log(`offscreen nav items: ${offscreen.length ? offscreen.join(', ') : 'none'}`);
console.log(`horizontal scroll: ${scrollsSideways}`);
console.log(`smallest tap target: ${report.smallestTarget}px${tooSmall ? '  BELOW THE 44px FLOOR' : ''}`);
console.log(`elements past the right edge: ${report.clipped.length}`);
for (const el of report.clipped) {
  console.log(`  ${el.tag}.${el.cls} right=${el.right}  "${el.text}"`);
}

// Chrome's --screenshot flag renders at its own viewport and crops to --window-size, which made
// this app look clipped at 390px when nothing was. Capturing through the same emulation override
// the measurements use is the only way the image and the numbers agree.
if (process.env.SHOT) {
  const { result } = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  const { writeFileSync } = await import('node:fs');
  writeFileSync(process.env.SHOT, Buffer.from(result.data, 'base64'));
  console.log(`screenshot: ${process.env.SHOT}`);
}

socket.close();
process.exit(offscreen.length + report.clipped.length + (scrollsSideways ? 1 : 0) + (tooSmall ? 1 : 0));
