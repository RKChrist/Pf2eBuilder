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
const api = process.argv[3] ?? 'http://localhost:5092';
const shots = process.env.SHOTS;
const pathbuilder = readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8');

const check = reporter();
const browser = await launch({ headless: process.env.HEADED !== '1' });

async function screen(width, height, mobile, isolated = false) {
  const page = await openPage(browser, { isolated });
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
  // Reported before it throws. Without this a page that never rendered exited 1 with no
  // FAIL line at all, which reads exactly like a check that ran and failed.
  check(`the page rendered ${selector}`, false, 'never appeared');
  throw new Error(`never saw ${selector}`);
};

// A truth about the page rather than the presence of a node, for the cases where a row is
// already there and what changed is what it says.
const until = async (page, expression, ms = 12000) => {
  const deadline = Date.now() + ms;
  while (Date.now() < deadline) {
    if (await page.eval(expression)) return true;
    await sleep(150);
  }
  return false;
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

// A mode is a page. The DM's tap both moves the table and navigates; a player's only navigates.
const clickCamp = async (page, activity) => {
  const hit = await page.eval(`(() => {
    const b = document.querySelector('[data-camp-act="${activity}"]');
    if (!b || b.disabled) return false;
    b.click();
    return true;
  })()`);
  if (!hit) throw new Error(`no camp button for ${activity}`);
  await sleep(900);
};
const goMode = async (page, mode) => {
  // By the mode rather than the label, because the active link carries a dot beside its text
  // and an exact-text match stopped finding it.
  const selector = `.shell__mode-link[data-mode="${mode}"]`;
  const went = await page.eval(`(() => {
    const link = document.querySelector(${JSON.stringify(selector)});
    if (!link) return false;
    link.click();
    return true;
  })()`);
  if (!went) throw new Error(`no mode link for ${mode}`);
  await sleep(900);
};

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

// The party's own feats and spells, joined to the records the ruleset holds.
await waitFor(dm, '[data-owned]');
const owned = await dm.eval(`(() => {
  const panel = document.querySelector('[data-owned]');
  panel.open = true;
  return {
    summary: panel.querySelector('.owned__count').textContent.trim(),
    kinds: [...panel.querySelectorAll('.pf-section__label')].map(h => h.textContent.trim()),
    openable: panel.querySelectorAll('a.owned__entry').length,
    unopenable: [...panel.querySelectorAll('.owned__entry--unknown .owned__entry-kind')].map(e => e.textContent.trim()),
    named: [...panel.querySelectorAll('.owned__entry-name')].map(e => e.textContent.trim()),
  };
})()`);
check('the party panel counts what each character has', /\d+ feats, \d+ spells/.test(owned.summary), owned.summary);
check('grouped the way a character sheet is', owned.kinds.includes('Class Feat') && owned.kinds.includes('Cantrip'),
  owned.kinds.join(', '));
// Three of the bard's names are pre-Remaster ones whose records are called something else now.
// The rename index closes them, so every name on this sheet opens something.
check('every one of them opens its rule', owned.openable === owned.named.length,
  `${owned.openable} of ${owned.named.length}`);
check('including the pre-Remaster ones', owned.named.includes('Inspire Competence'),
  owned.named.slice(0, 6).join(', '));
check('including a spell off the repertoire', owned.named.includes('Invisibility'), owned.named.slice(0, 8).join(', '));
await shot(dm, 'dm-08-reference');


// Exploration: what everyone is doing, and the one that changes how the fight starts.
await goMode(dm, 'Exploration');
await waitFor(dm, '[data-choose]');
check('exploration asks every character what they are doing', await dm.eval(
  `document.querySelectorAll('[data-choose]').length`).then(n => n === 1), 'one character on the roster, one chooser');
const activities = await dm.eval(
  `[...document.querySelectorAll('[data-choose] .pick')].map(b => b.textContent.trim())`);
check('and offers every printed activity plus doing nothing', activities.length === 10,
  activities.join(', '));
check('starting on nothing in particular', activities[0] === 'Nothing in particular', activities[0]);
check('with all of them on screen rather than inside a picker', await dm.eval(
  `document.querySelectorAll('[data-choose] select').length`) === 0);

await dm.eval(`[...document.querySelectorAll('[data-choose] .pick')]
  .find(b => b.textContent.trim() === 'Scout')?.click()`);
await sleep(1200);
check('choosing one says what it means at the table', await dm.eval(
  `document.querySelector('.choose__says')?.textContent.trim() ?? ''`).then(t => t.includes('initiative')),
  await dm.eval(`document.querySelector('.choose__says')?.textContent.trim() ?? 'nothing'`));
check('and the chosen one is the one marked', await dm.eval(
  `document.querySelector('[data-choose] .pick--on')?.textContent.trim() ?? ''`) === 'Scout');

// What each of the nine does, once under the chips. The chips carry it in a title attribute,
// which a pointer finds and a thumb never does.
check('and what every one of them does is readable without choosing it', await dm.eval(
  `document.querySelectorAll('.guide__entry').length`) === 9);
await shot(dm, 'dm-05-exploration');

// What this character can attempt, decided from their own ranks.
await waitFor(dm, '[data-trying]');
const trying = await dm.eval(`(() => {
  const panel = document.querySelector('[data-trying]');
  panel.open = true;
  panel.querySelector('.trying__rest').open = true;
  const read = selector => [...panel.querySelectorAll(selector)].map(e => ({
    name: e.querySelector('.try__name').textContent.trim(),
    skill: e.querySelector('.try__skill').textContent.trim(),
  }));
  return {
    summary: panel.querySelector('.trying__count').textContent.trim(),
    can: read('.try:not(.try--barred)'),
    cannot: read('.try--barred'),
  };
})()`);
check('the panel counts what this character can try',
  /can attempt \d+\s+of \d+/.test(trying.summary), trying.summary);
check('Decipher Writing is open to a bard trained in Society',
  trying.can.some(t => t.name === 'Decipher Writing'),
  trying.can.slice(0, 5).map(t => t.name).join(', '));
check('and Treat Wounds is not, because they are untrained in Medicine',
  trying.cannot.some(t => t.name === 'Treat Wounds' && t.skill.includes('trained in Medicine')),
  trying.cannot.slice(0, 5).map(t => t.name + ' (' + t.skill + ')').join(', '));
await shot(dm, 'dm-09-attempts');


// Camp: the ten-minute activities and the clock they add to. Its own page, reachable from every
// mode, because stopping to Treat Wounds is not a mode the table is in.
await clickText(dm, '.shell__party', 'Camp');
await waitFor(dm, '[data-camping]');
check('the camp panel starts at no time at all', await dm.eval(
  `document.querySelector('.clock__value')?.textContent.trim() ?? ''`).then(t => t === 'no time at all'),
  await dm.eval(`document.querySelector('.clock__value')?.textContent.trim() ?? 'missing'`));

await clickCamp(dm, 'treat-wounds');
const treated = await dm.eval(`({
  clock: document.querySelector('.clock__value')?.textContent.trim() ?? '',
  immune: document.querySelector('.camping__immune')?.textContent.trim() ?? '',
  off: document.querySelector('[data-camp-act="treat-wounds"]')?.disabled === true,
})`);
check('Treat Wounds costs ten minutes', treated.clock === '10 minutes', treated.clock);
check('and leaves an hour of immunity on the target', treated.immune.includes('1 hour'), treated.immune);
check('with the button off while it runs', treated.off);

await clickCamp(dm, 'refocus');
check('another activity moves the clock on', await dm.eval(
  `document.querySelector('.clock__value')?.textContent.trim() ?? ''`).then(t => t === '20 minutes'),
  await dm.eval(`document.querySelector('.clock__value')?.textContent.trim() ?? ''`));

await clickText(dm, '.camp__rest', 'Rest for the night');
const morning = await dm.eval(`({
  clock: document.querySelector('.clock__value')?.textContent.trim() ?? '',
  immune: document.querySelector('.camping__immune')?.textContent.trim() ?? 'none',
})`);
check('a night adds eight hours to the clock', morning.clock === '8 hours 20 minutes', morning.clock);
check('and nobody is still immune in the morning', morning.immune === 'none', morning.immune);
await shot(dm, 'dm-06-camp');

// Downtime: a day counter and one activity each, with the DC the task level comes to.
await goMode(dm, 'Downtime');
await waitFor(dm, '[data-choose]');
check('downtime starts on day one', await dm.eval(
  `document.querySelector('.clock__value')?.textContent.trim() ?? ''`).then(t => t === '1'),
  await dm.eval(`document.querySelector('.clock__value')?.textContent.trim() ?? 'missing'`));

await dm.eval(`[...document.querySelectorAll('[data-choose] .pick')]
  .find(b => b.textContent.trim() === 'Earn Income')?.click()`);
await sleep(1400);
const spending = await dm.eval(`({
  dc: document.querySelector('.spending__dc-value')?.textContent.trim() ?? '',
  level: document.querySelector('.spending__level .pf-stepper__value')?.textContent.trim() ?? '',
  says: document.querySelector('.choose__says')?.textContent.trim() ?? '',
})`);
check("a task level defaults to the character own level", spending.level === "7", spending.level);
check('and the DC for it comes down with the character', spending.dc === '23', spending.dc);
check('Earn Income says where its payment table is rather than inventing one',
  spending.says.includes('table is in the book'), spending.says);
await shot(dm, 'dm-07-downtime');

await dm.eval(`document.querySelector('.downtime__next')?.click()`);
await sleep(1200);
const tomorrow = await dm.eval(`({
  day: document.querySelector('.clock__value')?.textContent.trim() ?? '',
  chosen: document.querySelector('[data-choose] .pick--on')?.textContent.trim() ?? 'gone',
  dc: document.querySelector('.spending__dc-value')?.textContent.trim() ?? 'none',
})`);
check('the day turns', tomorrow.day === '2', tomorrow.day);
check('and clears what everybody chose', tomorrow.chosen === 'Nothing today' && tomorrow.dc === 'none',
  JSON.stringify(tomorrow));



// Into a fight.
await goMode(dm, 'Encounter');
await waitFor(dm, '.fight');
check('switching to Fight shows an initiative panel with nobody in it', await dm.eval(
  `!!document.querySelector('.fight') && document.querySelectorAll('.turn').length === 0`));
check('and says nobody is in it yet', await dm.eval(
  `document.body.innerText.includes('Nobody is in this fight yet')`));

await clickText(dm, '.fight__acts button', 'Add');
await waitFor(dm, '.adding .pf-section__label');
check('the roster is offered before the bestiary', await dm.eval(
  `(() => {
    const headings = [...document.querySelectorAll('.adding .pf-section__label')].map(h => h.textContent.trim());
    return headings.join(' | ') === 'From this campaign | A monster';
  })()`),
  await dm.eval(`[...document.querySelectorAll('.adding .pf-section__label')].map(h => h.textContent.trim()).join(' | ')`));
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
const roster = await dm.eval(`[...document.querySelectorAll('.turn__called')].map(e => e.textContent.trim())`);
check('both combatants are in the order', roster.length === 2, roster.join(', '));
check('the DM sees the monster hit points', await dm.eval(
  `[...document.querySelectorAll('.turn .hits__count')].some(e => /\\d+\\/\\d+/.test(e.textContent))`));

// The gap the owner reported. A row used to be a name and an initiative, so a GM asking
// whether a 24 hits Gnibbo had to leave the fight for the party page to find out.
const defended = await dm.eval(`[...document.querySelectorAll('.turn')].map(turn => ({
  who: turn.querySelector('.turn__called').textContent.trim(),
  stats: [...turn.querySelectorAll('.num[data-stat], .defence[data-defence]')]
    .map(d => d.dataset.stat ?? d.dataset.defence),
}))`);
check('every combatant carries the five a fight rolls against',
  defended.length === 2 && defended.every(row =>
    ['AC', 'Fort', 'Ref', 'Will', 'Perc'].every(s => row.stats.includes(s))),
  JSON.stringify(defended));

const written = await dm.eval(`(() => {
  const read = (s) => {
    const cell = document.querySelector('.num[data-stat="' + s + '"], .defence[data-defence="' + s + '"]');
    return cell?.querySelector('.num__value, .defence__value')?.textContent.trim() ?? '';
  };
  return { fort: read('Fort'), ac: read('AC') };
})()`);
check('a save is written as a roll and an armour class as a difficulty',
  /^[+-]/.test(written.fort) && /^[0-9]/.test(written.ac), JSON.stringify(written));

// A GM could apply a condition to a character from the party page and to nobody at all from
// the fight, which is the one screen they are on while conditions are being applied.
await dm.eval(`[...document.querySelectorAll('.turn')]
  .find(t => t.querySelector('.turn__called').textContent.includes('Gnibbo'))
  ?.querySelector('.chip--add')?.click()`);
await waitFor(dm, '.condition');
await dm.eval(`(() => {
  const row = [...document.querySelectorAll('.condition')]
    .find(c => c.dataset.condition === 'clumsy');
  row?.querySelector('button:last-of-type')?.click();
})()`);
await sleep(1200);
await dm.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(700);
check('a condition applied from the fight lands on the row it was applied from',
  await until(dm, `[...document.querySelectorAll('.turn')]
    .find(t => t.querySelector('.turn__called').textContent.includes('Gnibbo'))
    ?.textContent.toLowerCase().includes('clumsy')`));

// Its own row is where it comes off again, in one tap, because Undo is a button away.
await dm.eval(`[...document.querySelectorAll('.turn')]
  .find(t => t.querySelector('.turn__called').textContent.includes('Gnibbo'))
  ?.querySelector('.chip:not(.chip--add)')?.click()`);
check('and comes off from the same row',
  await until(dm, `![...document.querySelectorAll('.turn')]
    .find(t => t.querySelector('.turn__called').textContent.includes('Gnibbo'))
    ?.textContent.toLowerCase().includes('clumsy')`));

await clickText(dm, '.fight__acts button', 'Roll');
await sleep(1000);
const rolled = await dm.eval(`(() => ({
  initiatives: [...document.querySelectorAll('.turn__initiative')].map(e => Number(e.value ?? e.textContent.trim())),
  current: document.querySelectorAll('.turn--now').length,
  round: document.querySelector('.shell__round')?.textContent.trim() ?? null,
}))()`);
check('rolling gives everyone an initiative', rolled.initiatives.every(n => n > 0), JSON.stringify(rolled.initiatives));
check('in descending order', rolled.initiatives.every((n, i, a) => i === 0 || a[i - 1] >= n), JSON.stringify(rolled.initiatives));
check.eq('exactly one combatant is on turn', rolled.current, 1);
check('and the shell says which round it is', rolled.round === 'Round 1', String(rolled.round));
await shot(dm, 'dm-03-initiative');

const first = await dm.eval(`document.querySelector('.turn--now .turn__called').textContent.trim()`);
await clickText(dm, '.fight__acts button', 'Next turn');
await sleep(900);
const second = await dm.eval(`document.querySelector('.turn--now .turn__called').textContent.trim()`);
check('next turn moves the marker', first !== second, `${first} -> ${second}`);

await clickText(dm, '.fight__acts button', 'Undo');
await sleep(900);
check('undo puts the turn back', await dm.eval(
  `document.querySelector('.turn--now .turn__called').textContent.trim()`) === first, first);

// Editing a character, which is the other half of "campaign" and not "party tracker". The cards
// are on the party page, which every mode links back to.
await clickText(dm, '.shell__party', 'Party');
await waitFor(dm, '.character');
await clickText(dm, '.acts button', 'Edit');
await waitFor(dm, '.editor');
const beforeAc = await dm.eval(
  `[...document.querySelectorAll('.stat')].find(e => e.getAttribute('aria-label')?.startsWith('Armor Class')).querySelector('.stat__value').textContent.trim()`);
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
  `[...document.querySelectorAll('.stat')].find(e => e.getAttribute('aria-label')?.startsWith('Armor Class')).querySelector('.stat__value').textContent.trim()`);
