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
  await waitFor('.categories .category');
  await click('.categories .category');
  await waitFor('.rule .name');
}

await page.goto(client);
await waitFor('.pf-bottomnav__item');

check.eq('six navigation items', await count('.pf-bottomnav__item'), 6);
check('the group opens its category list', await waitFor('.categories .category'));
await waitFor('.category .size');
check('the board says what the group is for', (await text('.top .blurb'))?.length > 20, await text('.top .blurb'));
check('the board counts every category', await page.eval(`[...document.querySelectorAll('.categories .category')]
  .every(card => /^\\d{1,3}(,\\d{3})*$/.test(card.querySelector('.size')?.textContent.trim() ?? ''))`));
check('a category card explains itself', await count('.category .note') > 0);
check('the board groups categories under headings', await count('.section-title') > 1);

const topSearch = '[data-site-search]';
const typeTop = (typed) => page.eval(`(() => {
  const field = document.querySelector(${JSON.stringify(topSearch)});
  field.focus();
  field.value = ${JSON.stringify(typed)};
  field.dispatchEvent(new Event('input', { bubbles: true }));
  return true;
})()`);
const counted = () => page.eval(
  `performance.getEntriesByType('resource').filter(e => e.name.includes('/rules/counts?')).length`);

check('the search field is at the top of the screen', await page.eval(`(() => {
  const box = document.querySelector(${JSON.stringify(topSearch)}).getBoundingClientRect();
  return box.top >= 0 && box.top < 40;
})()`));

const topBefore = { searched: (await searches()).length, counted: await counted() };
for (const typed of ['s', 'sh', 'shi', 'shie', 'shiel', 'shield']) {
  await typeTop(typed);
  await sleep(50);
}
check('typing in the top search shows results underneath it', await waitFor('.site-search .option'));
await sleep(300);
const topAfter = await searches();
check.eq('six keystrokes at the top make one search', topAfter.length - topBefore.searched, 1);
check.eq('and one count', (await counted()) - topBefore.counted, 1);
check('the top search asked for the last keystroke', topAfter.at(-1).includes('Name=shield'), topAfter.at(-1));
check('the dropdown opens below the field, not over it', await page.eval(`(() => {
  const field = document.querySelector('.site-search .pf-search').getBoundingClientRect();
  const panel = document.querySelector('.site-search .panel').getBoundingClientRect();
  return panel.top >= field.bottom - 1 && panel.height > 100;
})()`));
check.eq('the exact name is the best match', await text('.site-search .option .name'), 'Shield');
check('each match says what kind of record it is', (await text('.site-search .option .kind'))?.length > 0);
check('matches are counted by category', await page.eval(
  `[...document.querySelectorAll('.site-search .chip')].some(chip => /\\d/.test(chip.textContent))`));

await page.key('Escape', 'Escape', 27);
await sleep(200);
check.eq('Escape closes the dropdown', await count('.site-search .panel'), 0);
check.eq('and keeps what was typed', await page.eval(`document.querySelector(${JSON.stringify(topSearch)}).value`), 'shield');

await page.key('Enter', 'Enter', 13);
await sleep(300);
check('Enter goes to the full results', (await page.eval('location.pathname')) === '/search'
  && (await page.eval('location.search')).includes('q=shield'), await page.eval('location.href'));
check('the full results are grouped by kind', await waitFor('.scopes .scope') && await count('.scopes .scope') > 2);
check('the full results list records', await waitFor('.rule'));
check('the full results label each record', (await text('.rule .kind'))?.length > 0);

await typeTop('longsword');
await waitFor('.site-search .option');
await page.eval(`document.activeElement.blur()`);
await sleep(100);
await page.key('/', 'Slash', 191);
check('/ focuses the search on a pointer device', await page.eval(
  `document.activeElement === document.querySelector(${JSON.stringify(topSearch)})`));
await typeTop('longsword');
await waitFor('.site-search .option');
await sleep(600);
check.eq('on the results page the dropdown stays shut', await count('.site-search .panel'), 0);
await page.goto(client);
await waitFor('.pf-bottomnav__item');
await typeTop('longsword');
await waitFor('.site-search .option');
await sleep(300);
await page.key('ArrowDown', 'ArrowDown', 40);
await sleep(100);
check('ArrowDown marks the first match', await count('.site-search .option.active') === 1);
await page.key('Enter', 'Enter', 13);
check('Enter on a match opens that record', await waitFor('.pf-sheet__panel'));
await sleep(500);
check.eq('the opened record is the match', await text('.pf-sheet__title'), 'Longsword');
await page.eval('history.back()');
await sleep(600);
await typeTop('');
await sleep(300);

await openFeats();
check('a category lists its records', await count('.rule') > 1);
check('the list says how many records the category holds',
  /^\d{1,3}(,\d{3})* feats$/.test(await text('.top .total')), await text('.top .total'));
