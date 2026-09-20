// A reload must not end the session, and must not end the GM.
//
//   node tools/ui-check/verify-reload.mjs
//
// Exit code is the number of failures.
//
// Before this was written, nothing in the client persisted anything. The campaign code lived in
// memory and the DM key arrived once from creating the campaign and was never shown or stored.
// So a GM whose tab reloaded, which phones do on their own, came back as a player for the rest
// of the session: no Add, no Roll, no Next turn, no initiative fields, and the monsters they had
// not revealed vanished off their own screen. There was no way back except a new campaign and
// re-importing everybody.
import { launch, openPage, sleep, reporter } from './cdp.mjs';
import { readFileSync } from 'node:fs';

const client = process.argv[2] ?? 'http://localhost:5173';
const fixture = readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8');

const check = reporter();
const browser = await launch({ headless: process.env.HEADED !== '1' });
const p = await openPage(browser);
await p.viewport(390, 844, true);

const wait = async (selector, ms = 25000) => {
  const deadline = Date.now() + ms;
  while (Date.now() < deadline) {
    if (await p.eval(`!!document.querySelector(${JSON.stringify(selector)})`)) return true;
    await sleep(200);
  }
  return false;
};

const clickText = async (selector, text) => {
  await p.eval(`(() => {
    const el = [...document.querySelectorAll(${JSON.stringify(selector)})]
      .find(e => e.textContent.trim() === ${JSON.stringify(text)});
    if (el && !el.disabled) el.click();
  })()`);
  await sleep(900);
};

await p.goto(`${client}/campaign`);
await wait('.join');
await clickText('button', 'Start a new campaign');
await wait('.campaign-code');

const code = await p.eval(`document.querySelector('.campaign-code')?.textContent.trim() ?? ''`);
check('a campaign was created', /^[A-HJ-NP-Z2-9]{6}$/.test(code), code);

await p.eval(`(() => {
  const box = document.querySelector('.pf-textarea');
  Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set
    .call(box, ${JSON.stringify(fixture)});
  for (const t of ['input', 'change']) box.dispatchEvent(new Event(t, { bubbles: true }));
})()`);
await clickText('button', 'Import');
check('a character is on the roster', await wait('.character'));

// Into a fight, with a monster the players have not been shown, because losing sight of that is
// the part a GM notices last and minds most.
await p.eval(`document.querySelector('.shell__mode-link[data-mode="Encounter"]')?.click()`);
await sleep(1200);
await clickText('.fight__acts button', 'Add');
await wait('.adding .pf-section__label');
await p.eval(`(() => {
  const field = document.querySelector('.adding .pf-search__input');
  Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(field, 'Ogre Warrior');
  for (const t of ['input', 'change']) field.dispatchEvent(new Event(t, { bubbles: true }));
})()`);
await sleep(2200);
await p.eval(`[...document.querySelectorAll('.adding .pf-row')]
  .find(r => r.textContent.includes('Ogre Warrior'))?.click()`);
await sleep(1200);
await p.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(800);

const before = await p.eval(`(() => ({
  dm: !!document.querySelector('.shell__dm'),
  controls: document.querySelectorAll('.fight__acts button, .now button').length,
  rows: [...document.querySelectorAll('.turn__called')].map(e => e.textContent.trim()),
}))()`);
check('the GM is the GM', before.dm);
check('and has the encounter controls', before.controls === 4, String(before.controls));
check('and can see the monster nobody has been shown', before.rows.includes('Ogre Warrior'),
  before.rows.join(', '));

// The thing a phone does on its own.
await p.goto(`${client}/campaign/encounter`);
check('the campaign comes back after a reload', await wait('.fight', 30000));

const after = await p.eval(`(() => ({
  code: document.querySelector('.campaign-code')?.textContent.trim() ?? '',
  dm: !!document.querySelector('.shell__dm'),
  controls: document.querySelectorAll('.fight__acts button, .now button').length,
  rows: [...document.querySelectorAll('.turn__called')].map(e => e.textContent.trim()),
  join: !!document.querySelector('.join'),
}))()`);

check('it is the same campaign', after.code === code, `${code} -> ${after.code}`);
check('not the join form', !after.join);
check('the GM is still the GM', after.dm, 'a reloaded GM used to come back as a player');
check('and still has the encounter controls', after.controls === 4, String(after.controls));
check('and can still see the unrevealed monster', after.rows.includes('Ogre Warrior'),
  after.rows.join(', '));

// Everything above is the path a table actually takes, so it has to be quiet. Counted here
// rather than at the end, because the probe below asks the server for a campaign that does not
// exist and a 404 is the correct answer to that.
{
  const errors = p.consoleErrors().filter((e) => !e.includes('ERR_BLOCKED_BY_CLIENT'));
  check('no console errors up to here', errors.length === 0, errors.join(' | ').slice(0, 200));
}

// A remembered campaign the server no longer has must not strand anybody on a dead screen.
await p.eval(`localStorage.setItem('pf2e.campaign', JSON.stringify({ Code: 'ZZZZZZ', DmKey: null }))`);
await p.goto(`${client}/campaign`);
check('a campaign that is gone falls back to the join form', await wait('.join', 20000));
check('and is not remembered again',
  await p.eval(`localStorage.getItem('pf2e.campaign')`) === null);

await browser.close();
process.exit(check.done());