check('saving an edit changes the number it feeds', Number(afterAc) === Number(beforeAc) + 1, `${beforeAc} -> ${afterAc}`);
check('and the editor closed', !(await dm.eval(`!!document.querySelector('.editor')`)));

// A player: same campaign, no key, a second tab.
// Isolated, because a player is a different device. Sharing the GM's storage would hand them
// the DM key and every check below would be testing one browser against itself.
const player = await screen(390, 844, true, true);
await player.goto(`${client}campaign`);
await waitFor(player, '.pf-input');
await type(player, '.pf-input', code);
await sleep(400);
await clickText(player, 'button', 'Join');
await waitFor(player, '.character');
check('a player joins with the code alone and sees the party', await player.eval(
  `[...document.querySelectorAll('.character__name')].map(e => e.textContent.trim())`)
  .then(names => names.includes('Gnibbo')), 'the roster reached a browser that only had the code');

// The strip is navigation for everybody, so a player gets the links: they can look at the camp
// page while the party is still walking. What a player does not get is the table moving when
// they tap one.
check('a player can navigate the modes too', await player.eval(
  `document.querySelectorAll('.shell__mode-link').length`).then(n => n === 3),
  await player.eval(`document.querySelectorAll('.shell__mode-link').length`));
check('and is not marked as the DM', !(await player.eval(`!!document.querySelector('.shell__dm')`)));

