// Walks the whole product: every route, every overlay sheet, every disclosure opened, at four
// widths in both themes. Reports what a screenshot cannot show.
//
//   node tools/ui-check/verify-walk.mjs            report only
//   SHOTS=<dir> node tools/ui-check/verify-walk.mjs   also write a png per page and width
//   node tools/ui-check/verify-walk.mjs --selftest  prove the detectors fire, then exit
//
// Exit code is the number of page-width-theme combinations with a finding.
//
// --selftest exists because a clean run is worth nothing unless the instruments work. It
// plants one known violation per detector on a real page and fails if any detector misses
// its own planted defect. Run it whenever you change what is measured.
import { launch, openPage, sleep } from './cdp.mjs';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';

const out = process.env.SHOTS ?? null;
const selftest = process.argv.includes('--selftest');
if (out) mkdirSync(out, { recursive: true });
const pathbuilder = readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8');

const b = await launch({ headless: true });
const p = await openPage(b);

const wait = async (sel, ms = 25000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    if (await p.eval(`!!document.querySelector(${JSON.stringify(sel)})`)) return true;
    await sleep(150);
  }
  return false;
};

const clickText = async (sel, text) => {
  const hit = await p.eval(`(() => {
    const el = [...document.querySelectorAll(${JSON.stringify(sel)})]
      .find(e => e.textContent.trim() === ${JSON.stringify(text)});
    if (el && !el.disabled) { el.click(); return true; }
    return false;
  })()`);
  await sleep(900);
  return hit;
};

const type = (sel, value) => p.eval(`(() => {
  const f = document.querySelector(${JSON.stringify(sel)});
  if (!f) return false;
  const proto = f.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(proto, 'value').set.call(f, ${JSON.stringify(value)});
  for (const t of ['input', 'change']) f.dispatchEvent(new Event(t, { bubbles: true }));
  return true;
})()`);

const go = async (route) => {
  await p.eval(`history.pushState({}, '', ${JSON.stringify(route)});
    window.dispatchEvent(new PopStateEvent('popstate'))`);
  await sleep(1400);
};

// Everything a person could unfold, unfolded, so a shut section never hides a broken one.
const openEverything = () => p.eval(`(() => {
  let n = 0;
  for (const d of document.querySelectorAll('details')) { if (!d.open) { d.open = true; n++; } }
  return n;
})()`);

const facts = () => p.eval(`(() => {
  const doc = document.documentElement;
  const vw = doc.clientWidth;
  // The accessible name the way a browser computes it, not just aria-label. A field labelled
  // by a <label for> has a perfectly good name, and the first version of this check called
  // every one of them nameless.
  const name = (el) => {
    const aria = el.getAttribute("aria-label");
    if (aria) return aria.trim();
    if (el.id) {
      const tied = document.querySelector("label[for=" + JSON.stringify(el.id) + "]");
      if (tied) return tied.textContent.replace(/\\s+/g, " ").trim();
    }
    const wrapping = el.closest("label");
    if (wrapping) return wrapping.textContent.replace(/\\s+/g, " ").trim();
    return (el.getAttribute("title") || el.textContent || "").replace(/\\s+/g, " ").trim();
  };

  // Drawn nowhere on purpose. A visually hidden control is reachable and named; it is not a
  // tap target that happens to be one pixel across.
  const hidden = (el) => {
    const style = getComputedStyle(el);
    return el.classList.contains("visually-hidden")
      || style.clipPath === "inset(50%)"
      || Number(style.opacity) === 0
      || el.closest(".visually-hidden") !== null;
  };
  const where = (el) => (el.className.baseVal ?? el.className ?? el.tagName).toString().slice(0, 44);

  const controls = [...document.querySelectorAll('button, a[href], input, select, textarea, summary')]
    .filter((e) => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0; });

  const small = controls
    .filter((e) => !hidden(e))
    .filter((e) => { const r = e.getBoundingClientRect(); return Math.min(r.width, r.height) < 40; })
    .map((e) => name(e).slice(0, 28) + ' ' + Math.round(e.getBoundingClientRect().width)
      + 'x' + Math.round(e.getBoundingClientRect().height));

  const nameless = controls
    .filter((e) => name(e).length === 0 && !e.hasAttribute('aria-labelledby') && e.type !== 'hidden')
    .map(where);

  // Inside a container that scrolls sideways on purpose, reaching past the viewport is the
  // design. Everywhere else it is content falling off the screen.
  const scroller = (el) => {
    for (let n = el.parentElement; n; n = n.parentElement) {
      if (/(auto|scroll)/.test(getComputedStyle(n).overflowX)) return true;
    }
    return false;
  };

  // html and body grow to contain whatever overflowed, so they are the symptom and never the
  // cause. Reporting them filled the list and pushed the actual culprit out of it, which is how
  // the self-test caught this detector missing a three-thousand-pixel div.
  const over = [...document.querySelectorAll('*')]
    .filter((e) => !['HTML', 'BODY'].includes(e.tagName))
    .filter((e) => e.getBoundingClientRect().right > vw + 1 && !scroller(e))
    .sort((a, b) => b.getBoundingClientRect().right - a.getBoundingClientRect().right)
    .map(where);

  const clipped = [...document.querySelectorAll('*')]
    .filter((e) => e.scrollWidth > e.clientWidth + 1
      && getComputedStyle(e).overflowX === 'hidden'
      && !hidden(e))
    .map(where);

  // A heading that jumps from h1 to h3 is a document nobody can navigate by heading.
  const levels = [...document.querySelectorAll('h1, h2, h3, h4')].map((h) => Number(h.tagName[1]));
  const jumps = levels.filter((l, i) => i > 0 && l - levels[i - 1] > 1);

  return {
    sideways: doc.scrollWidth > vw,
    over: [...new Set(over)].slice(0, 5),
    clipped: [...new Set(clipped)].slice(0, 5),
    small: [...new Set(small)].slice(0, 6),
    nameless: [...new Set(nameless)].slice(0, 5),
    headingJumps: jumps.length,
  };
})()`);

