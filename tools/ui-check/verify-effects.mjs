// Drives the real client to a character's effect picker and asserts that the buffs a table
// applies are there, that each says what it does, and that applying one changes the number it
// is supposed to change and leaves the others alone.
//
//   node tools/ui-check/verify-effects.mjs [client] [api]
//
// Exit code is the number of failed checks.
import { launch, openPage, reporter, sleep } from './cdp.mjs';
import { readFileSync, writeFileSync } from 'node:fs';

const client = process.argv[2] ?? 'http://localhost:5173/';
const api = process.argv[3] ?? 'http://localhost:5092';
const shots = process.env.SHOTS;
const pathbuilder = readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8');

const check = reporter();
const browser = await launch({ headless: process.env.HEADED !== '1' });
const page = await openPage(browser);
await page.viewport(390, 844, true);
await page.coarse(true);

const waitFor = async (selector, ms = 25000) => {
  const deadline = Date.now() + ms;
  while (Date.now() < deadline) {
    if (await page.eval(`!!document.querySelector(${JSON.stringify(selector)})`)) return true;
    await sleep(150);
  }
  throw new Error(`never saw ${selector}`);
};

const click = async (selector) => {
  const hit = await page.eval(`(() => {
    const el = document.querySelector(${JSON.stringify(selector)});
    if (!el) return false;
    el.scrollIntoView({ block: 'center' });
    el.click();
    return true;
  })()`);
  if (!hit) throw new Error(`nothing to click at ${selector}`);
  await sleep(600);
};

const shot = async (name) => {
  if (!shots) return;
  const { data } = await page.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  writeFileSync(`${shots}/${name}.png`, Buffer.from(data, 'base64'));
};

// Start on the client so every fetch below runs from the origin the API allows.
await page.goto(client);
await waitFor('.pf-bottomnav__item');

// A campaign is created rather than conjured by reading a code, and the DM key comes back with
// it. The effect picker is a player screen, so the key is not used below.
const code = await page.eval(`(async () => {
  const made = await fetch(${JSON.stringify(api)} + '/campaigns', { method: 'POST' });
  if (!made.ok) throw new Error('create -> ' + made.status + ' ' + (await made.text()).slice(0, 200));
  const campaign = await made.json();
  const added = await fetch(${JSON.stringify(api)} + '/campaigns/' + campaign.code + '/characters', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ pathbuilder: ${JSON.stringify(pathbuilder)} }),
  });
  if (!added.ok) throw new Error('import -> ' + added.status + ' ' + (await added.text()).slice(0, 300));
  return campaign.code;
})()`);
check('a campaign with one imported character exists', /^[A-Z0-9]{6}$/.test(code), code);

// The page joins a table through its own form, which is the only way a player reaches one.
await page.goto(`${client}party`);
await waitFor('.pf-input, .pf-textarea');
await page.eval(`(() => {
  const field = document.querySelector('.pf-input');
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
  setter.call(field, ${JSON.stringify(code)});
  for (const type of ['input', 'change']) field.dispatchEvent(new Event(type, { bubbles: true }));
  return field.value;
})()`);
await sleep(600);
const joined = await page.eval(`(() => {
  const join = [...document.querySelectorAll('button')].find(b => b.textContent.trim() === 'Join');
  if (!join || join.disabled) return { clicked: false, disabled: join?.disabled ?? null };
  join.click();
  return { clicked: true };
})()`);
check('the join button takes the code', joined.clicked, JSON.stringify(joined));
await waitFor('.character');
await shot('01-party');

const read = `(() => {
  const stat = label => {
    const el = [...document.querySelectorAll('.stat')].find(e => e.getAttribute('aria-label')?.startsWith(label));
    return el ? el.querySelector('.stat__value').textContent.trim() : null;
  };
  const roll = name => {
    const el = [...document.querySelectorAll('.roll')].find(e => e.querySelector('.roll__name').textContent.trim() === name);
    return el ? el.querySelector('.roll__value').textContent.trim() : null;
  };
  return {
    ac: stat('Armor Class'),
    will: stat('Will'),
    spellAttack: stat('Spell Attack'),
    rapier: roll('+1 Striking Rapier'),
    performance: roll('Performance'),
    labels: [...document.querySelectorAll('.stat')].map(e => e.getAttribute('aria-label').replace(/ [+-]?\\d.*$/, '')),
    rolls: [...document.querySelectorAll('.roll__name')].filter(e => !e.closest('details')).map(e => e.textContent.trim()),
    hidden: [...document.querySelectorAll('details .roll__name')].map(e => e.textContent.trim()),
  };
})()`;

