// Drives the party tracker in a real Chrome and asserts the numbers the screen promises: the
// Goblin Bard imports at 76 and 25, clumsy 2 takes armour class to 23, a custom effect typed at
// the table lands like a seeded one, two status bonuses of different sizes leave only the larger
// applied with the smaller visibly suppressed, a hit-point delta lands on the server's number,
// and a second page on the same table follows every one of those without a reload.
//
//   dotnet run --project src/Pf2e.Api --urls http://localhost:5092
//   dotnet run --project src/Pf2e.Client --launch-profile http
//   node tools/ui-check/verify-party.mjs [clientUrl] [apiOrigin] [fixture]
//
// The API origin is asserted, not assumed. Several worktrees of this app run on one machine and
// the client reads its API address from a static file, so a client pointed at another
// worktree's API passes every visible assertion while proving nothing about this build.
//
// Exit code is the number of failures.
import { readFileSync } from 'node:fs';
import { launch, openPage, reporter, sleep } from './cdp.mjs';

const client = process.argv[2] ?? 'http://localhost:5173';
const apiOrigin = process.argv[3] ?? 'http://localhost:5092';
const fixture = process.argv[4] ?? 'tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json';
const pathbuilder = readFileSync(fixture, 'utf8');

const check = reporter();
const browser = await launch({ headless: true });

function driver(page) {
  const q = (selector) => JSON.stringify(selector);

  const api = {
    page,
    count: (selector) => page.eval(`document.querySelectorAll(${q(selector)}).length`),
    text: (selector) => page.eval(`document.querySelector(${q(selector)})?.textContent?.trim() ?? null`),
    number: async (selector) => Number.parseInt(await api.text(selector) ?? '', 10),
    click: (selector) => page.eval(
      `(() => { const el = document.querySelector(${q(selector)}); if (!el) return false; el.click(); return true; })()`),
    clickText: (selector, label) => page.eval(
      `(() => {
         const el = [...document.querySelectorAll(${q(selector)})]
           .find(n => n.textContent.trim() === ${JSON.stringify(label)});
         if (!el) return false;
         el.click();
         return true;
       })()`),
    // Blazor listens for the DOM event, so the native setter has to be used or the framework
    // never learns the value changed.
    set: (selector, value, event) => page.eval(
      `(() => {
         const el = document.querySelector(${q(selector)});
         if (!el) return false;
         const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement
                     : el instanceof HTMLSelectElement ? HTMLSelectElement
                     : HTMLInputElement;
         Object.getOwnPropertyDescriptor(proto.prototype, 'value').set.call(el, ${JSON.stringify(value)});
         el.dispatchEvent(new Event(${JSON.stringify(event)}, { bubbles: true }));
         return true;
       })()`),
    type: (selector, value) => api.set(selector, value, 'input'),
    choose: (selector, value) => api.set(selector, value, 'change'),
    async waitFor(selector, timeout = 20000) {
      for (let waited = 0; waited < timeout; waited += 100) {
        if (await api.count(selector) > 0) return true;
        await sleep(100);
      }
      return false;
    },
    async waitUntil(expression, timeout = 20000) {
      for (let waited = 0; waited < timeout; waited += 100) {
        if (await page.eval(`(() => { try { return !!(${expression}); } catch { return false; } })()`)) return true;
        await sleep(100);
      }
      return false;
    },
    stat: async (label) => Number.parseInt(await page.eval(
      `document.querySelector('.stat[aria-label^="${label}"] .stat__value')?.textContent?.trim() ?? ''`), 10),
    async closeSheet() {
      await api.click('.pf-sheet__close');
      await api.waitUntil('document.querySelectorAll(".pf-sheet__panel").length === 0');
    },
    async openBreakdown(label) {
      await api.click(`.stat[aria-label^="${label}"]`);
      await api.waitFor('.breakdown');
    },
    async addCustom(name, sign, value, type, applies) {
      await api.clickText('button', 'Effects');
      await api.waitFor('.pf-sheet__panel');
      await api.clickText('.picker__arms button', 'Custom');
      await api.waitFor('.custom');
      await api.type('.custom .pf-input', name);
      await api.clickText('.custom__sign button', sign);
      await api.choose('.custom__type .pf-select__native', type);
      await api.choose('.custom__applies .pf-select__native', applies);
      for (let step = 1; step < value; step++) {
        await api.click('.custom__value .pf-stepper__btn--plus');
      }
      await api.clickText('.custom button', 'Add');
      await api.waitUntil(
        `[...document.querySelectorAll('.applied')].some(n => n.textContent.includes(${JSON.stringify(name)}))`);
      await api.closeSheet();
    },
  };

  return api;
}