const seen = [];
const shoot = async (label, widths = [[390, 900, true, 'dark'], [768, 1024, false, 'light'], [1280, 1000, false, 'light'], [1920, 1080, false, 'dark']]) => {
  for (const [w, h, mobile, scheme] of widths) {
    await p.viewport(w, h, mobile);
    await p.send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: scheme }] });
    await sleep(600);
    const f = await facts();
    if (out) {
      const { data } = await p.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true });
      writeFileSync(`${out}/${label}-${w}.png`, Buffer.from(data, 'base64'));
    }
    const trouble = f.sideways || f.over.length || f.clipped.length || f.small.length
      || f.nameless.length || f.headingJumps;
    seen.push(`${trouble ? 'LOOK' : 'ok  '} ${(label + ' @' + w).padEnd(34)} ${trouble ? JSON.stringify(f) : ''}`);
  }
};

// ---- the rules browser, which is the front door ----------------------------------------

// Every detector, against a defect planted on purpose. A zero from a broken instrument and
// a zero from a clean product print the same.
if (selftest) {
  await p.viewport(390, 844, true);
  await p.goto('http://localhost:5173/');
  await wait('.pf-bottomnav__item');
  await p.eval(`(() => {
    const plant = (html) => document.body.insertAdjacentHTML('beforeend', html);
    plant('<div style="position:absolute;left:0;width:3000px;height:4px" class="ctl-wide"></div>');
    plant('<div class="ctl-clip" style="width:40px;overflow-x:hidden;white-space:nowrap">a very long line indeed</div>');
    plant('<button class="ctl-tiny" style="width:8px;height:8px;padding:0"></button>');
    plant('<h1>one</h1><h3>three</h3>');
    plant('<button class="visually-hidden ctl-hidden">hidden but named</button>');
  })()`);
  await sleep(300);
  const f = await facts();
  const caught = {
    sideways: f.sideways === true,
    overflow: f.over.some((c) => c.includes('ctl-wide')),
    clipped: f.clipped.some((c) => c.includes('ctl-clip')),
    tap: f.small.length > 0,
    unnamed: f.nameless.length > 0,
    headingJump: f.headingJumps > 0,
    hiddenExcluded: !f.small.some((c) => c.includes('hidden but named')),
  };
  const missed = Object.entries(caught).filter(([, ok]) => !ok).map(([name]) => name);
  console.log(JSON.stringify(caught, null, 1));
  console.log(missed.length
    ? `SELFTEST FAILED, these detectors missed their own planted defect: ${missed.join(", ")}`
    : 'SELFTEST PASSED, every detector caught its planted defect');
  await b.close();
  process.exit(missed.length);
}
await p.viewport(1280, 1000, false);
await p.goto('http://localhost:5173/');
await wait('.pf-bottomnav__item');
await openEverything();
await shoot('01-board');