const before = await page.eval(read);
check('the sheet shows named statistics', before.labels.length > 0, before.labels.join(', ').slice(0, 160));
check('armour class is one of them', before.ac !== null, String(before.ac));
check('a caster gets a spell attack and a spell DC', before.labels.includes('Spell Attack') && before.labels.includes('Spell DC'), before.labels.join(', '));
check('the weapons the export carried are on the card', before.rapier === '+15', String(before.rapier));
check('so are the skills', before.performance === '+17', String(before.performance));
check('an untrained skill is behind the summary, not in the open list',
  !before.rolls.includes('Athletics') && before.hidden.includes('Athletics'),
  `open: ${before.rolls.join(', ').slice(0, 120)} | behind: ${before.hidden.join(', ').slice(0, 80)}`);
check('a save reads as a roll, with a sign', /^[+-]/.test(String(before.will)), String(before.will));

await page.eval(`(() => {
  const open = [...document.querySelectorAll('button')].find(b => b.textContent.trim() === 'Effects');
  if (open) open.click();
  return !!open;
})()`);
await waitFor('.conditions');
await shot('02-picker');

const picker = await page.eval(`(() => {
  const kinds = [...document.querySelectorAll('[data-effect-group]')].map(g => g.dataset.effectGroup);
  const rows = [...document.querySelectorAll('.condition')].map(el => ({
    key: el.dataset.condition,
    reads: el.querySelector('.condition__reads')?.textContent.trim() ?? '',
  }));
  return { kinds, rows };
})()`);

check('the picker separates buffs from conditions', picker.kinds.join(' | ') === 'Buffs | Conditions', picker.kinds.join(' | '));
for (const key of ['raise-a-shield', 'cover', 'shield-spell', 'bless', 'courageous-anthem', 'rallying-anthem', 'heroism']) {
  const row = picker.rows.find(r => r.key === key);
  check(`${key} is offered`, !!row, row?.reads ?? 'missing');
  if (row) check(`${key} says what it does`, row.reads.length > 0, row.reads);
}
check('every offered effect states its modifiers', picker.rows.every(r => r.reads.length > 0),
  picker.rows.filter(r => !r.reads).map(r => r.key).join(', ') || 'all of them do');

// Raise a buckler. Armour class should move by one and nothing else should move at all.
await click('[data-condition="raise-a-shield"] .pf-stepper__btn:last-of-type');
await sleep(900);
const after = await page.eval(read);
await shot('03-shield-raised');

const num = v => Number(String(v ?? '').replace(/[^\d-]/g, ''));
check('raising a shield lifts armour class by one', num(after.ac) === num(before.ac) + 1,
  `${before.ac} -> ${after.ac}`);
check('and leaves Will where it was', num(after.will) === num(before.will), `${before.will} -> ${after.will}`);

// A bless is the buff that used to land nowhere a player could see. It moves attack rolls and
// nothing else, so it is the check that the sheet grew the rows the effect was always reaching.
await page.eval(`(() => {
  const open = [...document.querySelectorAll('button')].find(b => b.textContent.trim() === 'Effects');
  if (open) open.click();
  return !!open;
})()`);
await waitFor('.conditions');
await click('[data-condition="bless"] .pf-switch__input');
await sleep(900);
await page.eval(`(() => {
  const close = document.querySelector('.pf-sheet__close');
  if (close) close.click();
  return !!close;
})()`);
await sleep(600);
const blessed = await page.eval(read);
await shot('04-blessed');

check('a bless lifts the weapon attack', num(blessed.rapier) === num(after.rapier) + 1,
  `${after.rapier} -> ${blessed.rapier}`);
check('and the spell attack with it', num(blessed.spellAttack) === num(after.spellAttack) + 1,
  `${after.spellAttack} -> ${blessed.spellAttack}`);
check('and leaves a skill alone', num(blessed.performance) === num(after.performance),
  `${after.performance} -> ${blessed.performance}`);

const errors = page.consoleErrors().filter(e => !e.includes('ERR_BLOCKED_BY_CLIENT'));
check('no console errors', errors.length === 0, errors.join(' | ').slice(0, 300));

await browser.close();
process.exit(check.done());
