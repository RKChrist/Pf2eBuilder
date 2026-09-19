// Drives the campaign screen in a real Chrome, as the DM and then as a player.
//
//   dotnet run --project src/Pf2e.Api
//   dotnet run --project src/Pf2e.Client --launch-profile http
//   node tools/ui-check/verify-campaign.mjs
//
// The two roles are two tabs of one browser, which is what a table is: the DM created the
// campaign and holds the key, everyone else typed the code. Exit code is the number of failures.
import { launch, openPage, reporter, sleep } from './cdp.mjs';
import { readFileSync, writeFileSync } from 'node:fs';

const client = process.argv[2] ?? 'http://localhost:5173/';
const shots = process.env.SHOTS;
const pathbuilder = readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8');

const check = reporter();
const browser = await launch({ headless: process.env.HEADED !== '1' });

async function screen(width, height, mobile) {
  const page = await openPage(browser);
  await page.viewport(width, height, mobile);
  if (mobile) await page.coarse(true);
  return page;
}

const waitFor = async (page, selector, ms = 25000) => {
  const deadline = Date.now() + ms;
  while (Date.now() < deadline) {
    if (await page.eval(`!!document.querySelector(${JSON.stringify(selector)})`)) return true;
    await sleep(150);
  }
  throw new Error(`never saw ${selector}`);
};

// Blazor re-renders on every answer from the server, so a control can be absent for a frame
// after an unrelated change lands. Waiting for it beats sleeping longer and hoping.
const clickText = async (page, selector, text, ms = 10000) => {
  const deadline = Date.now() + ms;
  while (Date.now() < deadline) {
    const hit = await page.eval(`(() => {
      const el = [...document.querySelectorAll(${JSON.stringify(selector)})]
        .find(e => e.textContent.trim() === ${JSON.stringify(text)});
      if (!el || el.disabled) return false;
      el.scrollIntoView({ block: 'center' });
      el.click();
      return true;
    })()`);
    if (hit) {
      await sleep(700);
      return;
    }
    await sleep(200);
  }
  throw new Error(`nothing to click: ${selector} "${text}"`);
};

const type = (page, selector, value) => page.eval(`(() => {
  const field = document.querySelector(${JSON.stringify(selector)});
  if (!field) return false;
  const proto = field.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  const setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
  setter.call(field, ${JSON.stringify(value)});
  for (const t of ['input', 'change']) field.dispatchEvent(new Event(t, { bubbles: true }));
  return true;
})()`);

const shot = async (page, name) => {
  if (!shots) return;
  const { data } = await page.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  writeFileSync(`${shots}/${name}.png`, Buffer.from(data, 'base64'));
};

// The DM: whoever started the campaign, on a phone, because that is where a DM runs one.
const dm = await screen(390, 844, true);
await dm.goto(`${client}campaign`);
await waitFor(dm, '.pf-bottomnav__item');
await clickText(dm, 'button', 'Start a new campaign');
await waitFor(dm, '.campaign-code');
const code = await dm.eval(`document.querySelector('.campaign-code').textContent.trim()`);
check('starting a campaign gives a code', /^[A-Z0-9]{4,12}$/.test(code), code);
check('and the screen says this browser is the DM', await dm.eval(`!!document.querySelector('.shell__dm')`));
check('and offers the mode switch', await dm.eval(`!!document.querySelector('.shell__modes')`));

await type(dm, '.pf-textarea', pathbuilder);
await clickText(dm, 'button', 'Import');
await waitFor(dm, '.character');
check('the imported character is on the card', (await dm.eval(`document.querySelector('.character__name').textContent.trim()`)) === 'Gnibbo');
await shot(dm, 'dm-01-exploration');

// Exploration: what everyone is doing, and the one that changes how the fight starts.
check('exploration asks what each character is doing', await dm.eval(
  `!!document.querySelector('[data-doing]')`));
const activities = await dm.eval(
  `[...document.querySelector('[data-doing] select').options].map(o => o.textContent.trim())`);
check('and offers every printed activity plus doing nothing', activities.length === 10,
  activities.join(", "));
check('starting on nothing in particular', activities[0] === 'Nothing in particular', activities[0]);

await dm.eval(`(() => {
  const select = document.querySelector('[data-doing] select');
  const scout = [...select.options].find(o => o.textContent.trim() === 'Scout');
  Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(select, scout.value);
  select.dispatchEvent(new Event('change', { bubbles: true }));
  return true;
})()`);
await sleep(1200);
check('choosing one says what it means at the table', await dm.eval(
  `document.querySelector('.doing__says')?.textContent.trim() ?? ''`).then(t => t.includes('initiative')),
  await dm.eval(`document.querySelector('.doing__says')?.textContent.trim() ?? 'nothing'`));