const startedIn = await dm.eval(`document.querySelector('.shell__mode-link .shell__here')
  ?.closest('.shell__mode-link')?.dataset.mode ?? ''`);
await goMode(player, 'Downtime');
await sleep(1200);
const afterPlayerTap = await dm.eval(`document.querySelector('.shell__mode-link .shell__here')
  ?.closest('.shell__mode-link')?.dataset.mode ?? ''`);
check('a player tapping a mode does not move the table', afterPlayerTap === startedIn,
  `${startedIn} -> ${afterPlayerTap}`);
await shot(player, 'player-01-exploration');

await goMode(dm, 'Encounter');
await sleep(1500);

// The dot marks the mode the table is in, and it is the DM's choice that puts it there. The
// player is on Downtime by their own tap and still sees where the table went.
const marked = await player.eval(`document.querySelector('.shell__mode-link .shell__here')
  ?.closest('.shell__mode-link')?.dataset.mode ?? null`);
check('the mode the DM chose reaches the player', marked === 'Encounter', String(marked));

await goMode(player, 'Encounter');
await sleep(1200);
const seen = await player.eval(`(() => {
  const rows = [...document.querySelectorAll('.turn')];
  return {
    names: rows.map(r => r.querySelector('.turn__called').textContent.trim()),
    withNumbers: rows.filter(r => r.querySelector('.hits__count'))
      .map(r => r.querySelector('.turn__called').textContent.trim()),
    controls: document.querySelectorAll('.fight__acts button').length,
    initiativeFields: document.querySelectorAll('.turn__initiative--typed').length,
    removals: document.querySelectorAll('.turn__out').length,
  };
})()`);
check('the player is looking at a fight at all', seen.names.length > 0, seen.names.join(', '));
check('the player is not offered the encounter controls', seen.controls === 0, String(seen.controls));
check('nor the initiative fields', seen.initiativeFields === 0, String(seen.initiativeFields));
check('nor a way to take anybody out of the fight', seen.removals === 0, String(seen.removals));

