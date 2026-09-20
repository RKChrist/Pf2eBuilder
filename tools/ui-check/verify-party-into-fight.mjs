// The whole party into a fight in one tap, with four characters.
//
//   node tools/ui-check/verify-party-into-fight.mjs
//
// Exit code is 0 when everyone landed. This exists because the first version of that button
// dispatched one action per character with nothing awaited, so four commands raced each other
// on one campaign and three came back 500 with nothing on screen saying so. One character went
// in and the button then vanished, because it only shows when more than one is still absent.
//
// The server still loses one of two adds that arrive together into a campaign with no encounter
// row; verify-concurrency.mjs reproduces that. This check is the guarantee the client makes
// regardless: it never sends them together.
import { launch, openPage, sleep } from './cdp.mjs';
import { readFileSync } from 'node:fs';

const fixture = readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8');
const named = (name) => {
  const build = JSON.parse(fixture);
  build.build.name = name;
  return JSON.stringify(build);
};

const b = await launch({ headless: true });
const p = await openPage(b);
await p.viewport(1280, 1000, false);
await p.goto('http://localhost:5173/campaign');

const wait = async (sel, ms = 25000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    if (await p.eval(`!!document.querySelector(${JSON.stringify(sel)})`)) return true;
    await sleep(200);
  }
  throw new Error(`never saw ${sel}`);
};

const clickText = async (sel, text) => {
  await p.eval(`(() => {
    const el = [...document.querySelectorAll(${JSON.stringify(sel)})]
      .find(e => e.textContent.trim() === ${JSON.stringify(text)});
    if (el && !el.disabled) el.click();
  })()`);
  await sleep(900);
};

const type = (sel, value) => p.eval(`(() => {
  const f = document.querySelector(${JSON.stringify(sel)});
  const proto = f.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(proto, 'value').set.call(f, ${JSON.stringify(value)});
  for (const t of ['input', 'change']) f.dispatchEvent(new Event(t, { bubbles: true }));
})()`);

await wait('.join');
await clickText('button', 'Start a new campaign');
await wait('.campaign-code');

for (const name of ['Alpha', 'Bravo', 'Charlie', 'Delta']) {
  await wait('.pf-textarea');
  await type('.pf-textarea', named(name));
  await clickText('button', 'Import');
  await sleep(1400);
}

const roster = await p.eval(
  `[...document.querySelectorAll('.character__name')].map(e => e.textContent.trim())`);
console.log('roster:', JSON.stringify(roster));

// Through the mode strip, not a fresh load: a hard navigation drops the campaign, which is
// its own defect and not the one under test here.
await p.eval(`document.querySelector('.shell__mode-link[data-mode="Encounter"]')?.click()`);
await sleep(2000);
await wait('.fight');
await clickText('.fight__acts button', 'Add');
await wait('.adding .pf-section__label');

const allButton = await p.eval(
  `document.querySelector('.adding__all')?.textContent.replace(/\\s+/g, ' ').trim() ?? 'missing'`);
console.log('the one-tap button says:', allButton);

await p.eval(`document.querySelector('.adding__all')?.click()`);
await sleep(4000);
await p.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(1200);

const inFight = await p.eval(
  `[...document.querySelectorAll('.turn__called')].map(e => e.textContent.trim()).sort()`);
const trouble = await p.eval(
  `document.querySelector('.trouble, .import__trouble')?.textContent.trim() ?? 'none'`);

console.log('in the fight:', JSON.stringify(inFight));
console.log('error shown: ', trouble);
console.log('console errors:', p.consoleErrors().length);
console.log(inFight.length === roster.length
  ? `PASS  all ${roster.length} of the party went in on one tap`
  : `FAIL  ${inFight.length} of ${roster.length} landed`);

await b.close();
process.exit(inFight.length === roster.length ? 0 : 1);
