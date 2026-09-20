// The three pages that are not a fight: the party, the road and the days between.
//
//   node tools/ui-check/verify-modes.mjs
//
// Exit code is the number of failures. SHOTS=<dir> also photographs each page.
import { launch, openPage, sleep } from './cdp.mjs';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const base = process.env.BASE ?? 'http://localhost:5173';
const shots = process.env.SHOTS;
if (shots) mkdirSync(shots, { recursive: true });
const fixture = readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8');

const b = await launch({ headless: true });
const p = await openPage(b);
await p.viewport(1440, 1000, false);

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
const click = async (sel, text) => {
  const hit = await p.eval(`(() => {
    const el = [...document.querySelectorAll(${JSON.stringify(sel)})]
      .find(e => ${text === undefined ? 'true' : `e.textContent.trim() === ${JSON.stringify(text)}`});
    if (!el || el.disabled) return false;
    el.click();
    return true;
  })()`);
  await sleep(1000);
  return hit;
};
const type = (sel, value) => p.eval(`(() => {
  const f = document.querySelector(${JSON.stringify(sel)});
  const proto = f.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(proto, 'value').set.call(f, ${JSON.stringify(value)});
  for (const t of ['input', 'change']) f.dispatchEvent(new Event(t, { bubbles: true }));
})()`);
const shot = async (name) => {
  if (!shots) return;
  await sleep(500);
  const { data } = await p.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true });
  writeFileSync(join(shots, `${name}.png`), Buffer.from(data, 'base64'));
};
const text = (sel) => p.eval(`document.querySelector(${JSON.stringify(sel)})?.textContent.replace(/\\s+/g, ' ').trim() ?? ''`);

await p.goto(`${base}/campaign`);
await wait(`document.querySelector('.join')`);
await click('button', 'Start a new campaign');
await wait(`document.querySelector('.pf-textarea')`);
assert(await p.eval(`!document.querySelector('.import__more')`), 'with nobody in the party the import form is open, not folded away');
await type('.pf-textarea', fixture);
await click('button', 'Import');
await wait(`document.querySelector('.character__name')`);

assert(await p.eval(`document.querySelectorAll('.character .hits__count').length === 0 && !!document.querySelector('.character .hp__current')`),
  'a party card states hit points once');
await wait(`document.querySelector('.import__more') && !document.querySelector('.import__more').open`, 6000);
assert(await p.eval(`(() => { const d = document.querySelector('.import__more'); return !!d && !d.open; })()`),
  'and once there is a party the import form folds away');
assert((await text('.shell__party.on')) === 'Party', 'the header says you are on the party page', await text('.shell__party.on'));
await shot('1-party');

await click('.shell__mode-link[data-mode="Exploration"]');
await wait(`document.querySelector('.choose .pick[data-pick]')`);
const before = await text('.choose__says');
await p.eval(`document.querySelector('.choose .pick[data-pick="scout"]').dispatchEvent(new MouseEvent('mouseover', { bubbles: true }))`);
await p.eval(`document.querySelector('.choose .pick[data-pick="scout"]').focus()`);
await sleep(500);
const looking = await text('.choose__says');
assert(/^Scout\./.test(looking) && looking.length > 20, 'pointing at an activity says what it does before it is chosen', looking.slice(0, 80));
assert(before !== looking, 'in the line that was already there, so nothing jumps');
await shot('2-exploration');

assert((await text('.clock__value')) === 'no time at all', 'the road starts with no time gone', await text('.clock__value'));
await click('.passing__span[data-minutes="60"]');
await wait(`document.querySelector('.clock__value')?.textContent.trim() !== 'no time at all'`, 8000);
const hour = await text('.clock__value');
assert(/hour/i.test(hour), 'an hour passes when the DM says so', hour);
await click('.passing__span[data-minutes="10"]');
await sleep(800);
assert(/10 min/i.test(await text('.clock__value')), 'and ten minutes more on top of it', await text('.clock__value'));

await click('.shell__mode-link[data-page="Camp"]');
await wait(`document.querySelector('.camp')`);
assert((await text('.shell__mode-link--page.on')) === 'Camp', 'the strip says you are at camp', await text('.shell__mode-link--page.on'));
assert(/10 min/i.test(await text('.clock__value')), 'and camp reads the same clock the road moved', await text('.clock__value'));
await shot('3-camp');

await click('.shell__mode-link[data-mode="Downtime"]');
await wait(`document.querySelector('.downtime .pick[data-pick]')`);
await p.eval(`document.querySelector('.downtime .pick[data-pick]').focus()`);
await sleep(400);
assert((await text('.downtime .choose__says')).length > 20, "a day's work explains itself the same way", (await text('.downtime .choose__says')).slice(0, 70));

// The DC as a die. Gnibbo crafts with Crafting against a level 7 task, and the
// picture has to agree with the rule: the face to beat is the DC less the modifier.
await click('.downtime .pick[data-pick="craft"]');
await wait(`document.querySelector('.downtime .die')`, 10000);
const die = await p.eval(`(() => {
  const dc = Number(document.querySelector('.spending__dc-value').textContent);
  const modifier = Number(document.querySelector('.spending__skill').textContent.replace(/[^0-9+-]/g, ''));
  const faces = [...document.querySelectorAll('.downtime .die__face')];
  return { dc, modifier, count: faces.length,
           needs: Number(document.querySelector('.downtime .die__face--needs')?.textContent ?? 0),
           says: document.querySelector('.downtime .die__needs').textContent.trim() };
})()`);
assert(die.count === 20, 'a DC is drawn as the twenty faces of the die');
assert(die.needs === Math.min(20, Math.max(2, die.dc - die.modifier)) || die.needs === die.dc - die.modifier,
  'and the face to beat is the DC less the modifier', JSON.stringify(die));
await shot('4-downtime');

console.log(`console errors: ${p.consoleErrors().length}`);
console.log(`\n${failures} failure(s)`);
await b.close();
process.exit(failures);