// The party's own numbers are on the party card and always were, so the order showing them is
// not a leak. A monster's are, at any reveal state, and no row that is not the party has any.
check('a player reads the party numbers and nobody else' + String.fromCharCode(39) + 's',
  seen.withNumbers.length > 0 && seen.withNumbers.every(name => name === 'Gnibbo'),
  seen.withNumbers.join(' | '));
check('an unrevealed monster is not on their screen either', !seen.names.includes('Ogre Warrior'),
  seen.names.join(', '));
await shot(player, 'player-02-fight');

// A condition on a monster is the DM's to put on and the DM's to lift. The apply side has
// refused a player from the beginning. The remove side asked nobody, so a player could take
// frightened off the ogre on the turn it mattered, and no screen said who had.
const ogreRow = `[...document.querySelectorAll('.turn')]
  .find(t => t.querySelector('.turn__called')?.textContent.includes('Ogre'))`;

await dm.eval(`${ogreRow}?.querySelector('.reveal .pf-switch__input')?.click()`);
await sleep(1000);
await dm.eval(`${ogreRow}?.querySelector('.chip--add')?.click()`);
await waitFor(dm, '.condition');
await dm.eval(`[...document.querySelectorAll('.condition')]
  .find(c => c.dataset.condition === 'frightened')
  ?.querySelector('button:last-of-type')?.click()`);
