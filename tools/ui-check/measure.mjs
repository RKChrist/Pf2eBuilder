// Measures the running client's real layout through the Chrome DevTools Protocol.
// Node 24 ships a WebSocket client, so this needs no dependency and no browser driver.
//
//   node tools/ui-check/measure.mjs [url] [width] [height]
//
// Set INJECT_CSS to a stylesheet string to reinstate a rule before measuring. That is how a
// layout fix is shown to matter: measure once as shipped, once with the old rule put back, and
// compare. Exit code is the number of problems found, so this works as a check.
//
// Set ACT to page script to reach a state first, such as a category's records:
//   ACT="document.querySelector('.pf-bottomnav__item:nth-child(2)').click(); await wait(500);
//        document.querySelector('.categories .category').click(); await wait(1500);"

const [url = 'http://localhost:5173/', width = '390', height = '844'] = process.argv.slice(2);
const port = process.env.CDP_PORT ?? '9222';

// A tab of its own, closed afterwards, so two runs against one Chrome never drive each other's page.
const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' })).json();
if (!target.webSocketDebuggerUrl) throw new Error('could not open a tab; start chrome with --remote-debugging-port');

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
if (process.env.SCHEME) {
  await send('Emulation.setEmulatedMedia', {
    features: [{ name: 'prefers-color-scheme', value: process.env.SCHEME }],
  });
}
await send('Page.navigate', { url });
for (let tries = 0; tries < 100; tries++) {
  const now = await send('Runtime.evaluate', { expression: 'location.href + " " + document.readyState', returnByValue: true });
  const [href, state] = (now.result?.result?.value ?? '').split(' ');
  if (href !== 'about:blank' && state !== 'loading') break;
  await new Promise(resolve => setTimeout(resolve, 100));
}

// Blazor boots its runtime before anything renders, so wait for real nodes rather than for load.
const booted = await evaluate(`new Promise(done => {
  const ready = () => document.querySelectorAll('nav button, nav a, [role=navigation] button, [role=navigation] a').length > 0;
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

// ACT is page script run before measuring, for a state no URL reaches: a category's records,
// or the search dropdown open. It may await; `wait(ms)` is in scope.
if (process.env.ACT) {
  await evaluate(`(async () => {
    const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
    ${process.env.ACT}
    return true;
  })()`);
}

// Data arrives after the navigation renders, and a count or a list that lands later can change
// the layout being measured.
await new Promise(resolve => setTimeout(resolve, Number(process.env.SETTLE_MS ?? 1000)));

const report = await evaluate(`(() => {
  // The app's own bar is the nav with the most items: a pager or a scope strip has two or
  // three. Chosen by shape rather than by class, because a renamed class once made this
  // tool announce that the client never rendered its navigation.
  const bar = [...document.querySelectorAll('nav, [role=navigation]')]
    .map(n => ({ n, count: n.querySelectorAll('button, a').length }))
    .sort((a, b) => b.count - a.count)[0]?.n;
  if (!bar) throw new Error('no navigation landmark on the page');
  const viewport = document.documentElement.clientWidth;
  const items = [...bar.querySelectorAll('button, a')].map(item => {
    const box = item.getBoundingClientRect();
    return {
      label: ((item.textContent ?? '').trim().split(/\\s+/).pop()) || '?',
      left: Math.round(box.left),
      right: Math.round(box.right),
      width: Math.round(box.width),
      onScreen: box.left >= -0.5 && box.right <= viewport + 0.5,
    };
  });
  const targets = [...document.querySelectorAll('button, a, input, select')]
    .map(el => ({ el, box: el.getBoundingClientRect() }))
    .filter(({ box }) => box.width > 0 && box.height > 0);
  const smallest = targets.reduce((worst, target) =>
    Math.min(target.box.width, target.box.height) < Math.min(worst.box.width, worst.box.height) ? target : worst);

  // Content clipped by an ancestor's overflow never widens scrollWidth, so it has to be found
  // element by element. Only elements that STRADDLE an edge count: one parked entirely
  // off-canvas is a panel waiting to slide in, which is deliberate, and flagging it made this
  // tool report a bug about a working docked sheet.
  const clipped = [...document.querySelectorAll('body *')]
    .filter(el => {
      const box = el.getBoundingClientRect();
      if (box.width === 0 || box.height === 0) return false;
      const straddlesRight = box.left < viewport - 1 && box.right > viewport + 1;
      const straddlesLeft = box.right > 1 && box.left < -1;
      if (!straddlesRight && !straddlesLeft) return false;
      // A strip that scrolls sideways on purpose, such as the trait filter or the search
      // scopes, holds content past the edge by design.
      for (let parent = el.parentElement; parent; parent = parent.parentElement) {
        if (['auto', 'scroll'].includes(getComputedStyle(parent).overflowX)) return false;
      }
      return true;
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
    smallestTarget: Math.round(Math.min(smallest.box.width, smallest.box.height)),
    smallestCulprit: [smallest.el.tagName.toLowerCase(), ...smallest.el.classList].join('.')
      + ' "' + (smallest.el.textContent ?? '').trim().slice(0, 30) + '"',
  };
})()`);

const offscreen = report.items.filter(item => !item.onScreen).map(item => item.label);
const scrollsSideways = report.scrollWidth > report.viewport;
const tooSmall = report.smallestTarget < 44;

console.log(`viewport ${report.viewport}  bar ${report.barWidth}  nav items ${report.items.length}`);
console.log(report.items.map(i => `${i.label} ${i.left}..${i.right}`).join('  |  '));
console.log(`offscreen nav items: ${offscreen.length ? offscreen.join(', ') : 'none'}`);
console.log(`horizontal scroll: ${scrollsSideways}`);
console.log(`smallest tap target: ${report.smallestTarget}px${tooSmall ? `  BELOW THE 44px FLOOR on ${report.smallestCulprit}` : ''}`);
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
await fetch(`http://127.0.0.1:${port}/json/close/${target.id}`);
// Not process.exit: exiting while the socket is still closing trips a libuv assertion on Windows.
process.exitCode = offscreen.length + report.clipped.length + (scrollsSideways ? 1 : 0) + (tooSmall ? 1 : 0);
