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

const code = await page.eval(`(async () => {
  const letters = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  const code = [...crypto.getRandomValues(new Uint8Array(6))].map(n => letters[n % letters.length]).join('');
  const table = await fetch(${JSON.stringify(api)} + '/tables/' + code).then(r => r.json());
  const added = await fetch(${JSON.stringify(api)} + '/tables/' + code + '/characters', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ pathbuilder: ${JSON.stringify(pathbuilder)} }),
  });
  if (!added.ok) throw new Error('import -> ' + added.status + ' ' + (await added.text()).slice(0, 300));
  return code;
})()`);
check('a table with one imported character exists', /^[A-Z0-9]{6}$/.test(code), code);

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
    const el = [...document.querySelectorAll('.stat')].find(e => e.getAttribute('aria-label') === label);
    return el ? el.querySelector('.stat__value').textContent.trim() : null;
  };
  return {
    ac: stat('Armor Class'),
    will: stat('Will'),
    labels: [...document.querySelectorAll('.stat')].map(e => e.getAttribute('aria-label')),
  };
})()`;

const before = await page.eval(read);
check('the sheet shows named statistics', before.labels.length > 0, before.labels.join(', ').slice(0, 160));
check('armour class is one of them', before.ac !== null, String(before.ac));

await page.eval(`(() => {
  const open = [...document.querySelectorAll('button')].find(b => b.textContent.trim() === 'Effects');
  if (open) open.click();
  return !!open;
})()`);
await waitFor('.conditions');
await shot('02-picker');

const picker = await page.eval(`(() => {
  const kinds = [...document.querySelectorAll('.effects__kind')].map(h => h.textContent.trim());
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

const errors = page.consoleErrors().filter(e => !e.includes('ERR_BLOCKED_BY_CLIENT'));
check('no console errors', errors.length === 0, errors.join(' | ').slice(0, 300));

await browser.close();
process.exit(check.done());
