// A camping session, the way the rules run one, from both sides of the screen.
//
//   node tools/ui-check/verify-camp.mjs
//
// Exit code is the number of failures. SHOTS=<dir> also photographs each step.
import { launch, openPage, sleep } from './cdp.mjs';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const base = process.env.BASE ?? 'http://localhost:5173';
const shots = process.env.SHOTS;
if (shots) mkdirSync(shots, { recursive: true });
const fixture = JSON.parse(readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8'));

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
    const proto = f.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(proto, 'value').set.call(f, ${JSON.stringify(value)});
    for (const t of ['input', 'change']) f.dispatchEvent(new Event(t, { bubbles: true }));
  })()`),
  text: (sel) => p.eval(`document.querySelector(${JSON.stringify(sel)})?.textContent.replace(/\\s+/g, ' ').trim() ?? ''`),
  shot: async (name) => {
    if (!shots) return;
    await sleep(500);
    const { data } = await p.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true });
    writeFileSync(join(shots, `${name}.png`), Buffer.from(data, 'base64'));
  },
});

const named = (name) => { fixture.build.name = name; return JSON.stringify(fixture); };

const dmPage = await openPage(b);
await dmPage.viewport(1440, 1000, false);
const dm = drive(dmPage);

await dmPage.goto(`${base}/campaign`);
await dm.wait(`document.querySelector('.join')`);
await dm.click('button', 'Start a new campaign');
await dm.wait(`document.querySelector('.pf-textarea')`);
const code = await dm.text('.campaign-code');
await dm.type('.pf-textarea', named('Gnibbo'));
await dm.click('button', 'Import');
await dm.wait(`document.querySelector('.character__name')`);

// A player, in a browser of their own, with the code and nothing else.
const playerPage = await openPage(b, { isolated: true });
await playerPage.viewport(1440, 1000, false);
const player = drive(playerPage);
await playerPage.goto(`${base}/campaign`);
await player.wait(`document.querySelector('.join input')`);
await player.type('.join input', code);
await player.click('button', 'Join');
await player.wait(`document.querySelector('.character__name')`);
assert(await playerPage.eval(`!document.querySelector('.shell__dm')`), 'the second browser is a player, not the DM');
await player.click('.import__more summary');
await player.wait(`document.querySelector('.import__more .pf-textarea')`);
await player.type('.import__more .pf-textarea', named('Einar'));
await player.click('.import__more button', 'Import');
const imported = await player.wait(`[...document.querySelectorAll('.character__name')].some(e => e.textContent.trim() === 'Einar')`, 15000);
assert(imported, 'a player imports their own character', await player.text('.import__trouble'));
assert(await dm.wait(`[...document.querySelectorAll('.character__name')].some(e => e.textContent.trim() === 'Einar')`, 10000),
  'and it arrives on the DM’s screen without a reload');

await dm.click('.shell__mode-link[data-page="Camp"]');
await dm.wait(`document.querySelector('.steps')`);
assert((await dmPage.eval(`document.querySelectorAll('.steps__step').length`)) === 5, 'camp is a tab, and a camping session is five steps');
assert(/Prepare a campsite/.test(await dm.text('.steps__step--here')), 'which starts at preparing a campsite', await dm.text('.steps__step--here .steps__name'));
await dm.shot('1-prepare');

await dmPage.eval(`(() => { const s = document.querySelector('.zone__pick select'); s.value = [...s.options].find(o => o.textContent.startsWith('Greenbelt')).value; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
await dm.wait(`document.querySelector('.dc__value')?.textContent.trim() === '16'`, 8000);
assert((await dm.text('.dc__value')) === '16', 'picking a zone sets the Zone DC the rules give it', await dm.text('.dc__value'));
await dm.click('.outcome[data-outcome="CriticalSuccess"]');
await dm.wait(`document.querySelector('.dc--moved')`, 8000);
assert((await dm.text('.dc--moved .dc__value')) === '16', 'a perfect campsite makes tonight’s Encounter DC two higher', await dm.text('.dc--moved .dc__value'));

await dm.click('.steps__face[data-step="CampingActivities"]');
assert(/Step 2/.test(await dm.text('.step__name')), 'anybody can look ahead at a step');
await dm.click('.step__move');
await player.click('.shell__mode-link[data-page="Camp"]');
assert(await player.wait(`document.querySelector('.steps__step--here .steps__name')?.textContent.trim() === 'Camping activities'`, 10000),
  'and when the DM moves the table on, the player’s screen follows');

// The player takes an activity for their own character.
await playerPage.eval(`[...document.querySelectorAll('.actor')].find(a => a.textContent.includes('Einar')).click()`);
await sleep(500);
await playerPage.eval(`document.querySelector('.act[data-act="Relax"] .roll[data-roll="Success"]').click()`);
assert(await player.wait(`document.querySelector('.act[data-act="Relax"].act--closed')`, 10000),
  'a player takes a Camping activity, and one somebody succeeded at is done for tonight');
assert(await dm.wait(`document.querySelector('.act[data-act="Relax"].act--closed')`, 10000), 'on every screen');
assert(/2 hours/.test(await dm.text('.clock__value')), 'and it put two hours on the clock', await dm.text('.clock__value'));
assert((await playerPage.eval(`[...document.querySelectorAll('.actor')].find(a => a.textContent.includes('Einar')).querySelectorAll('.actor__pip--used').length`)) === 1,
  'and used one of that character’s four');
await dm.shot('2-activities');

// A recipe of the table's own, written by the player, eaten by the DM's character.
await player.click('.book__add summary');
await player.wait(`document.querySelector('.writing input')`);
await player.type('.writing input', 'Hearty Stew');
await player.type('.writing textarea', 'Success: +1 status bonus to Fortitude saves until the next camp.');
await player.click('.writing button', 'Write it in');
assert(await dm.wait(`[...document.querySelectorAll('.entry__name')].some(e => e.textContent.trim() === 'Hearty Stew')`, 10000),
  'a player writes a recipe into the camp book and the table sees it');

await dm.click('.steps__face[data-step="Eating"]');
await dm.click('.step__move');
await dm.wait(`document.querySelector('.plate')`);
await dmPage.eval(`[...document.querySelector('.plate').querySelectorAll('.pick')].find(b => b.textContent.trim() === 'Hearty Stew').click()`);
assert(await dm.wait(`document.querySelector('.plate .pick--on')?.textContent.trim() === 'Hearty Stew'`, 8000),
  'and a character can choose it as their meal');
await dm.shot('3-eating');

await dm.click('.steps__face[data-step="Resting"]');
await dm.click('.step__move');
await dm.wait(`document.querySelector('.watches')`);
assert((await dmPage.eval(`document.querySelectorAll('.watches__watch').length`)) === 2, 'two people keep two watches');
assert(/16 hours/.test(await dm.text('.watches__scale span:last-child')), 'which takes the sixteen hours the rules’ table says', await dm.text('.watches__scale'));
assert((await dmPage.eval(`document.querySelectorAll('.watches__check').length`)) === 3, 'with a check for an encounter every four hours');
await dm.shot('4-resting');

await dm.click('.step__act');
assert(await dm.wait(`document.querySelector('.steps__step--here .steps__name')?.textContent.trim() === 'Daily preparations'`, 10000),
  'resting for the night brings the table to daily preparations');
await dm.click('.step__act');
assert(await dm.wait(`document.querySelector('.steps__step--here .steps__name')?.textContent.trim() === 'Prepare a campsite'`, 10000),
  'and breaking camp starts the next one from the top');
assert(await dmPage.eval(`[...document.querySelectorAll('.entry__name')].some(e => e.textContent.trim() === 'Hearty Stew')`),
  'with the recipe still in the book');
await dm.shot('5-next-camp');

await playerPage.viewport(390, 844, true);
await sleep(800);
assert(await playerPage.eval(`document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1`), 'on a phone the five steps run down the page and nothing scrolls sideways');
await player.shot('6-phone');

console.log(`console errors: ${dmPage.consoleErrors().length + playerPage.consoleErrors().length}`);
console.log(`\n${failures} failure(s)`);
await b.close();
process.exit(failures);