check('list rows show a glance line', await count('.rule .glance') > 0);
check('a glance line reads as facts', await page.eval(
  `[...document.querySelectorAll('.rule .glance')].some(line => line.textContent.includes('Prerequisites: '))`));
check.eq('a common record wears no rarity badge', await count('.rule .pf-rarity--common'), 0);
check('a row shows at most four traits', await page.eval(
  `[...document.querySelectorAll('.rule .marks')].every(marks => marks.querySelectorAll('.pf-trait').length <= 4)`));
check('filters are folded away on a phone', await page.eval(
  `getComputedStyle(document.querySelector('.refine')).display === 'none'`));
await click('.refine-toggle');
await sleep(200);
check('the Filters toggle opens them', await page.eval(
  `getComputedStyle(document.querySelector('.refine')).display !== 'none'
    && document.querySelector('.refine-toggle').getAttribute('aria-expanded') === 'true'`));
check('the level filter is one dual-thumb range', await count('.pf-slider--range') === 1);

const before = (await searches()).length;
for (const typed of ['s', 'sh', 'shi', 'shie', 'shiel', 'shield']) {
  await page.eval(`(() => {
    const field = document.querySelector('.filters .pf-search__input');
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
  (await text('.rule .name')).toLowerCase().includes('shield'), await text('.rule .name'));

await page.eval(`(() => {
  const field = document.querySelector('.filters .pf-search__input');
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
  [...document.querySelectorAll('.rule .level')].every(meta => {
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

const rowName = await text('.rule .name');
await click('.rule .open');
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
check('Escape leaves the list behind it', await count('.rule') > 1);

await click('.rule .open');
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
check('retry recovers', await waitFor('.rule .name'));

// The three bands of design/003. Widths are measured, not assumed: a screenshot of a wide layout
// has twice fooled a reader of this repo.
const columns = () => page.eval(`(() => {
  const fullest = [...document.querySelectorAll('.categories')]
    .sort((a, b) => b.children.length - a.children.length)[0];
  const tops = [...fullest.querySelectorAll('.category')]
    .slice(0, 6).map(row => Math.round(row.getBoundingClientRect().top));
  return tops.filter(top => top === tops[0]).length;
})()`);

const frame = () => page.eval(`(() => {
  const nav = document.querySelector('.pf-bottomnav').getBoundingClientRect();
  const row = document.querySelector('.rule')?.getBoundingClientRect();
  const panel = document.querySelector('.pf-sheet__panel')?.getBoundingClientRect();
  const scrim = document.querySelector('.pf-sheet__scrim');
  return {
    navWidth: Math.round(nav.width),
    navHeight: Math.round(nav.height),
    rowRight: row ? Math.round(row.right) : null,
    panelLeft: panel ? Math.round(panel.left) : null,
    panelRight: panel ? Math.round(panel.right) : null,
    scrimShown: scrim ? getComputedStyle(scrim).display !== 'none' : false,
    viewport: document.documentElement.clientWidth,
    sideways: document.documentElement.scrollWidth > document.documentElement.clientWidth,
  };
})()`);

await page.goto(client);
await waitFor('.pf-bottomnav__item');
check.eq('one column of categories on a phone', await columns(), 1);

await page.viewport(834, 900, false);
await sleep(400);
check.eq('two columns of categories on a tablet', await columns(), 2);
check('the bar is still a bar on a tablet', (await frame()).navWidth > 600);

await page.viewport(1440, 900, false);
await sleep(400);
check.eq('three columns of categories on a laptop', await columns(), 3);
const rail = await frame();
check('the navigation stands up as a rail', rail.navWidth <= 80 && rail.navHeight > 600,
  `${rail.navWidth}x${rail.navHeight}`);

await openFeats();
await click('.rule .open');
await waitFor('.pf-sheet__panel');
await sleep(600);
const docked = await frame();
check('the record docks at the side', docked.panelRight === docked.viewport && docked.panelLeft > docked.viewport / 2,
  `${docked.panelLeft}..${docked.panelRight} of ${docked.viewport}`);
check('the list is still beside it, not under it', docked.rowRight <= docked.panelLeft,
  `row ends ${docked.rowRight}, panel starts ${docked.panelLeft}`);
check('nothing is dimmed behind a docked record', !docked.scrimShown);
check('no sideways scroll on a laptop', !docked.sideways);
check('prose keeps its measure', await page.eval(`(() => {
  const body = document.querySelector('.pf-sheet__body');
  return Math.round(body.getBoundingClientRect().width) <= 560;
})()`));

await page.key('Escape', 'Escape', 27);
await sleep(500);
check.eq('Escape closes a docked record too', await count('.pf-sheet__panel'), 0);
await page.viewport(390, 844, true);
await sleep(300);

const errors = page.consoleErrors().filter(entry => !entry.includes('ERR_BLOCKED_BY_CLIENT'));
check.eq('no console errors', errors.length, 0, errors.join(' | '));

await browser.close();
process.exit(check.done());
