// Drives the component gallery in a real Chrome and asserts the behaviour that cannot be
// read off the source: pointer-media sizing, keyboard operation, the no-cross invariant,
// touch dragging without page scroll, and the hold-to-confirm delay.
import { launch, openPage, reporter, sleep } from './cdp.mjs';

const GALLERY = process.argv[2];
if (!GALLERY) throw new Error('usage: node verify-sliders.mjs <absolute path to gallery/index.html>');
const url = `file:///${GALLERY.replace(/\\/g, '/')}`;

const check = reporter();
const browser = await launch({ headless: false });
const page = await openPage(browser);

// Navigated here rather than by openPage, which took a url until the day it took options and
// stopped taking one. This file kept calling it the old way and drove about:blank.
await page.goto(url);

const centre = async (selector, nth = 0) =>
  page.eval(`(() => {
    const el = document.querySelectorAll(${JSON.stringify(selector)})[${nth}];
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return JSON.stringify({ x: r.x + r.width / 2, y: r.y + r.height / 2, w: r.width, h: r.height, left: r.x, right: r.right });
  })()`).then((s) => (s ? JSON.parse(s) : null));

const show = (selector, nth = 0) =>
  page.eval(`(() => {
    const el = document.querySelectorAll(${JSON.stringify(selector)})[${nth}];
    if (el) el.scrollIntoView({ block: 'center' });
    return !!el;
  })()`).then(async (ok) => { await sleep(250); return ok; });

// The gallery has a sticky header. A rect can say an element is on screen while the header sits
// on top of it, and then every pointer assertion here is answered by the header instead.
const topmostAt = (x, y) =>
  page.eval(`(() => { const e = document.elementFromPoint(${x}, ${y}); return e ? e.className || e.tagName : 'nothing'; })()`);

// ---------------------------------------------------------------- load

const count = (selector) => page.eval(`document.querySelectorAll(${JSON.stringify(selector)}).length`);

check('page has no console errors', page.consoleErrors().length === 0, page.consoleErrors().join(' | '));

const sliders = await count('.pf-slider');
const ranges = await count('.pf-slider--range');
check('slider present in gallery', sliders > 0, `${sliders} found`);
check('range slider present in gallery', ranges > 0, `${ranges} found`);
if (sliders === 0 || ranges === 0) {
  console.log('\nnothing to drive; the remaining checks would pass vacuously, so stopping here.');
  check.done();
  await browser.close();
  process.exit(1);
}

check('behaviour script ran (fill set from value)', (await page.eval(
  `getComputedStyle(document.querySelector('.pf-slider')).getPropertyValue('--pf-fill-end').trim()`,
)) !== '0%');

const readouts = await count('.pf-slider__readout');
// Set once on the box-sizing rule and inherited, rather than a second hand-kept selector list.
check('the browser tap highlight is off on every control', await page.eval(
  `['.pf-btn', '.pf-slider__input', '.pf-slider__readout', '.pf-row', '.pf-stepper__btn', '.pf-segmented__option']
     .every(s => { const e = document.querySelector(s);
                   return e && getComputedStyle(e).webkitTapHighlightColor === 'rgba(0, 0, 0, 0)'; })`,
));

check('readouts carry inputmode numeric',
  readouts > 0 && (await page.eval(
    `[...document.querySelectorAll('.pf-slider__readout')].every(e => e.getAttribute('inputmode') === 'numeric')`)),
  `${readouts} readouts`);

// ---------------------------------------------------------------- pointer media sizing

const sizes = () => page.eval(`(() => {
  const s = getComputedStyle(document.documentElement);
  const input = document.querySelector('.pf-slider__input');
  const r = input.getBoundingClientRect();
  return JSON.stringify({
    mark: s.getPropertyValue('--pf-thumb-mark').trim(),
    track: s.getPropertyValue('--pf-track').trim(),
    hit: s.getPropertyValue('--pf-thumb-hit').trim(),
    inputHeight: Math.round(r.height),
    coarse: matchMedia('(pointer: coarse)').matches,
    anyCoarse: matchMedia('(any-pointer: coarse)').matches,
  });
})()`).then(JSON.parse);

await page.coarse(false);
const fine = await sizes();
check('fine pointer emulated', fine.coarse === false && fine.anyCoarse === false, JSON.stringify(fine));
check.eq('fine: thumb mark is --control-thumb-fine', fine.mark, '20px');
check.eq('fine: track is --control-track-fine', fine.track, '6px');
check.eq('fine: hit box stays at --tap-min', fine.hit, '44px');
check('fine: range input box still 44px tall', fine.inputHeight >= 44, `${fine.inputHeight}px`);