await sleep(1200);
await dm.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(800);

check('revealing a monster puts it on the player screen',
  await until(player, `${ogreRow} !== undefined`, 15000));
check('with the condition the DM put on it, because that is how a player picks a turn',
  await until(player, `${ogreRow}?.textContent.toLowerCase().includes('frightened')`, 15000));

const lifting = await player.eval(`(() => {
  const row = ${ogreRow};
  const chip = row?.querySelector('[data-effect]');
  return {
    tag: chip?.tagName ?? 'none',
    id: chip?.dataset.effect ?? '',
    removals: row?.querySelectorAll('button[data-effect]').length ?? -1,
    adds: row?.querySelectorAll('.chip--add').length ?? -1,
  };
})()`);
check('the player is not offered a control that takes it off',
  lifting.tag === 'SPAN' && lifting.removals === 0, JSON.stringify(lifting));
check('nor one that puts another on', lifting.adds === 0, String(lifting.adds));

// Everything above is the path a table takes, so it has to be quiet. Counted here rather than
// at the end, because the probe below is refused on purpose and a 403 is the right answer to it.
const errors = [...dm.consoleErrors(), ...player.consoleErrors()]
  .filter(e => !e.includes('ERR_BLOCKED_BY_CLIENT'));
check('no console errors', errors.length === 0, errors.join(' | ').slice(0, 300));

// The markup is the courtesy. This is the rule.
const refused = await player.eval(`(async () => {
  const answer = await fetch(
    ${JSON.stringify(api)} + '/campaigns/' + ${JSON.stringify(code)} + '/effects/' + '${lifting.id}',
    {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ effect: null, targets: [] }),
    });
  return answer.status;
})()`);
check.eq('and a request that asks anyway is refused', refused, 403);
check('so it is still on the monster',
  await until(player, `${ogreRow}?.textContent.toLowerCase().includes('frightened')`, 5000));

await browser.close();
process.exit(check.done());
