// Drives the running client in a real Chrome and asserts the behaviour the screens promise:
// the search debounce, the deep link, back and Escape closing the sheet, paging, the level
// range, and a failure with a retry that recovers. Requests are blocked through the protocol
// rather than by stopping the API, so the failure arm is exercised without a second terminal.
//
//   dotnet run --project src/Pf2e.Api
//   dotnet run --project src/Pf2e.Client --launch-profile http
//   node tools/ui-check/verify-client.mjs
//
// Exit code is the number of failures.
import { launch, openPage, reporter, sleep } from './cdp.mjs';

const client = process.argv[2] ?? 'http://localhost:5173/';
const api = process.argv[3] ?? 'http://localhost:5092';

const check = reporter();
const browser = await launch({ headless: true });
const page = await openPage(browser);
await page.send('Network.enable');
await page.viewport(390, 844, true);

const click = (selector) => page.eval(
  `(() => { const el = document.querySelector(${JSON.stringify(selector)}); if (!el) return false; el.click(); return true; })()`);

const text = (selector) => page.eval(
  `document.querySelector(${JSON.stringify(selector)})?.textContent?.trim() ?? null`);

const count = (selector) => page.eval(`document.querySelectorAll(${JSON.stringify(selector)}).length`);

const searches = () => page.eval(
  `performance.getEntriesByType('resource').filter(e => e.name.includes('/rules?')).map(e => e.name)`);

async function waitFor(selector, timeout = 15000) {
  for (let waited = 0; waited < timeout; waited += 100) {
    if (await count(selector) > 0) return true;
    await sleep(100);
  }
  return false;
}

async function openFeats() {
  await click('.pf-bottomnav__item:nth-child(2)');
  await waitFor('.pf-row');
  await click('.pf-row');
  await waitFor('.pf-row__title');
}

await page.goto(client);
await waitFor('.pf-bottomnav__item');

check.eq('six navigation items', await count('.pf-bottomnav__item'), 6);
check('the group opens its category list', await waitFor('.pf-row'));

await openFeats();
check('a category lists its records', await count('.pf-row') > 1);
check('the level filter is one dual-thumb range', await count('.pf-slider--range') === 1);

const before = (await searches()).length;
for (const typed of ['s', 'sh', 'shi', 'shie', 'shiel', 'shield']) {
  await page.eval(`(() => {
    const field = document.querySelector('.pf-search__input');
    field.value = ${JSON.stringify(typed)};
    field.dispatchEvent(new Event('input', { bubbles: true }));
    return true;
  })()`);
  await sleep(50);
}
await sleep(1200);
const after = await searches();
check.eq('six keystrokes make one search', after.length - before, 1);
check('the last keystroke is the one searched', after.at(-1).includes('Name=shield'), after.at(-1));
check('the results are the searched ones',
  (await text('.pf-row__title')).toLowerCase().includes('shield'), await text('.pf-row__title'));

await page.eval(`(() => {
  const field = document.querySelector('.pf-search__input');
  field.value = '';
  field.dispatchEvent(new Event('input', { bubbles: true }));
  return true;
})()`);
await sleep(1200);

const firstPage = await text('.pf-pager__status');
await click('.pf-pager__btn--next');
await sleep(1200);
check('the pager turns the page', (await text('.pf-pager__status')) !== firstPage,
  `${firstPage} -> ${await text('.pf-pager__status')}`);

const levelled = await page.eval(`(() => {
  const low = document.querySelector('.pf-slider__input--low');
  low.value = '5';
  low.dispatchEvent(new Event('input', { bubbles: true }));
  low.dispatchEvent(new Event('change', { bubbles: true }));
  return true;
})()`);
await sleep(1200);
check('the range slider filters by level', levelled && (await searches()).at(-1).includes('MinLevel=5'),
  (await searches()).at(-1));
check('every listed record is inside the range', await page.eval(`
  [...document.querySelectorAll('.pf-row__meta')].every(meta => {
    const level = meta.textContent.match(/Level (-?\\d+)/);
    return !level || Number(level[1]) >= 5;
  })`));

