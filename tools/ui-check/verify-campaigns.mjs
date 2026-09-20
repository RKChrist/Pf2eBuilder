// A browser can be in several campaigns at once, and leaving one must not lose it.
//
//   node tools/ui-check/verify-campaigns.mjs
//
// Exit code is the number of failures.
//
// This browser used to keep exactly one campaign, under `pf2e.campaign`, and joining or creating
// another wrote straight over it. There was no way out of a campaign either: once one was open,
// the join form never came back. So a GM running two tables lost the first one the moment they
// started the second, and lost it for good, because the DM key is shown nowhere and exists
// nowhere else. Without that key there is no Add, no Roll and no Next turn, and the monsters
// they had not revealed yet are gone off their own screen.
import { launch, openPage, sleep, reporter } from './cdp.mjs';

const client = process.argv[2] ?? 'http://localhost:5173';
const CODE = /^[A-HJ-NP-Z2-9]{6}$/;

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

const click = async (selector) => {
  const hit = await p.eval(`(() => {
    const el = document.querySelector(${JSON.stringify(selector)});
    if (!el || el.disabled) return false;
    el.scrollIntoView({ block: 'center' });
    el.click();
    return true;
  })()`);
  await sleep(900);
  return hit;
};

// Both scoped, because the remembered-campaigns list on the join form renders a code and a DM
// mark of its own for every row. Unscoped, either of these would pass while standing nowhere
// near a campaign.
const headerCode = () =>
  p.eval(`document.querySelector('.top .aside .campaign-code')?.textContent.trim() ?? ''`);
const isGm = () => p.eval(`!!document.querySelector('.shell .shell__dm')`);

const called = () =>
  p.eval(`[...document.querySelectorAll('.turn__called')].map(e => e.textContent.trim())`);
const kept = () => p.eval(`[...document.querySelectorAll('.kept__row')].map(row => ({
  code: row.dataset.code ?? '',
  dm: !!row.querySelector('.shell__dm'),
}))`);

await p.goto(`${client}/campaign`);
await wait('.join');
await clickText('button', 'Start a new campaign');
await wait('.top .aside .campaign-code');

const first = await headerCode();
check('a table can be started', CODE.test(first), first);
check('and whoever started it is its GM', await isGm());

// A monster the players have not been shown, because that is the part of a lost campaign a GM
// notices last and minds most.
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

const brought = await called();
check('the GM puts a monster in the fight nobody has seen',
  brought.includes('Ogre Warrior'), brought.join(', '));

check('a campaign offers a way out of it', await click('.leave'));
check('and leaving lands back on the join form', await wait('.join'));
check('with no campaign in the header any more',
  await p.eval(`!document.querySelector('.top .aside .campaign-code')`), await headerCode());

// Scoped, because the list this browser is now holding puts a code on the join form too, and an
// unscoped wait would come back before the second table had opened.
await clickText('button', 'Start a new campaign');
await wait('.top .aside .campaign-code');

const second = await headerCode();
check('a second table can be started from there', CODE.test(second), second);
check('and it is a table of its own', second !== first, `${first} then ${second}`);

await click('.leave');
await wait('.kept');

const both = await kept();
const codes = both.map((row) => row.code);
check('the join form lists both tables this browser has been at',
  both.length === 2 && codes.includes(first) && codes.includes(second), codes.join(', '));
check('with the one just left at the top', both[0]?.code === second, codes.join(', '));
check('and the DM mark on both, because this browser started both',
  both.every((row) => row.dm), both.map((row) => `${row.code}:${row.dm}`).join(', '));

check('the first table opens again from the list',
  await click(`.kept__row[data-code="${first}"] .kept__open`) && await wait('.shell', 30000));
const reopened = await headerCode();
check('as the same table', reopened === first, `${first} -> ${reopened}`);
check('whose GM is the GM again', await isGm(), 'switching tables used to cost the DM key');

await p.eval(`document.querySelector('.shell__mode-link[data-mode="Encounter"]')?.click()`);
await wait('.fight', 30000);
const controls = await p.eval(`document.querySelectorAll('.fight__acts button, .now button').length`);
check('with the encounter controls back', controls === 4, String(controls));
const still = await called();
check('and the monster nobody was shown still on the GM screen',
  still.includes('Ogre Warrior'), still.join(', '));

await click('.leave');
await wait('.kept');
// Held, not clicked. Forgetting is the only thing in the app that destroys a DM key, so the
// button waits for a press that lasts, and a check that clicked it would be asserting against a
// control the product does not have.
const where = async (code) => {
  const found = await p.eval(`(() => {
    const el = document.querySelector('.kept__row[data-code="' + ${JSON.stringify(code)} + '"] .kept__forget');
    if (!el) return null;
    el.scrollIntoView({ block: 'center' });
    const r = el.getBoundingClientRect();
    return JSON.stringify({ x: r.x + r.width / 2, y: r.y + r.height / 2 });
  })()`);
  return found ? JSON.parse(found) : null;
};

const holdForget = async (code) => {
  const at = await where(code);
  if (!at) return false;
  await p.mouseDown(at.x, at.y);
  await sleep(1200);
  await p.mouseUp(at.x, at.y);
  await sleep(900);
  return true;
};

// The half that makes the hold worth having. A tap is what a thumb does by accident next to
// Continue, and it has to cost nothing.
//
// A real press rather than el.click(). A programmatic click reports detail 0, which the kit
// passes straight through on purpose so that a keyboard activation is not asked to hold a key
// down, and a check that used one would be asserting against a gesture no thumb makes.
const tapForget = async (code) => {
  const at = await where(code);
  if (!at) return false;
  await p.mouseDown(at.x, at.y);
  await sleep(80);
  await p.mouseUp(at.x, at.y);
  await sleep(900);
  return true;
};

check('the control is there to press', await tapForget(second));
const afterTap = (await kept()).map((row) => row.code);
check('and a tap on it forgets nothing', afterTap.includes(second), afterTap.join(', '));

check('a table can be forgotten by holding the control that says so', await holdForget(second));

const rest = await kept();
check('and the forgotten one leaves the list',
  !rest.some((row) => row.code === second), rest.map((row) => row.code).join(', '));
check('while the other one stays, still with its DM mark',
  rest.some((row) => row.code === first && row.dm),
  rest.map((row) => `${row.code}:${row.dm}`).join(', '));

// Either casing, because which one the app's serializer writes is not this file's business.
const stored = await p.eval(`localStorage.getItem('pf2e.campaigns')`);
const remembered = JSON.parse(stored ?? '[]').map((campaign) => campaign.Code ?? campaign.code);
check('the kept table survives in storage', remembered.includes(first), String(stored).slice(0, 200));
check('and the forgotten one does not', !remembered.includes(second), String(stored).slice(0, 200));
check('and the single-campaign slot this replaces is gone',
  await p.eval(`localStorage.getItem('pf2e.campaign')`) === null,
  'one slot per browser is what lost the first table');

// Everything above is a path a GM actually walks, so it has to be quiet.
const errors = p.consoleErrors().filter((e) => !e.includes('ERR_BLOCKED_BY_CLIENT'));
check('no console errors along the way', errors.length === 0, errors.join(' | ').slice(0, 200));

await browser.close();
process.exit(check.done());
