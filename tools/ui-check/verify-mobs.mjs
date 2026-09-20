// A DM fills a fight with monsters and a player is shown numbered mobs and nothing else.
//
//   node tools/ui-check/verify-mobs.mjs
//
// Exit code is the number of failures. SHOTS=<dir> also photographs both sides of the screen.
import { launch, openPage, sleep } from './cdp.mjs';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const base = process.env.BASE ?? 'http://localhost:5173';
const shots = process.env.SHOTS;
if (shots) mkdirSync(shots, { recursive: true });

const b = await launch({ headless: true });

let failures = 0;
const assert = (ok, label, detail = '') => {
  if (!ok) failures++;
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${label}${detail ? `  (${detail})` : ''}`);
};

const drive = (p) => ({
  wait: async (expression, ms = 25000) => {
    const end = Date.now() + ms;
    while (Date.now() < end) { if (await p.eval(`!!(${expression})`)) return true; await sleep(200); }
    return false;
  },
  click: async (sel, text) => {
    const hit = await p.eval(`(() => {
      const el = [...document.querySelectorAll(${JSON.stringify(sel)})]
        .find(e => ${text === undefined ? 'true' : `e.textContent.trim() === ${JSON.stringify(text)}`});
      if (!el || el.disabled) return false;
      el.click();
      return true;
    })()`);
    await sleep(1000);
    return hit;
  },
  type: (sel, value) => p.eval(`(() => {
    const f = document.querySelector(${JSON.stringify(sel)});
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(f, ${JSON.stringify(value)});
    for (const t of ['input', 'change']) f.dispatchEvent(new Event(t, { bubbles: true }));
  })()`),
  shot: async (name) => {
    if (!shots) return;
    await sleep(500);
    const { data } = await p.send('Page.captureScreenshot', { format: 'png' });
    writeFileSync(join(shots, `${name}.png`), Buffer.from(data, 'base64'));
  },
});

const rows = (p) => p.eval(`[...document.querySelectorAll('.turn')].map(t => ({
  name: t.querySelector('.turn__called')?.textContent.trim() ?? '',
  alias: t.querySelector('.turn__alias')?.textContent.trim() ?? '',
  text: t.innerText.replace(/\\s+/g, ' ').trim(),
}))`);

const dmPage = await openPage(b);
await dmPage.viewport(1440, 1000, false);
const dm = drive(dmPage);

await dmPage.goto(`${base}/campaign`);
await dm.wait(`document.querySelector('.join')`);
await dm.click('button', 'Start a new campaign');
await dm.wait(`document.querySelector('.campaign-code')`);
const code = await dmPage.eval(`document.querySelector('.campaign-code').textContent.trim()`);

await dm.click('.shell__mode-link[data-mode="Encounter"]');
await dm.wait(`document.querySelector('.fight')`);
await dm.click('.fight__acts button', 'Add');
await dm.wait(`document.querySelector('.adding .pf-search__input')`);

const monstersFirst = await dmPage.eval(
  `document.querySelector('.adding .pf-section__label')?.textContent.trim()`);
assert(monstersFirst === 'A monster', 'the add panel opens on monsters', monstersFirst);

await dm.click('.adding__count .pf-stepper__btn--plus');
await dm.click('.adding__count .pf-stepper__btn--plus');
await dm.type('.adding .pf-search__input', 'goblin warrior');
await dm.wait(`document.querySelector('.adding .pf-card .pf-row')`);
await dm.shot('1-add-panel');
await dm.click('.adding .pf-card .pf-row');
await dm.wait(`document.querySelectorAll('.turn').length === 3`);
let fight = await rows(dmPage);
assert(fight.length === 3, 'three of one monster arrive on one tap', fight.map(r => r.name).join(', '));
assert(new Set(fight.map(r => r.name)).size === 3, 'each with a name of its own');
assert(fight.map(r => r.alias).sort().join('|') === 'Mob 1 to players|Mob 2 to players|Mob 3 to players',
  'and the DM is told what the players call each', fight.map(r => r.alias).join(', '));

await dm.click('.adding__count .pf-stepper__btn--minus');
await dm.click('.adding__count .pf-stepper__btn--minus');
await dm.click('.adding__own .pf-disclose__summary, .adding__own summary, .adding__own button');
await dm.wait(`document.querySelector('.adding .monster')`);
await dm.type('.adding .monster input', 'Clockwork Heron');
await dm.shot('2-make-your-own');
await dm.click('.adding .monster__go');
await dm.wait(`document.querySelectorAll('.turn').length === 4`);
fight = await rows(dmPage);
assert(fight.some(r => r.name === 'Clockwork Heron'), 'a monster from no book joins the fight');
await dm.click('.pf-sheet:not([hidden]) .pf-sheet__close');

await dmPage.eval(`[...document.querySelectorAll('.turn')]
  .find(t => t.querySelector('.turn__called').textContent.includes('Heron'))
  .querySelector('.turn__edit').click()`);
await dm.wait(`document.querySelector('.pf-sheet:not([hidden]) .monster')`);
await dm.click('.pf-sheet:not([hidden]) .monster button', 'Make it elite');
await dm.shot('3-edit');
await dm.click('.pf-sheet:not([hidden]) .monster__go');
await dm.wait(`[...document.querySelectorAll('.turn__called')].some(e => e.textContent.includes('Elite Clockwork Heron'))`);
fight = await rows(dmPage);
const heron = fight.find(r => r.name.includes('Heron'));
assert(heron?.name === 'Elite Clockwork Heron' && /30\s*\/\s*30/.test(heron.text) && /AC\s*17/.test(heron.text),
  'an elite adjustment lands on the row', heron?.text.slice(0, 90));
await dm.shot('4-dm');

const playerPage = await openPage(b, { isolated: true });
await playerPage.viewport(1440, 1000, false);
const player = drive(playerPage);
await playerPage.goto(`${base}/campaign`);
await player.wait(`document.querySelector('.join input')`);
await player.type('.join input', code);
await player.click('button', 'Join');
await player.wait(`document.querySelector('.shell__mode-link')`);
await player.click('.shell__mode-link[data-mode="Encounter"]');
await player.wait(`document.querySelectorAll('.turn').length === 4`);
const seen = await rows(playerPage);
const page = await playerPage.eval(`document.querySelector('main').innerText`);
assert(seen.map(r => r.name).sort().join('|') === 'Mob 1|Mob 2|Mob 3|Mob 4',
  'a player sees four numbered mobs', seen.map(r => r.name).join(', '));
assert(!/goblin|heron|elite/i.test(page), 'and no name of any of them anywhere on the page');
assert(seen.every(r => !/AC|Fort|\d+\s*\/\s*\d+/.test(r.text)), 'and no number from any of their lines',
  seen[0]?.text);
assert(await playerPage.eval(`!document.querySelector('.turn__edit, .fight__acts button')`),
  'and no control that would change the fight');
await player.shot('5-player');

await dmPage.eval(`document.querySelector('.turn .pf-switch__input').click()`);
await sleep(2500);
const after = await rows(playerPage);
assert(after.some(r => /goblin/i.test(r.name)) && after.filter(r => /^Mob \d$/.test(r.name)).length === 3,
  'revealing one names that one and leaves the rest as mobs', after.map(r => r.name).join(', '));

console.log(`\n${failures} failure(s)`);
await b.close();
process.exit(failures);
