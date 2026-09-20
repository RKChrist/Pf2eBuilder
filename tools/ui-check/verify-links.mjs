// A rule's name opens its record wherever it is written, and every sheet is the right-hand
// panel on a desk and a bottom sheet on a phone.
//
//   node tools/ui-check/verify-links.mjs
//
// Exit code is the number of failures. This exists because names on the conditions page, in the
// effect picker and on the mode boards were plain text, and four of six sheets opened across the
// whole of a laptop screen while the page reserved room at the right for a panel that never came.
import { launch, openPage, sleep } from './cdp.mjs';
import { readFileSync } from 'node:fs';

const base = process.env.BASE ?? 'http://localhost:5173';
const fixture = readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8');

const b = await launch({ headless: true });
const p = await openPage(b);
await p.viewport(1440, 900, false);

let failures = 0;
const assert = (ok, label, detail = '') => {
  if (!ok) failures++;
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${label}${detail ? `  (${detail})` : ''}`);
};

const wait = async (expression, ms = 20000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    if (await p.eval(`!!(${expression})`)) return true;
    await sleep(150);
  }
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
  await sleep(900);
  return hit;
};

const type = (sel, value) => p.eval(`(() => {
  const f = document.querySelector(${JSON.stringify(sel)});
  const proto = f.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(proto, 'value').set.call(f, ${JSON.stringify(value)});
  for (const t of ['input', 'change']) f.dispatchEvent(new Event(t, { bubbles: true }));
})()`);

const openSheets = () => p.eval(`[...document.querySelectorAll('.pf-sheet:not([hidden])')].map(s => {
  const panel = s.querySelector('.pf-sheet__panel').getBoundingClientRect();
  const scrim = s.querySelector('.pf-sheet__scrim');
  return {
    title: s.querySelector('.pf-sheet__title')?.textContent.trim() ?? '',
    left: Math.round(panel.left), width: Math.round(panel.width), top: Math.round(panel.top),
    scrim: !!scrim && getComputedStyle(scrim).display !== 'none',
  };
})`);

const ruleSheetTitle = () =>
  p.eval(`document.querySelector('.rule-sheet:not([hidden]) .pf-sheet__title')?.textContent.trim() ?? ''`);

const closeRule = () => click('.rule-sheet:not([hidden]) .pf-sheet__close');

await p.goto(`${base}/conditions`);
await wait(`document.querySelector('.cards .rule-link')`);
const condition = await p.eval(`document.querySelector('.cards .rule-link').textContent.trim()`);
await click('.cards .rule-link');
await wait(`document.querySelector('.rule-sheet:not([hidden]) .pf-sheet__title')?.textContent.trim()`);
assert((await ruleSheetTitle()) === condition, 'a condition card title opens that condition', `${condition} -> ${await ruleSheetTitle()}`);
assert(new URL(await p.eval('location.href')).searchParams.has('rule'), 'and the address carries the rule, so Back closes it');
await closeRule();

await p.goto(`${base}/campaign`);
await wait(`document.querySelector('.join')`);
await click('button', 'Start a new campaign');
await wait(`document.querySelector('.campaign-code')`);
await wait(`document.querySelector('.pf-textarea')`);
await type('.pf-textarea', fixture);
await click('button', 'Import');
await wait(`document.querySelector('.character__name')`);

const lineage = await p.eval(`[...document.querySelectorAll('.character .rule-link')].slice(0, 2).map(e => e.textContent.trim())`);
assert(lineage.length === 2, 'ancestry and class are two links on the party card', lineage.join(' / '));
await click('.character .rule-link');
await wait(`document.querySelector('.rule-sheet:not([hidden]) .pf-sheet__title')?.textContent.trim()`);
assert((await ruleSheetTitle()) === lineage[0], 'the ancestry link opens the ancestry', await ruleSheetTitle());
await closeRule();

await click('.shell__mode-link[data-mode="Exploration"]');
await wait(`document.querySelector('.chip--add')`);
await click('.chip--add');
await wait(`document.querySelector('.pf-sheet:not([hidden]) .condition')`);
let sheets = await openSheets();
assert(sheets.length === 1 && sheets[0].left > 720 && !sheets[0].scrim,
  'the effect picker docks on the right with nothing dimmed', JSON.stringify(sheets[0]));
const pageRight = await p.eval(`Math.round(document.querySelector('.page').getBoundingClientRect().right)`);
assert(Math.abs(pageRight - sheets[0].left) <= 2, 'the page ends where the panel begins', `${pageRight} vs ${sheets[0].left}`);

const picked = await p.eval(`document.querySelector('.pf-sheet:not([hidden]) .condition .rule-link')?.textContent.trim() ?? ''`);
await click('.pf-sheet:not([hidden]) .condition .rule-link');
await wait(`document.querySelector('.rule-sheet:not([hidden]) .pf-sheet__title')?.textContent.trim()`);
assert((await ruleSheetTitle()) === picked, 'a condition name inside the picker opens that condition', `${picked} -> ${await ruleSheetTitle()}`);
const onTop = await p.eval(`(() => {
  const panel = document.querySelector('.rule-sheet:not([hidden]) .pf-sheet__panel').getBoundingClientRect();
  const hit = document.elementFromPoint(panel.left + panel.width / 2, panel.top + 30);
  return !!hit?.closest('.rule-sheet');
})()`);
assert(onTop, 'the rule panel sits above the picker');
await closeRule();
sheets = await openSheets();
assert(sheets.length === 1 && sheets[0].title !== picked, 'closing the rule leaves the picker open', JSON.stringify(sheets.map(s => s.title)));
await click('.pf-sheet:not([hidden]) .pf-sheet__close');

await wait(`document.querySelector('.try__name .rule-link, .try__name.rule-link, .try .rule-link')`);
const attempt = await p.eval(`document.querySelector('.try .rule-link').textContent.trim()`);
await click('.try .rule-link');
const opened = await wait(`document.querySelector('.rule-sheet:not([hidden]) .pf-sheet__title')?.textContent.trim()`, 8000);
const landed = new URL(await p.eval('location.href'));
assert(opened || landed.pathname === '/search', 'a skill action name opens its record, or a search for it',
  `${attempt} -> ${opened ? await ruleSheetTitle() : landed.pathname + landed.search}`);

await p.viewport(390, 844, true);
await p.goto(`${base}/conditions`);
await wait(`document.querySelector('.cards .rule-link')`);
await click('.cards .rule-link');
await wait(`document.querySelector('.rule-sheet:not([hidden]) .pf-sheet__title')?.textContent.trim()`);
sheets = await openSheets();
assert(sheets.length === 1 && sheets[0].left === 0 && sheets[0].width === 390 && sheets[0].scrim,
  'on a phone the same sheet rises from the bottom over a scrim', JSON.stringify(sheets[0]));

console.log(`console errors: ${p.consoleErrors().length}`);
console.log(`\n${failures} failure(s)`);
await b.close();
process.exit(failures);
