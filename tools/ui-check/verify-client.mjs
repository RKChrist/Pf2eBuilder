// Drives the running client in a real Chrome and asserts the behaviour the screens promise:
// the header search and its results page, the explained browse screens, trait sheets, the
// record sheet and the records it names, the search debounce, the deep link, back and Escape
// closing a sheet, paging, the level range bounded by the category, the trait facet, a list's own
// address surviving refresh and back, badges for yes-or-no fields, links only to real records,
// heightening in words, a missing record told apart from a failed one, the desktop filter column,
// and a failure with a retry that recovers. Requests are blocked through the protocol rather than
// by stopping the API, so the failure arm is exercised without a second terminal.
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

// The resource timing buffer holds 250 entries and then silently stops recording. This run makes
// more requests than that, so without this the search assertions late in the file read a stale
// last entry and report that a working filter is broken. That happened to the level slider.
await page.send('Page.addScriptToEvaluateOnNewDocument', {
  source: "performance.setResourceTimingBufferSize(5000);",
});
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

check.eq('no control sits inside another', await count('button button, button a, a button, a a'), 0);
const traitName = await text('.rule .trait-link');
await click('.rule .trait-link');
check('a trait chip opens the trait sheet', await waitFor('.trait-sheet .pf-sheet__panel'));
await sleep(300);
check('the trait is in the address bar', decodeURIComponent(await page.eval('location.search')).toLowerCase()
  .includes(`trait=${traitName.toLowerCase()}`), await page.eval('location.search'));
check.eq('the chip does not also open its row', await count('.pf-sheet:not(.trait-sheet) .pf-sheet__panel'), 0);
check.eq('the trait sheet is titled with the trait', (await text('.trait-sheet .pf-sheet__title')).toLowerCase(), traitName.toLowerCase());
check('the trait sheet explains it or says it cannot yet',
  (await text('.trait-sheet .gloss'))?.length > 10, await text('.trait-sheet .gloss'));
check('the trait sheet says where it is found', await waitFor('.trait-sheet .place'));
check('each place is counted', await page.eval(
  `[...document.querySelectorAll('.trait-sheet .place-count')].every(c => /^\\d{1,3}(,\\d{3})*$/.test(c.textContent.trim()))`));
check('the sheet links the full text', (await page.eval(
  `document.querySelector('.trait-sheet a.archives')?.href ?? ''`)).startsWith('https://2e.aonprd.com/'));
await page.eval('history.back()');
await sleep(600);
check.eq('back closes the trait sheet', await count('.trait-sheet .pf-sheet__panel'), 0);
check('back leaves the list where it was', await count('.rule') > 1);

await click('.rule .trait-link');
await waitFor('.trait-sheet .place');
await sleep(300);
const place = await text('.trait-sheet .place-label');
await click('.trait-sheet .place');
await sleep(1200);
check.eq('a place opens that category', await text('.top .heading'), place);
check.eq('filtered by the trait', (await text('.active-trait'))?.split('\n')[0].trim().toLowerCase(), traitName.toLowerCase());
check('and the list is only records with it', (await searches()).at(-1).toLowerCase().includes(`trait=${encodeURIComponent(traitName).toLowerCase()}`),
  (await searches()).at(-1));
await openFeats();
await click('.refine-toggle');
await sleep(200);
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
check('back stays in the app', (await page.eval('location.pathname')) === '/browse/feat',
  await page.eval('location.pathname'));

await page.goto(deepLink);
check('a deep link restores the record', await waitFor('.pf-sheet__panel'));
await sleep(600);
check.eq('the deep-linked record is the right one', await text('.pf-sheet__title'), rowName);
check('the sheet names where the rule is printed',
  (await text('.pf-sheet__body')).includes('Archives of Nethys'));

await typeTop('Reactive Shield');
await waitFor('.site-search .option');
await sleep(300);
await page.key('ArrowDown', 'ArrowDown', 40);
await page.key('Enter', 'Enter', 13);
await waitFor('.rule-sheet .stats');
await sleep(500);
check.eq('the record is headed with its kind and level', await text('.rule-sheet .kind'), 'Feat 1');
check('its traits can be opened from the sheet', await count('.rule-sheet .trait-link') > 0);
check('its key facts lead as a stat block', (await text('.rule-sheet .stats'))?.includes('Reaction'),
  await text('.rule-sheet .stats'));