const one = driver(await openPage(browser));
await one.page.viewport(390, 844, true);
await one.page.goto(`${client}/party`);

check('the party screen offers a way in', await one.waitFor('.join'));

await one.clickText('button', 'Start a new campaign');
check('a new campaign gets a code', await one.waitFor('.campaign-code'));
const code = (await one.text('.campaign-code')).trim();
check('the code is six readable characters', /^[A-HJ-NP-Z2-9]{6}$/.test(code), code);

await one.type('.pf-textarea', pathbuilder);
await one.clickText('button', 'Import');
check('the import puts a character on the table', await one.waitFor('.character'));

check.eq('the Goblin Bard arrives named', await one.text('.character__name'), 'Gnibbo');
check.eq('maximum hit points are 76', await one.number('.hp__max'), 76);
check.eq('current hit points start at maximum', await one.number('.hp__current'), 76);
check.eq('armour class is 25', await one.stat('Armor Class'), 25);

const willBefore = await one.stat('Will');
const reflexBefore = await one.stat('Reflex');

await one.clickText('button', 'Effects');
check('the effect picker opens', await one.waitFor('.pf-sheet__panel'));
check('it offers all three arms', await one.count('.picker__arms button') >= 3);
check('clumsy is offered on the conditions arm', await one.waitFor('[data-condition="clumsy"]'));

for (const step of [1, 2]) {
  await one.click('[data-condition="clumsy"] .pf-stepper__btn--plus');
  check(`clumsy reaches ${step}`, await one.waitUntil(
    `document.querySelector('[data-condition="clumsy"] .pf-stepper__value')?.textContent?.trim() === '${step}'`));
}
await one.closeSheet();

check('clumsy 2 lands on the card', await one.waitUntil(
  `[...document.querySelectorAll('.chips *')].some(n => n.textContent.trim().toLowerCase().includes('clumsy'))`));
check.eq('clumsy 2 takes armour class to 23', await one.stat('Armor Class'), 23);
check.eq('clumsy 2 takes Reflex down 2 as well', await one.stat('Reflex'), reflexBefore - 2);
check.eq('clumsy 2 leaves Will alone', await one.stat('Will'), willBefore);

await one.openBreakdown('Armor Class');
const sheetTitle = await one.text('.pf-sheet__title');
check('the sheet is titled with the statistic and its total',
  sheetTitle.includes('Armor Class') && sheetTitle.includes('23'), sheetTitle);
const applied = await one.text('.breakdown__applied');
check('the breakdown names the modifier that moved it', applied.toLowerCase().includes('clumsy'), applied);
check('the breakdown still names the armour', applied.toLowerCase().includes('studded leather'), applied);
check('the breakdown states the base value', (await one.text('.breakdown__base'))?.length > 0);
await one.closeSheet();

// design/005 in one gesture: a buff a player types behaves exactly like a printed condition,
// because both reach Stacking.Resolve by the same path.
await one.addCustom('Courageous Anthem', 'Bonus', 1, 'Status', 'stat:Will');
check.eq('a custom status bonus lands on the number it names', await one.stat('Will'), willBefore + 1);
check('the custom effect shows on the card', await one.waitUntil(
  `[...document.querySelectorAll('.chips *')].some(n => n.textContent.includes('Courageous Anthem'))`));