// The keys come off the board rather than being written here, because a route typed from
// memory that does not exist renders an empty page and the checks all pass on it.
const categories = await p.eval(
  `[...document.querySelectorAll('a.category')].map(a => a.getAttribute('href'))`);
if (categories.length < 3) throw new Error('the board offered no categories to walk');

let n = 2;
for (const route of [...categories.slice(0, 3), '/conditions']) {
  await go(route);
  await wait('.rule, .condition-row, .pf-card', 20000);
  await openEverything();
  await shoot(`0${n}-${route.split('/').pop()}`);
  n++;
}

// A record, opened over the list it was found in.
await go(categories[0]);
await wait('.rule .open', 20000);
await p.eval(`document.querySelector('.rule .open')?.click()`);
if (!(await wait('.pf-sheet__panel', 12000))) throw new Error('a record never opened');
await sleep(900);
await openEverything();
await shoot('06-rule-sheet');
await p.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(600);

await go('/search');
await sleep(1200);
await type('.pf-search__input', 'shield');
await sleep(2200);
await openEverything();
await shoot('07-search');

// ---- the campaign ------------------------------------------------------------------------

await go('/campaign');
if (!(await wait('.join'))) throw new Error('the campaign page never offered a way in');
await shoot('08-join');

await clickText('button', 'Start a new campaign');
await wait('.campaign-code');
await shoot('09-empty-party');

await type('.pf-textarea', pathbuilder);
await clickText('button', 'Import');
await wait('.character');
await openEverything();
await shoot('10-party');

// The three sheets a character opens.
await p.eval(`document.querySelector('.stat')?.click()`);
await sleep(1100);
await shoot('11-breakdown');
await p.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(600);

await clickText('.acts button', 'Effects');
await wait('.condition', 12000);
await openEverything();
await shoot('12-effects');
for (const arm of ['Rules', 'Custom']) {
  await clickText('.picker__arms button, .picker__arms label', arm);
  await sleep(900);
  await shoot(`12-effects-${arm.toLowerCase()}`);
}
await p.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(600);

await clickText('.acts button', 'Edit');
await wait('.editor', 12000);
await shoot('13-editor');
await p.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(600);

// ---- the modes ---------------------------------------------------------------------------

await go('/campaign/exploration');
await wait('.explore');
await openEverything();
await shoot('14-exploration');

await go('/campaign/camp');
await wait('.camp');
await openEverything();
await shoot('15-camp');

await go('/campaign/downtime');
await wait('.downtime');
await openEverything();
await shoot('16-downtime');

await go('/campaign/encounter');
await wait('.fight');
await shoot('17-fight-empty');

await clickText('.fight__acts button', 'Add');
await wait('.adding .pf-section__label');
await shoot('18-adding');
await p.eval(`[...document.querySelectorAll('.adding .pf-row')]
  .find(r => r.textContent.includes('Gnibbo'))?.click()`);
await sleep(900);
await type('.adding .pf-search__input', 'Ogre Warrior');
await sleep(2000);
await shoot('19-adding-found');
await p.eval(`[...document.querySelectorAll('.adding .pf-row')]
  .find(r => r.textContent.includes('Ogre Warrior'))?.click()`);
await sleep(1100);
await p.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(700);
await shoot('20-fight-before-roll');

await clickText('.fight__acts button', 'Roll');
await sleep(1300);
await shoot('21-fight');

// A condition on a monster, which the client could not do until now.
await p.eval(`[...document.querySelectorAll('.turn')]
  .find(t => t.textContent.includes('Ogre'))?.querySelector('.chip--add')?.click()`);
await wait('.condition', 12000);
await p.eval(`(() => {
  const row = [...document.querySelectorAll('.condition')].find(c => c.dataset.condition === 'frightened');
  row?.querySelector('.pf-stepper__btn:last-of-type, button:last-of-type')?.click();
})()`);
await sleep(1300);
await p.eval(`document.querySelector('.pf-sheet__close')?.click()`);
await sleep(800);
await shoot('22-fight-monster-condition');

if (out) writeFileSync(`${out}/report.txt`, seen.join('\n'));
console.log(seen.join('\n'));

const found = seen.filter((line) => line.startsWith('LOOK')).length;
console.log(`\n${seen.length} page-width-theme combinations, ${found} with a finding`);
await b.close();
process.exit(found);