await page.coarse(true);
const coarse = await sizes();
check('coarse pointer emulated', coarse.coarse === true && coarse.anyCoarse === true, JSON.stringify(coarse));
check.eq('coarse: thumb mark is --control-thumb-coarse', coarse.mark, '44px');
check.eq('coarse: track is --control-track-coarse', coarse.track, '14px');
check('coarse and fine renderings actually differ', coarse.mark !== fine.mark && coarse.track !== fine.track);

await page.coarse(false);

// The gallery has to show both sizes at once, so the difference is visible without a device.
const pinned = await page.eval(`(() => {
  const a = document.querySelector('.g-pointer--coarse .pf-slider');
  const b = document.querySelector('.g-pointer--fine .pf-slider');
  if (!a || !b) return JSON.stringify({ found: false });
  const g = (e) => getComputedStyle(e);
  const thumb = (e) => g(e).getPropertyValue('--pf-thumb-mark').trim();
  const track = (e) => g(e).getPropertyValue('--pf-track').trim();
  return JSON.stringify({ found: true, coarseMark: thumb(a), fineMark: thumb(b), coarseTrack: track(a), fineTrack: track(b) });
})()`).then(JSON.parse);
check('gallery pins a coarse and a fine specimen side by side', pinned.found);
if (pinned.found) {
  check.eq('pinned coarse specimen draws the coarse mark', pinned.coarseMark, '44px');
  check.eq('pinned fine specimen draws the fine mark', pinned.fineMark, '20px');
  check('the two pinned specimens differ on screen', pinned.coarseTrack !== pinned.fineTrack,
    `${pinned.coarseTrack} vs ${pinned.fineTrack}`);
}

// gallery.css pins tokens, it does not restyle components. Any pf- rule in it breaks that.
const { readFileSync } = await import('node:fs');
const galleryCss = readFileSync(GALLERY.replace(/index\.html$/i, 'gallery.css'), 'utf8');
const componentRules = [...galleryCss.matchAll(/^[^@{}\n]*\.pf-[\w-]+[^{}\n]*\{([^}]*)\}/gm)]
  .filter(([, body]) => /^\s*(?!--)[a-z-]+\s*:/m.test(body));
check('gallery.css sets only custom properties on pf- selectors', componentRules.length === 0,
  componentRules.map((m) => m[0].split('{')[0].trim()).join(' | '));

// ---------------------------------------------------------------- keyboard, single slider

const KEYS = {
  ArrowRight: ['ArrowRight', 39],
  ArrowLeft: ['ArrowLeft', 37],
  Home: ['Home', 36],
  End: ['End', 35],
  PageUp: ['PageUp', 33],
  PageDown: ['PageDown', 34],
};
const press = (name) => page.key(name, KEYS[name][0], KEYS[name][1]);

const val = (selector, nth = 0) =>
  page.eval(`Number(document.querySelectorAll(${JSON.stringify(selector)})[${nth}].value)`);

await show('.pf-slider__input');
await page.eval(`document.querySelector('.pf-slider__input').focus()`);
check('slider input takes focus', await page.eval(`document.activeElement.classList.contains('pf-slider__input')`));

const before = await val('.pf-slider__input');
await press('ArrowRight');
const afterRight = await val('.pf-slider__input');
check('ArrowRight steps the value up', afterRight === before + 1, `${before} -> ${afterRight}`);
await press('ArrowLeft');
check.eq('ArrowLeft steps the value back down', await val('.pf-slider__input'), before);

await press('Home');
const atHome = await val('.pf-slider__input');
const min = await page.eval(`Number(document.querySelector('.pf-slider__input').min)`);
check.eq('Home goes to the minimum', atHome, min);

await press('End');
const atEnd = await val('.pf-slider__input');
const max = await page.eval(`Number(document.querySelector('.pf-slider__input').max)`);
check.eq('End goes to the maximum', atEnd, max);

await press('PageDown');
const afterPageDown = await val('.pf-slider__input');
check('PageDown jumps more than one step', max - afterPageDown > 1, `${max} -> ${afterPageDown}`);
await press('PageUp');
check('PageUp jumps back up', (await val('.pf-slider__input')) > afterPageDown);

check('keyboard focus is visible', await page.eval(
  `(() => { const e = document.querySelector('.pf-slider__input');
            return e.matches(':focus-visible') && getComputedStyle(e).outlineStyle !== 'none'; })()`,
));

check('readout mirrors the slider', await page.eval(
  `(() => { const s = document.querySelector('.pf-slider');
            return s.querySelector('.pf-slider__readout').value === s.querySelector('.pf-slider__input').value; })()`,
));

