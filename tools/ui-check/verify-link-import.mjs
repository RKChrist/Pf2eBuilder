// A character arrives from a Wanderer's Guide link, with the numbers Wanderer's Guide shows.
//
//   node tools/ui-check/verify-link-import.mjs [link]
//
// This one talks to the real Wanderer's Guide, so it is run by hand and is not part of a suite.
// The default link is the owner's own public character.
import { launch, openPage, sleep } from './cdp.mjs';
import { writeFileSync } from 'node:fs';

const link = process.argv[2] ?? 'https://wanderersguide.app/stat-block/character/70618';
const base = process.env.BASE ?? 'http://localhost:5173';

const b = await launch({ headless: true });
const p = await openPage(b);
await p.viewport(1440, 900, false);

let failures = 0;
const assert = (ok, label, detail = '') => {
  if (!ok) failures++;
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${label}${detail ? `  (${detail})` : ''}`);
};
const wait = async (expression, ms = 25000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (await p.eval(`!!(${expression})`)) return true; await sleep(200); }
  return false;
};
const type = (sel, value) => p.eval(`(() => {
  const f = document.querySelector(${JSON.stringify(sel)});
  Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(f, ${JSON.stringify(value)});
  for (const t of ['input', 'change']) f.dispatchEvent(new Event(t, { bubbles: true }));
})()`);

await p.goto(`${base}/campaign`);
await wait(`document.querySelector('.join')`);
await p.eval(`[...document.querySelectorAll('button')].find(e => e.textContent.trim() === 'Start a new campaign').click()`);
await wait(`document.querySelector('.import__link input')`);
await type('.import__link input', link);
await sleep(400);
await p.eval(`document.querySelector('.import__go').click()`);
const landed = await wait(`document.querySelector('.character__name')`);
const trouble = await p.eval(`document.querySelector('.import__trouble')?.textContent.trim() ?? ''`);
assert(landed, 'the character is on the party page', trouble);

const stats = await p.eval(`Object.fromEntries([...document.querySelectorAll('.character .stat')].map(s =>
  [s.querySelector('.stat__label, dt, span')?.textContent.trim(), s.textContent.replace(/\s+/g, ' ').trim()]))`);
console.log(JSON.stringify(stats));
console.log('name:', await p.eval(`document.querySelector('.character__name')?.textContent.trim()`));
if (process.env.SHOT) {
  const { data } = await p.send('Page.captureScreenshot', { format: 'png' });
  writeFileSync(process.env.SHOT, Buffer.from(data, 'base64'));
}
console.log(`\n${failures} failure(s)`);
await b.close();
process.exit(failures);