check('a trigger or requirement is marked as a gate', await page.eval(
  `[...document.querySelectorAll('.rule-sheet .stat.gate dt')].map(dt => dt.textContent.trim()).join()`) === 'Trigger,Requirements',
  await page.eval(`[...document.querySelectorAll('.rule-sheet .stat.gate dt')].map(dt => dt.textContent.trim()).join()`));
check('the source is stated', await page.eval(
  `[...document.querySelectorAll('.rule-sheet dt')].some(dt => dt.textContent.trim() === 'Source')`));
check.eq('the full text is one clear button', await text('.rule-sheet a.pf-btn.archives'), 'Full rules text on Archives of Nethys');
const referenced = await text('.rule-sheet .ref');
await click('.rule-sheet .ref');
await sleep(1500);
check.eq('a named record opens from the sheet', await text('.pf-sheet__title'), referenced);
check('and it is in the address bar', (await page.eval('location.search')).includes('rule=archetype-'),
  await page.eval('location.search'));
await page.eval('history.back()');
await sleep(800);
check.eq('back returns to the record that named it', await text('.pf-sheet__title'), 'Reactive Shield');
await click('.rule-sheet .trait-link');
check('a trait opens over the record', await waitFor('.trait-sheet .pf-sheet__panel'));
await page.eval('history.back()');
await sleep(800);
check.eq('and back closes it onto the record', await text('.rule-sheet .pf-sheet__title'), 'Reactive Shield');
await page.eval('history.back()');
await sleep(600);
await typeTop('');
await sleep(300);

await page.goto(`${client}conditions`);
check('the conditions screen loads', await waitFor('.pf-card'));
check('a condition states its modifiers', await count('.pf-mod') > 0);
check('a penalty is drawn as one', await page.eval(
  `[...document.querySelectorAll('.pf-mod')].some(m => m.classList.contains('pf-mod--penalty'))`));
check.eq('conditions wear the same heading as every list', await text('.top .heading'), 'Conditions');
check('conditions say what they are for', (await text('.top .blurb'))?.length > 20, await text('.top .blurb'));
check('conditions lead back to their group', (await text('.top .back'))?.includes('At the table'), await text('.top .back'));

const requested = (part) => page.eval(
  `performance.getEntriesByType('resource').filter(e => e.name.includes(${JSON.stringify(part)})).map(e => decodeURIComponent(e.name))`);
const openSheet = async (id) => {
  await page.goto(`${client}conditions?rule=${id}`);
  await waitFor('.rule-sheet .pf-sheet__body > *:not(.pending)');
  await sleep(400);
};
const rows = () => page.eval(`[...document.querySelectorAll('.rule-sheet dt')].map(dt => [dt.textContent.trim(), dt.nextElementSibling?.textContent.trim().replace(/\\s+/g, ' ')])`);

await openSheet('background-51');
check.eq('a yes-or-no field that is yes is a badge', await text('.rule-sheet .flag'), 'General background');
check('and never the word true', !(await text('.pf-sheet__body')).match(/\btrue\b/i));
await openSheet('background-491');
check.eq('a yes-or-no field that is no shows nothing', await count('.rule-sheet .flag'), 0);
check('and never the word false', !(await text('.pf-sheet__body')).match(/\bfalse\b/i));
const lore = 'Academia Lore or a Lore skill associated with your school';
check('a phrase that names no record stays words', (await text('.pf-sheet__body')).includes(lore)
  && !(await page.eval(`[...document.querySelectorAll('.rule-sheet .ref')].some(a => a.textContent.includes('Academia'))`)));
check('a value that names a record is a link to it', await page.eval(
  `[...document.querySelectorAll('.rule-sheet a.ref')].some(a => a.textContent.trim() === 'Arcana' && a.href.includes('rule=skill-'))`));
check.eq('the sheet asks nothing to decide its links', (await requested('/rules?')).length, 0);

await openSheet('ancestry-59');
const speed = (await rows()).find(([label]) => label === 'Speed');
check.eq('a speed reads in feet', speed?.[1], '20 feet');
check('and never as JSON', !(await text('.pf-sheet__body')).includes('max'));

await openSheet('spell-1530');
check.eq('a spell is headed with its rank', await text('.rule-sheet .kind'), 'Spell, rank 3');
check.eq('a heightening step counts from the next rank', (await rows()).find(([label]) => label === 'Heightened')?.[1], 'Every rank from 4th');
await openSheet('spell-38');
check.eq('listed heightenings name their ranks', (await rows()).find(([label]) => label === 'Heightened')?.[1], 'At 4th');