// ---------------------------------------------------------------- no-cross, range slider

await show('.pf-slider--range');
const low = '.pf-slider--range .pf-slider__input--low';
const high = '.pf-slider--range .pf-slider__input--high';

await page.eval(`document.querySelector('${low}').focus()`);
await press('End');
const crossedLow = { lo: await val(low), hi: await val(high) };
check('low thumb driven to the top cannot pass the high thumb', crossedLow.lo <= crossedLow.hi, JSON.stringify(crossedLow));

await page.eval(`document.querySelector('${high}').focus()`);
await press('Home');
const crossedHigh = { lo: await val(low), hi: await val(high) };
check('high thumb driven to the bottom cannot pass the low thumb', crossedHigh.hi >= crossedHigh.lo, JSON.stringify(crossedHigh));

await page.eval(`document.querySelector('${high}').focus()`);
await press('ArrowRight');
check('high thumb is independently keyboard-operable', (await val(high)) > crossedHigh.hi);

// Both thumbs at the top is reachable, and with the high input permanently on top it used to be
// a corner a pointer could not drag the pair back out of. High first, then low, or the low is
// only ever clamped to wherever the high happens to be.
await page.eval(`document.querySelector('${high}').focus()`);
await press('End');
await page.eval(`document.querySelector('${low}').focus()`);
await press('End');
const parked = await page.eval(`(() => {
  const root = document.querySelector('.pf-slider--range');
  const lo = root.querySelector('.pf-slider__input--low');
  const hi = root.querySelector('.pf-slider__input--high');
  const z = (e) => getComputedStyle(e).zIndex;
  return JSON.stringify({ lo: lo.value, hi: hi.value, loZ: z(lo), hiZ: z(hi) });
})()`).then(JSON.parse);
check('both thumbs parked at the maximum', parked.lo === parked.hi, JSON.stringify(parked));
check('the grabbable thumb there is the one with room to move',
  Number(parked.loZ) > Number(parked.hiZ), `low z=${parked.loZ}, high z=${parked.hiZ}`);
await page.eval(`(() => {
  const root = document.querySelector('.pf-slider--range');
  const lo = root.querySelector('.pf-slider__input--low');
  lo.value = lo.min;
  lo.dispatchEvent(new Event('input', { bubbles: true }));
})()`);

check('both thumbs announce a name and a value to a screen reader', await page.eval(
  `(() => {
     const root = document.querySelector('.pf-slider--range');
     const inputs = [...root.querySelectorAll('.pf-slider__input')];
     return inputs.length === 2 && inputs.every(i =>
       (i.getAttribute('aria-label') || i.getAttribute('aria-labelledby')) && i.value !== '');
   })()`,
));

check('the wall each thumb hits is announced', await page.eval(
  `(() => {
     const lo = document.querySelector('${low}'), hi = document.querySelector('${high}');
     return lo.getAttribute('aria-valuemax') === hi.value && hi.getAttribute('aria-valuemin') === lo.value;
   })()`,
));

// ---------------------------------------------------------------- touch drag does not scroll

// No device metrics override here. With mobile:true Chrome lays the page out at the requested
// width but reports a 492px innerWidth, so getBoundingClientRect and the CDP input coordinate
// space disagree and every touch lands somewhere else. Touch emulation alone is enough to make
// the pointer coarse, and then the two coordinate spaces are the same one.
await page.send('Emulation.clearDeviceMetricsOverride');
await page.coarse(true);
await show('.pf-slider__input');
const scrollBefore = await page.eval(`window.scrollY`);
const scrollable = await page.eval(`document.documentElement.scrollHeight - window.innerHeight`);
check('the page is scrollable, so "did not scroll" means something', scrollable > 100, `${scrollable}px of scroll`);

const rail = await centre('.pf-slider__input');
const under = await topmostAt(rail.left + rail.w * 0.15, rail.y);
check('the slider, not the sticky header, is under the touch point', under.includes('pf-slider__input'), under);
const startValue = await val('.pf-slider__input');
await page.touchDrag({ x: rail.left + rail.w * 0.15, y: rail.y }, { x: rail.left + rail.w * 0.85, y: rail.y });
const scrollAfter = await page.eval(`window.scrollY`);
const draggedValue = await val('.pf-slider__input');
check('touch drag moves the thumb', draggedValue !== startValue, `${startValue} -> ${draggedValue}`);
check('touch drag does not scroll the page', scrollBefore === scrollAfter, `${scrollBefore} -> ${scrollAfter}`);