await shot(dm, 'dm-05-exploration');

// Camp: the ten-minute activities and the clock they add to.
check('the camp panel starts at no time at all', await dm.eval(
  `document.querySelector('.camp__clock-value')?.textContent.trim() ?? ''`).then(t => t === 'no time at all'),
  await dm.eval(`document.querySelector('.camp__clock-value')?.textContent.trim() ?? 'missing'`));

await clickText(dm, '.camping__act', 'Treat Wounds');
const treated = await dm.eval(`({
  clock: document.querySelector('.camp__clock-value')?.textContent.trim() ?? '',
  immune: document.querySelector('.camping__immune')?.textContent.trim() ?? '',
  off: [...document.querySelectorAll('.camping__act')].some(b => b.textContent.trim() === 'Treat Wounds' && b.disabled),
})`);
check('Treat Wounds costs ten minutes', treated.clock === '10 minutes', treated.clock);
check('and leaves an hour of immunity on the target', treated.immune.includes('1 hour'), treated.immune);
check('with the button off while it runs', treated.off);

await clickText(dm, '.camping__act', 'Refocus');
check('another activity moves the clock on', await dm.eval(
  `document.querySelector('.camp__clock-value')?.textContent.trim() ?? ''`).then(t => t === '20 minutes'),
  await dm.eval(`document.querySelector('.camp__clock-value')?.textContent.trim() ?? ''`));

await clickText(dm, '.camp__rest', 'Rest for the night');
const morning = await dm.eval(`({
  clock: document.querySelector('.camp__clock-value')?.textContent.trim() ?? '',
  immune: document.querySelector('.camping__immune')?.textContent.trim() ?? 'none',
})`);
check('a night adds eight hours to the clock', morning.clock === '8 hours 20 minutes', morning.clock);
check('and nobody is still immune in the morning', morning.immune === 'none', morning.immune);
await shot(dm, 'dm-06-camp');


// Into a fight.
await clickText(dm, '.shell__modes button', 'Fight');
await waitFor(dm, '.fight');
check('switching to Fight shows the initiative panel', await dm.eval(`!!document.querySelector('.fight')`));
check('and says nobody is in it yet', await dm.eval(
  `document.body.innerText.includes('Nobody is in this fight yet')`));

await clickText(dm, '.fight__acts button', 'Add');
await waitFor(dm, '.adding__heading');
check('the roster is offered before the bestiary', await dm.eval(
  `(() => {
    const headings = [...document.querySelectorAll('.adding__heading')].map(h => h.textContent.trim());
    return headings.join(' | ') === 'From this campaign | A monster';
  })()`),
  await dm.eval(`[...document.querySelectorAll('.adding__heading')].map(h => h.textContent.trim()).join(' | ')`));
await clickText(dm, '.pf-row__title, .pf-row .title, .pf-row', 'Gnibbo').catch(async () => {
  await dm.eval(`(() => {
    const row = [...document.querySelectorAll('.adding .pf-row')].find(r => r.textContent.includes('Gnibbo'));
    if (row) row.click();
    return !!row;
  })()`);
  await sleep(700);
});

await type(dm, '.adding .pf-search__input', 'Ogre Warrior');
await sleep(1200);
await waitFor(dm, '.adding .pf-row');
const ogre = await dm.eval(`(() => {
  const row = [...document.querySelectorAll('.adding .pf-row')].find(r => r.textContent.includes('Ogre Warrior'));
  if (!row) return null;
  const meta = row.textContent.trim();
  row.click();
  return meta;
})()`);
check('a creature row says its level and how hard it is to hit', /Level \d/.test(ogre ?? ''), ogre ?? 'not found');
await sleep(900);
await shot(dm, 'dm-02-adding');

await dm.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(600);
await waitFor(dm, '.turn');
const roster = await dm.eval(`[...document.querySelectorAll('.turn__name')].map(e => e.textContent.trim())`);
check('both combatants are in the order', roster.length === 2, roster.join(', '));
check('the DM sees the monster hit points', await dm.eval(
  `[...document.querySelectorAll('.turn__line')].some(e => /\\d+ \\/ \\d+ HP/.test(e.textContent))`));