await page.goto(`${client}browse/feat?rule=no-such-record`);
check('an unknown record says so', await waitFor('.rule-sheet .pf-state'));
await sleep(300);
check.eq('it is titled as missing', await text('.pf-sheet__title'), 'No such record');
check('it offers Close', await page.eval(`[...document.querySelectorAll('.rule-sheet .pf-state .pf-btn')].map(b => b.textContent.trim()).join() === 'Close'`));
check.eq('and not Try again', await count('.rule-sheet .pf-state--error'), 0);
await click('.rule-sheet .pf-state .pf-btn');
await sleep(600);
check.eq('Close shuts it', await count('.pf-sheet__panel'), 0);
check('and stays on the list', (await page.eval('location.href')).endsWith('/browse/feat'), await page.eval('location.href'));

await page.send('Network.setBlockedURLs', { urls: [`${api}/rules/feat-*`] });
await page.goto(`${client}browse/feat?rule=feat-4776`);
check('a record the service cannot reach fails', await waitFor('.rule-sheet .pf-state--error'));
check('and offers Try again', (await text('.rule-sheet .pf-state--error .pf-btn')) === 'Try again');
await page.send('Network.setBlockedURLs', { urls: [] });
await click('.rule-sheet .pf-state--error .pf-btn');
check('which recovers', await waitFor('.rule-sheet .stats'));

await page.goto(`${client}browse/spell`);
await waitFor('.rule .name');
check('a spell row says rank, not level', /^Rank \d+$/.test(await text('.rule .level')), await text('.rule .level'));
await click('.refine-toggle');
await sleep(200);
check.eq('the spell filter is by rank', await text('.pf-slider__label'), 'Rank');
check.eq('bounded by the ranks spells have', await page.eval(
  `[...document.querySelectorAll('.pf-slider__end')].map(e => e.textContent.trim()).join('..')`), '1..10');
check.eq('with no row of tick marks', await count('.pf-slider__tick'), 0);

await page.goto(`${client}browse/action`);
await waitFor('.rule .name');
await click('.refine-toggle');
await sleep(300);
check.eq('a category without levels has no level filter', await count('.pf-slider'), 0);

await page.goto(`${client}browse/feat`);
await waitFor('.trait-chip .trait-count');
const facet = await requested('/rules/traits?');
check('the trait filter is counted across the whole category', facet.some(url => url.includes('Category=feat')), facet.join());
const chips = await page.eval(`[...document.querySelectorAll('.trait-chip')].map(c => ({
  name: c.firstChild.textContent.trim(), count: Number(c.querySelector('.trait-count').textContent.replace(/,/g, '')) }))`);
check('a handful of traits, not all of them', chips.length > 3 && chips.length <= 8, chips.length);
check('most common first', chips.every((chip, i) => i === 0 || chips[i - 1].count >= chip.count), JSON.stringify(chips));
check('counted past one page of rows', chips[0].count > 50, chips[0].count);
await page.eval(`(() => {
  const field = document.querySelector('.trait-find .pf-search__input');
  field.value = 'Fighte';
  field.dispatchEvent(new Event('input', { bubbles: true }));
})()`);
await sleep(300);
check('any other trait is found by typing', await page.eval(
  `[...document.querySelectorAll('.trait-chip')].some(c => c.firstChild.textContent.trim() === 'Fighter')`));
await page.eval(`[...document.querySelectorAll('.trait-chip')].find(c => c.firstChild.textContent.trim() === 'Fighter').click()`);
await sleep(1200);
check('picking it filters the list', (await searches()).at(-1).includes('Trait=Fighter'), (await searches()).at(-1));
check('and puts it in the address', (await page.eval('location.search')).includes('with=Fighter'), await page.eval('location.search'));
check.eq('the chosen trait leads, pressed', await page.eval(
  `document.querySelector('.trait-chip')?.getAttribute('aria-pressed')`), 'true');
await page.eval(`(() => {
  const field = document.querySelector('.filters .pf-search__input');
  field.value = 'shield';
  field.dispatchEvent(new Event('input', { bubbles: true }));
})()`);
await sleep(1500);
check('typing a name puts it in the address', (await page.eval('location.search')).includes('q=shield'), await page.eval('location.search'));
const facetAfter = await requested('/rules/traits?');
check('the trait counts follow the name filter', facetAfter.at(-1)?.includes('Name=shield'), facetAfter.at(-1));