// pan-y rather than none: the horizontal axis belongs to the thumb, the vertical axis stays
// with the page, so a full-width slider is not a stripe the page refuses to scroll under.
check.eq('touch-action is pan-y', await page.eval(`getComputedStyle(document.querySelector('.pf-slider__input')).touchAction`), 'pan-y');
await page.eval(`window.scrollTo(0, ${scrollBefore})`);
await sleep(150);
await page.touchDrag({ x: rail.x, y: rail.y }, { x: rail.x, y: rail.y - 160 }, 14);
check('a vertical swipe starting on the slider still scrolls the page',
  (await page.eval(`window.scrollY`)) > scrollBefore, `${scrollBefore} -> ${await page.eval(`window.scrollY`)}`);

// ---------------------------------------------------------------- haptics

await page.eval(`window.__buzz = 0; navigator.vibrate = () => { window.__buzz++; return true; };`);
const TICKED = '.pf-slider:has(.pf-slider__ticks) .pf-slider__input';
if (await page.eval(`!!document.querySelector('${TICKED}')`)) {
  await show(TICKED);
  const r2 = await centre(TICKED);
  const under2 = await topmostAt(r2.left + r2.w * 0.2, r2.y);
  check('the ticked slider is under the touch point', under2.includes('pf-slider__input'), under2);
  await page.touchDrag({ x: r2.left + r2.w * 0.2, y: r2.y }, { x: r2.left + r2.w * 0.8, y: r2.y }, 16);
  const buzz = await page.eval(`window.__buzz`);
  check('a touch drag across ticks fires haptic detents', buzz > 0, `${buzz} calls`);
} else {
  check('a slider with ticks exists to test haptics on', false, 'no .pf-slider__ticks found');
}

// ---------------------------------------------------------------- value bubble

await page.coarse(false);
await page.send('Emulation.clearDeviceMetricsOverride');
await show('.pf-slider__input');
const rail2 = await centre('.pf-slider__input');
await page.mouseDown(rail2.left + rail2.w * 0.5, rail2.y);
await sleep(150);
const bubbling = await page.eval(`(() => {
  const s = document.querySelector('.pf-slider');
  const b = s.querySelector('.pf-slider__bubble');
  return JSON.stringify({
    flagged: s.classList.contains('pf-slider--bubbling'),
    visible: Number(getComputedStyle(b).opacity) > 0,
    text: b.textContent.trim(),
    value: s.querySelector('.pf-slider__input').value,
  });
})()`).then(JSON.parse);
await page.mouseUp(rail2.left + rail2.w * 0.5, rail2.y);
check('bubble appears while dragging', bubbling.flagged && bubbling.visible, JSON.stringify(bubbling));
check.eq('bubble reads the live value', bubbling.text, bubbling.value);
await sleep(250);
check('bubble leaves on release', await page.eval(
  `Number(getComputedStyle(document.querySelector('.pf-slider__bubble')).opacity) === 0`,
));

// ---------------------------------------------------------------- hold to confirm

const hold = await page.eval(`document.querySelectorAll('.pf-btn--hold').length`);
if (hold > 0) {
  await show('.pf-btn--hold');
  await page.eval(`window.__fired = 0; document.querySelector('.pf-btn--hold').addEventListener('click', () => window.__fired++);`);
  const b = await centre('.pf-btn--hold');
  await page.mouseDown(b.x, b.y);
  await sleep(120);
  await page.mouseUp(b.x, b.y);
  await sleep(150);
  check.eq('a quick tap on a destructive action does nothing', await page.eval(`window.__fired`), 0);

  await page.mouseDown(b.x, b.y);
  await sleep(900);
  await page.mouseUp(b.x, b.y);
  await sleep(200);
  check('holding a destructive action fires it', (await page.eval(`window.__fired`)) > 0, `${await page.eval(`window.__fired`)} click(s)`);
} else {
  check('a hold-to-confirm button exists in the gallery', false, 'no .pf-btn--hold found');
}

// ---------------------------------------------------------------- sheet drag to dismiss

// At a phone width, because dragging a sheet away is a phone gesture and the stylesheet stopped
// offering it anywhere else. Every sheet docks to the side from 1024px, where there is no
// grabber to take hold of and nothing that goes down, which is asserted on its own below. This
// section ran at whatever width the window happened to be and started failing the day that
// changed, against a product that was right.
await page.viewport(390, 800, true);

