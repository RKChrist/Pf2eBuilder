// Screenshots every screen of the running client, with a seeded campaign, into one folder.
//
//   node tools/ui-check/tour.mjs <outDir> [width] [height]
//
// THEME=dark tours the dark theme; light is the app's default. Run it before and after a visual
// change and the two folders are the comparison.
import { launch, openPage, sleep } from './cdp.mjs';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [outDir, width = '1440', height = '900'] = process.argv.slice(2);
if (!outDir) throw new Error('usage: tour.mjs <outDir> [width] [height]');
mkdirSync(outDir, { recursive: true });

const base = process.env.BASE ?? 'http://localhost:5173';
const mobile = Number(width) < 600;
const fixture = JSON.parse(readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8'));

const b = await launch({ headless: true });
const p = await openPage(b);
await p.viewport(Number(width), Number(height), mobile);

const shot = async (name) => {
  await sleep(700);
  const { data } = await p.send('Page.captureScreenshot', { format: 'png' });
  writeFileSync(join(outDir, `${name}.png`), Buffer.from(data, 'base64'));
  console.log('shot', name);
};

const wait = async (sel, ms = 25000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    if (await p.eval(`!!document.querySelector(${JSON.stringify(sel)})`)) return true;
    await sleep(200);
  }
  console.log('never saw', sel);
  return false;
};

const click = async (sel, text) => {
  const hit = await p.eval(`(() => {
    const el = [...document.querySelectorAll(${JSON.stringify(sel)})]
      .find(e => ${text === undefined ? 'true' : `e.textContent.trim() === ${JSON.stringify(text)}`});
    if (!el || el.disabled) return false;
    el.click();
    return true;
  })()`);
  await sleep(1200);
  return hit;
};

const type = (sel, value) => p.eval(`(() => {
  const f = document.querySelector(${JSON.stringify(sel)});
  const proto = f.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(proto, 'value').set.call(f, ${JSON.stringify(value)});
  for (const t of ['input', 'change']) f.dispatchEvent(new Event(t, { bubbles: true }));
})()`);

const closeSheet = () => click('.pf-sheet.open .pf-sheet__close, .pf-sheet__close');

await p.goto(`${base}/`);
if (process.env.THEME) {
  await p.eval(`localStorage.setItem('theme', ${JSON.stringify(process.env.THEME)})`);
  await p.goto(`${base}/`);
}
await wait('nav a, nav button');
await shot('01-board');

await p.goto(`${base}/browse/spell`);
await wait('.rule .open');
await shot('02-list');
await click('.rule .open');
await shot('03-list-detail');
await click('.pf-sheet .trait-link');
await shot('04-trait');

await p.goto(`${base}/conditions`);
await wait('main h1');
await sleep(1200);
await shot('05-conditions');

await p.goto(`${base}/search?q=fire`);
await wait('main h1');
await sleep(1500);
await shot('06-search');

await p.goto(`${base}/account`);
await wait('main h1');
await shot('07-account');

await p.goto(`${base}/campaign`);
await wait('.join');
await shot('08-join');
await click('button', 'Start a new campaign');
await wait('.campaign-code');
for (const name of ['Gnibbo', 'Einar']) {
  await wait('.pf-textarea');
  fixture.build.name = name;
  await type('.pf-textarea', JSON.stringify(fixture));
  await click('button', 'Import');
  await sleep(1400);
}
await shot('09-party');

for (const [mode, name] of [['Encounter', '10-encounter'], ['Exploration', '12-exploration'], ['Downtime', '13-downtime']]) {
  await click(`.shell__mode-link[data-mode="${mode}"]`);
  await sleep(1500);
  if (mode === 'Encounter' && await wait('.fight', 8000)) {
    await click('.fight__acts button', 'Add');
    await click('.adding__all');
    await sleep(3000);
    await closeSheet();
    await shot(name);
    await click('.turn__called, .turn button');
    await shot('11-encounter-detail');
    await closeSheet();
    continue;
  }
  await shot(name);
}

await click('.shell__party[href="/campaign/camp"]');
await sleep(1500);
await shot('14-camp');

console.log('console errors:', p.consoleErrors().length);
await b.close();