const listed = await page.eval(`[...document.querySelectorAll('.rule .name')].map(n => n.textContent.trim()).join('|')`);
const address = await page.eval('location.href');
await page.goto(address);
await waitFor('.rule .name');
await sleep(800);
check.eq('refresh keeps the category', await text('.top .heading'), 'Feats');
check.eq('refresh keeps the name filter', await page.eval(`document.querySelector('.filters .pf-search__input').value`), 'shield');
check('refresh keeps the trait filter', (await text('.active-trait'))?.startsWith('Fighter'), await text('.active-trait'));
check.eq('refresh lists the same records', await page.eval(`[...document.querySelectorAll('.rule .name')].map(n => n.textContent.trim()).join('|')`), listed);

await page.goto(`${client}?group=feats`);
await waitFor('.categories .category');
await click('.categories .category');
await waitFor('.rule .name');
await page.eval(`(() => {
  const field = document.querySelector('.filters .pf-search__input');
  field.value = 'rage';
  field.dispatchEvent(new Event('input', { bubbles: true }));
})()`);
await sleep(1200);
check.eq('a list has an address of its own', await page.eval('location.pathname'), '/browse/feat');
check.eq('its back link names its group', (await text('.top .back'))?.replace('←', '').trim(), 'Feats');
await page.eval('history.back()');
await sleep(800);
check.eq('back returns to the board', await page.eval('location.pathname + location.search'), '/?group=feats');
check('the board is showing', await count('.categories .category') > 0);

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

await page.viewport(1024, 800, false);
await sleep(400);
check.eq('three columns of categories on a small laptop', await columns(), 3);

await page.viewport(1440, 900, false);
await sleep(400);
check.eq('four columns of categories on a laptop', await columns(), 4);
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

await page.goto(`${client}browse/feat`);
await waitFor('.trait-chip');
await sleep(600);
const laptop = await page.eval(`(() => {
  const side = document.querySelector('.side').getBoundingClientRect();
  const list = document.querySelector('.results').getBoundingClientRect();
  return {
    sideRight: Math.round(side.right),
    listLeft: Math.round(list.left),
    listWidth: Math.round(list.width),
    refine: getComputedStyle(document.querySelector('.refine')).display,
    toggle: getComputedStyle(document.querySelector('.refine-toggle')).display,
  };
})()`);
check('the filters stand in a column beside the list', laptop.sideRight <= laptop.listLeft, JSON.stringify(laptop));
check('open, with no toggle to find them behind', laptop.refine !== 'none' && laptop.toggle === 'none', JSON.stringify(laptop));
check('the list spends the width beside them', laptop.listWidth > 900, laptop.listWidth);
check('a row reads name, level and glance on one line', await page.eval(`(() => {
  const row = [...document.querySelectorAll('.rule')].find(r => r.querySelector('.glance'));
  const top = el => Math.round(row.querySelector(el).getBoundingClientRect().top);
  return Math.abs(top('.name') - top('.level')) < 8 && Math.abs(top('.name') - top('.glance')) < 8;
})()`));
await page.eval('window.scrollTo(0, 3000)');
await sleep(400);
const held = await page.eval(`({
  side: Math.round(document.querySelector('.side').getBoundingClientRect().top),
  head: Math.round(document.querySelector('.site-head').getBoundingClientRect().bottom),
  scrolled: Math.round(window.scrollY),
})`);
check('the filters stay in view below the header while the list scrolls',
  held.scrolled > 1000 && Math.abs(held.side - held.head) <= 1, JSON.stringify(held));

await page.goto(`${client}search?q=shield`);
await waitFor('.rule .name');
await sleep(600);
check('search results stand beside their kinds too', await page.eval(`(() => {
  const scopes = document.querySelector('.scopes').getBoundingClientRect();
  const list = document.querySelector('.results').getBoundingClientRect();
  return scopes.right <= list.left && list.width > 900;
})()`));

await page.viewport(1920, 1080, false);
await page.goto(client);
await waitFor('.categories .category');
await sleep(400);
check.eq('five columns of categories on a wide screen', await columns(), 5);
await page.viewport(390, 844, true);
await sleep(300);

// Chrome logs the unknown record's 404 itself; that answer is the point of the check that asked.
const notFound = page.consoleErrors().filter(entry => entry.includes('status of 404'));
check.eq('the only 404 is the record asked for on purpose', notFound.length, 1);
const errors = page.consoleErrors().filter(entry => !entry.includes('ERR_BLOCKED_BY_CLIENT') && !entry.includes('status of 404'));
check('no console errors', errors.length === 0, errors.join(' | '));

await browser.close();
process.exit(check.done());