await one.addCustom('Heroism', 'Bonus', 2, 'Status', 'stat:Will');
check.eq('two status bonuses do not stack, the larger wins', await one.stat('Will'), willBefore + 2);

await one.openBreakdown('Will');
const willApplied = await one.text('.breakdown__applied');
const willSuppressed = await one.text('.breakdown__suppressed');
check('the larger bonus is the applied one', willApplied.includes('Heroism'), willApplied);
check('the smaller one is shown as suppressed rather than dropped',
  willSuppressed.includes('Courageous Anthem'), willSuppressed);
check('the suppression carries the reason the engine gave',
  willSuppressed.toLowerCase().includes('does not stack'), willSuppressed);
check('the suppressed row is not also an applied row',
  !willApplied.includes('Courageous Anthem'), willApplied);
await one.closeSheet();

// A second page joined to the same code is the only way to tell a live push from a local
// re-render, which is the whole reason the hub exists.
const two = driver(await openPage(browser));
await two.page.viewport(1440, 900, false);
await two.page.goto(`${client}/party`);
await two.waitFor('.join');
await two.type('.join .pf-input', code);
await two.clickText('button', 'Join');
check('a second page joins the same table', await two.waitFor('.character'));
check.eq('the second page sees the same armour class', await two.stat('Armor Class'), 23);
check.eq('the second page sees the same Will', await two.stat('Will'), willBefore + 2);

// The card carried two ways to change one number: a typed amount with Damage and Heal, and a
// stepper beside it for one point at a time. The typed field takes a one, so the stepper was
// a second control for a job the first already did.
const hurt = async (amount) => {
  await one.type('.character .amount__field input', String(amount));
  await one.clickText('.character .amount button', 'Damage');
};

await hurt(1);
check('a hit-point delta lands on the first page', await one.waitUntil(
  `Number.parseInt(document.querySelector('.hp__current').textContent, 10) === 75`));
check.eq('the card shows the server number, not a guess', await one.number('.hp__current'), 75);
check('the second page follows without a reload', await two.waitUntil(
  `Number.parseInt(document.querySelector('.hp__current').textContent, 10) === 75`));

await hurt(1);
check('two deltas sum rather than clobber', await one.waitUntil(
  `Number.parseInt(document.querySelector('.hp__current').textContent, 10) === 74`));
check('the second page follows the second delta too', await two.waitUntil(
  `Number.parseInt(document.querySelector('.hp__current').textContent, 10) === 74`));

for (const [name, page] of [['first', one.page], ['second', two.page]]) {
  const calls = (await page.eval(`performance.getEntriesByType('resource').map(e => e.name)`))
    .filter(url => /\/(tables|rules|conditions)(\/|\?|$)/.test(url) || url.includes('/hub/'));
  const strays = [...new Set(calls.filter(url => !url.startsWith(apiOrigin)))];
  check(`the ${name} page reached an API`, calls.length > 0, `${calls.length} calls`);
  check(`every ${name}-page API call went to the API under test`, strays.length === 0, strays.join(' | '));

  // A websocket never appears in resource timings, so the hub's destination is only checkable
  // through the HTTP negotiate that precedes it. No negotiate means unverified, not fine.
  const negotiate = calls.filter(url => url.includes('/negotiate'));
  check(`the ${name} page's hub negotiated`, negotiate.length > 0,
    negotiate[0] ?? 'no negotiate call seen, so the hub origin is unverified');
  check(`the ${name} page's hub negotiated against the API under test`,
    negotiate.length > 0 && negotiate.every(url => url.startsWith(apiOrigin)), negotiate.join(' | '));

  const errors = page.consoleErrors().filter(entry => !entry.includes('ERR_BLOCKED_BY_CLIENT'));
  check.eq(`no console errors on the ${name} page`, errors.length, 0, errors.join(' | '));
}

await browser.close();
process.exit(check.done());