// The busiest screen at the narrowest phone, which measure.mjs cannot reach: it takes a URL,
// and the records, their filters and the pager are three taps in.
await page.coarse(true);
await page.viewport(320, 740, true);
await sleep(400);
const narrow = await page.eval(`(() => {
  const viewport = document.documentElement.clientWidth;
  const boxes = [...document.querySelectorAll('button, a, input, select')]
    .map(el => ({ el, box: el.getBoundingClientRect() }))
    .filter(({ box }) => box.width > 0 && box.height > 0);
  const smallest = boxes.reduce((worst, item) =>
    Math.min(item.box.width, item.box.height) < Math.min(worst.box.width, worst.box.height) ? item : worst);
  return {
    smallest: Math.round(Math.min(smallest.box.width, smallest.box.height)),
    culprit: smallest.el.className,
    past: [...document.querySelectorAll('body *')].filter(el => {
      const box = el.getBoundingClientRect();
      if (box.width === 0 || box.height === 0) return false;
      if (box.right <= viewport + 1 && box.left >= -1) return false;
      // A strip that scrolls sideways on purpose, such as the trait filter, holds its
      // content past the edge by design. Everything else past the edge is a defect.
      for (let parent = el.parentElement; parent; parent = parent.parentElement) {
        if (['auto', 'scroll'].includes(getComputedStyle(parent).overflowX)) return false;
      }
      return true;
    }).length,
    sideways: document.documentElement.scrollWidth > viewport,
  };
})()`);
check('no tap target under 44px at 320', narrow.smallest >= 44, `${narrow.smallest}px on ${narrow.culprit}`);
check.eq('nothing past the right edge at 320', narrow.past, 0);
check('no sideways scroll at 320', !narrow.sideways);
await page.viewport(390, 844, true);
await page.coarse(false);
await sleep(300);

const rowName = await text('.pf-row__title');
await click('.pf-row');
check('a record opens the sheet', await waitFor('.pf-sheet__panel'));
await sleep(500);
check.eq('the sheet is titled with the record', await text('.pf-sheet__title'), rowName);
check('the record is in the address bar', (await page.eval('location.search')).includes('rule='));
check('the sheet takes focus', await page.eval(
  `document.activeElement?.classList.contains('pf-sheet__panel') === true`));

const deepLink = await page.eval('location.href');
await page.key('Escape', 'Escape', 27);
await sleep(600);
check.eq('Escape closes the sheet', await count('.pf-sheet__panel'), 0);
check('Escape leaves the list behind it', await count('.pf-row') > 1);

await click('.pf-row');
await waitFor('.pf-sheet__panel');
await sleep(400);
await page.eval('history.back()');
await sleep(800);
check.eq('back closes the sheet', await count('.pf-sheet__panel'), 0);
check('back stays in the app', (await page.eval('location.pathname')) === '/');

await page.goto(deepLink);
check('a deep link restores the record', await waitFor('.pf-sheet__panel'));
await sleep(600);
check.eq('the deep-linked record is the right one', await text('.pf-sheet__title'), rowName);
check('the sheet names where the rule is printed',
  (await text('.pf-sheet__body')).includes('Archives of Nethys'));

await page.goto(`${client}conditions`);
check('the conditions screen loads', await waitFor('.pf-card'));
check('a condition states its modifiers', await count('.pf-mod') > 0);
check('a penalty is drawn as one', await page.eval(
  `[...document.querySelectorAll('.pf-mod')].some(m => m.classList.contains('pf-mod--penalty'))`));

await page.send('Network.setBlockedURLs', { urls: [`${api}/*`] });
await page.goto(client);
await waitFor('.pf-bottomnav__item');
await openFeats();
check('a dead service shows the failure', await waitFor('.pf-state--error'));
check('the failure says what went wrong',
  (await text('.pf-state--error')).includes('not reachable'), await text('.pf-state--error'));

await page.send('Network.setBlockedURLs', { urls: [] });
await click('.pf-state--error .pf-btn');
check('retry recovers', await waitFor('.pf-row__title'));

const errors = page.consoleErrors().filter(entry => !entry.includes('ERR_BLOCKED_BY_CLIENT'));
check.eq('no console errors', errors.length, 0, errors.join(' | '));

await browser.close();
process.exit(check.done());