const sheet = await page.eval(`document.querySelectorAll('.pf-sheet__grab').length`);
if (sheet > 0) {
  await page.coarse(true);
  await show('.pf-sheet__grab');
  await page.eval(`window.__closed = 0; document.querySelector('.pf-sheet__close').addEventListener('click', () => window.__closed++);`);
  const g = await centre('.pf-sheet__grab');
  const panel = await centre('.pf-sheet__panel');

  // Mid-drag the panel has to sit exactly under the finger, so its transition must be off.
  await page.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: g.x, y: g.y }] });
  await page.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: g.x, y: g.y + 40 }] });
  await sleep(60);
  const midDrag = await page.eval(`(() => {
    const p = document.querySelector('.pf-sheet__panel');
    return JSON.stringify({
      flagged: document.querySelector('.pf-sheet').classList.contains('pf-sheet--dragging'),
      duration: getComputedStyle(p).transitionDuration,
      drag: p.style.getPropertyValue('--pf-sheet-drag'),
    });
  })()`).then(JSON.parse);
  await page.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  // Chrome treats a gesture that starts immediately after the previous one as part of the same
  // sequence and cancels the pointer, so the probe drag and the real one need a gap between.
  await sleep(600);
  check('the panel follows the finger with no transition', midDrag.flagged && midDrag.duration === '0s', JSON.stringify(midDrag));
  check('the panel actually moved with the finger', midDrag.drag !== '', `--pf-sheet-drag: ${midDrag.drag}`);
  check('the transition comes back on release', await page.eval(
    `getComputedStyle(document.querySelector('.pf-sheet__panel')).transitionDuration !== '0s'`));

  await page.touchDrag({ x: g.x, y: g.y }, { x: g.x, y: g.y + panel.h * 0.6 }, 14);
  check('dragging the sheet down dismisses it', (await page.eval(`window.__closed`)) > 0, `${await page.eval(`window.__closed`)} close(s)`);

  // The other half of the same decision. A panel standing at the side is not dismissed by
  // pulling it downwards, so the grabber goes and the behaviour file is told to refuse the
  // gesture. Without this, hiding the grabber everywhere would read as a pass.
  await page.viewport(1280, 800, false);
  await sleep(300);
  const docked = await page.eval(`(() => {
    const grab = document.querySelector('.pf-sheet__grab');
    return JSON.stringify({
      grabber: grab ? getComputedStyle(grab).display : 'gone',
      docked: getComputedStyle(document.querySelector('.pf-sheet')).getPropertyValue('--pf-sheet-docked').trim(),
    });
  })()`).then(JSON.parse);
  check('a sheet at desk width docks to the side', docked.docked === '1', JSON.stringify(docked));
  check('and offers no grabber to pull down', docked.grabber === 'none', JSON.stringify(docked));

  await page.viewport(390, 800, true);
  await sleep(200);
  await page.coarse(false);
} else {
  check('the bottom sheet has a grabber', false, 'no .pf-sheet__grab found');
}

// ---------------------------------------------------------------- reduced motion

await page.send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-reduced-motion', value: 'reduce' }] });
await sleep(150);
const motion = await page.eval(`(() => {
  const s = getComputedStyle(document.documentElement);
  return JSON.stringify({ quick: s.getPropertyValue('--motion-duration-quick').trim(),
                          sheet: s.getPropertyValue('--motion-duration-sheet').trim(),
                          hold: s.getPropertyValue('--motion-duration-hold').trim() });
})()`).then(JSON.parse);
check.eq('reduced motion zeroes the quick transition', motion.quick, '0ms');
check.eq('reduced motion zeroes the sheet transition', motion.sheet, '0ms');
check.eq('reduced motion does NOT shorten the hold delay', motion.hold, '600ms');
await page.send('Emulation.setEmulatedMedia', { features: [] });

// ---------------------------------------------------------------- width

for (const width of [320, 390, 430]) {
  // mobile:false, because mobile:true reports a 492px innerWidth whatever width is asked for,
  // which would let every one of these pass without the page ever being that narrow.
  await page.viewport(width, 800, false);
  await sleep(250);
  const overflow = await page.eval(`(() => {
    const doc = document.documentElement;
    const worst = [...document.querySelectorAll('.pf-slider, .pf-slider *')]
      .reduce((m, e) => Math.max(m, e.getBoundingClientRect().right), 0);
    return JSON.stringify({ scrollWidth: doc.scrollWidth, inner: window.innerWidth, worstRight: Math.round(worst) });
  })()`).then(JSON.parse);
  check(`the page really is ${width}px wide`, overflow.inner === width, `innerWidth ${overflow.inner}`);
  check(`holds at ${width}px with no horizontal overflow`, overflow.scrollWidth <= overflow.inner, JSON.stringify(overflow));
}

// ---------------------------------------------------------------- end

const errors = page.consoleErrors();
check('no console errors after the whole run', errors.length === 0, errors.join(' | '));

const failures = check.done();
await browser.close();
process.exit(failures ? 1 : 0);