await clickText(dm, '.fight__acts button', 'Roll');
await sleep(1000);
const rolled = await dm.eval(`(() => ({
  initiatives: [...document.querySelectorAll('.turn__initiative')].map(e => Number(e.textContent.trim())),
  current: document.querySelectorAll('.turn--now').length,
  round: document.querySelector('.shell__round')?.textContent.trim() ?? null,
}))()`);
check('rolling gives everyone an initiative', rolled.initiatives.every(n => n > 0), JSON.stringify(rolled.initiatives));
check('in descending order', rolled.initiatives.every((n, i, a) => i === 0 || a[i - 1] >= n), JSON.stringify(rolled.initiatives));
check.eq('exactly one combatant is on turn', rolled.current, 1);
check('and the shell says which round it is', rolled.round === 'Round 1', String(rolled.round));
await shot(dm, 'dm-03-initiative');

const first = await dm.eval(`document.querySelector('.turn--now .turn__name').textContent.trim()`);
await clickText(dm, '.fight__acts button', 'Next turn');
await sleep(900);
const second = await dm.eval(`document.querySelector('.turn--now .turn__name').textContent.trim()`);
check('next turn moves the marker', first !== second, `${first} -> ${second}`);

await clickText(dm, '.fight__acts button', 'Undo');
await sleep(900);
check('undo puts the turn back', await dm.eval(
  `document.querySelector('.turn--now .turn__name').textContent.trim()`) === first, first);

// Editing a character, which is the other half of "campaign" and not "party tracker".
await clickText(dm, '.shell__modes button', 'Explore');
await sleep(700);
await clickText(dm, '.acts button', 'Edit');
await waitFor(dm, '.editor');
const beforeAc = await dm.eval(
  `[...document.querySelectorAll('.stat')].find(e => e.getAttribute('aria-label') === 'Armor Class').querySelector('.stat__value').textContent.trim()`);
await shot(dm, 'dm-04-editor');

const raised = await dm.eval(`(() => {
  const field = [...document.querySelectorAll('.editor__field')]
    .find(f => f.querySelector('.editor__label')?.textContent.trim() === 'Armour bonus');
  const plus = field?.querySelector('.pf-stepper__btn--plus');
  if (!plus) return false;
  plus.click();
  return true;
})()`);
check('the editor offers the armour bonus', raised);
await sleep(400);
await clickText(dm, '.editor__acts button', 'Save');
await sleep(1200);
const afterAc = await dm.eval(
  `[...document.querySelectorAll('.stat')].find(e => e.getAttribute('aria-label') === 'Armor Class').querySelector('.stat__value').textContent.trim()`);
check('saving an edit changes the number it feeds', Number(afterAc) === Number(beforeAc) + 1, `${beforeAc} -> ${afterAc}`);
check('and the editor closed', !(await dm.eval(`!!document.querySelector('.editor')`)));

// A player: same campaign, no key, a second tab.
const player = await screen(390, 844, true);
await player.goto(`${client}campaign`);
await waitFor(player, '.pf-input');
await type(player, '.pf-input', code);
await sleep(400);
await clickText(player, 'button', 'Join');
await waitFor(player, '.character');
check('a player joins with the code alone', await player.eval(`!!document.querySelector('.character')`));
check('and is not offered the mode switch', !(await player.eval(`!!document.querySelector('.shell__modes')`)));
check('and is not marked as the DM', !(await player.eval(`!!document.querySelector('.shell__dm')`)));
await shot(player, 'player-01-exploration');

await clickText(dm, '.shell__modes button', 'Fight');
await sleep(1500);
const seen = await player.eval(`(() => ({
  mode: document.querySelector('.shell__mode')?.textContent.trim() ?? null,
  names: [...document.querySelectorAll('.turn__name')].map(e => e.textContent.trim()),
  monsterNumbers: [...document.querySelectorAll('.turn__line')].map(e => e.textContent.trim()),
  controls: document.querySelectorAll('.fight__acts button').length,
}))()`);
check('the mode the DM chose reaches the player', seen.mode === 'In a fight', String(seen.mode));
check('the player is not offered the encounter controls', seen.controls === 0, String(seen.controls));
check('and sees no monster hit points at all', seen.monsterNumbers.length === 0,
  seen.monsterNumbers.join(' | '));
check('an unrevealed monster is not on their screen either', !seen.names.includes('Ogre Warrior'),
  seen.names.join(', '));
await shot(player, 'player-02-fight');

const errors = [...dm.consoleErrors(), ...player.consoleErrors()]
  .filter(e => !e.includes('ERR_BLOCKED_BY_CLIENT'));
check('no console errors', errors.length === 0, errors.join(' | ').slice(0, 300));

await browser.close();
process.exit(check.done());
