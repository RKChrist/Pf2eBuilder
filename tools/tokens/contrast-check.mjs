// Reads dist/tokens.css and checks every foreground/background pair the UI actually uses.
// It parses the generated file rather than a copy of the palette, so it cannot pass while
// the shipped tokens say something else.
import { readFileSync } from 'node:fs';

const css = readFileSync('dist/tokens.css', 'utf8');

const block = (selector) => {
  const start = css.indexOf(selector);
  if (start < 0) throw new Error(`no ${selector} block in dist/tokens.css`);
  const body = css.slice(css.indexOf('{', start) + 1);
  return body.slice(0, body.indexOf('\n}'));
};

const vars = (text) =>
  Object.fromEntries([...text.matchAll(/--([\w-]+):\s*(#[0-9A-Fa-f]{6})\s*;/g)].map((m) => [m[1], m[2]]));

const light = vars(block(':root {'));
const dark = { ...light, ...vars(block(':root[data-theme="dark"]')) };

const lin = (c) => { c /= 255; return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4; };
const lum = (h) => { const n = parseInt(h.slice(1), 16);
  return 0.2126 * lin((n >> 16) & 255) + 0.7152 * lin((n >> 8) & 255) + 0.0722 * lin(n & 255); };
const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };

// 4.5 is WCAG AA for body text. 3.0 covers borders and large type, which carry meaning here
// because the sheet's grid lines separate one character's numbers from another's.
const TEXT_ON_PAGE = ['text-primary', 'text-secondary', 'text-muted', 'accent-base', 'danger-base',
  'rarity-common', 'rarity-uncommon', 'rarity-rare', 'rarity-unique',
  'outcome-critical-failure', 'outcome-failure', 'outcome-success', 'outcome-critical-success',
  'rank-filled', 'effect-up', 'effect-down'];

let failures = 0;
const check = (label, fg, bg, min) => {
  const r = ratio(fg, bg);
  if (r < min) failures++;
  console.log(`${r >= min ? 'PASS' : 'FAIL'}  ${label.padEnd(38)} ${r.toFixed(2)}:1  (needs ${min.toFixed(1)})`);
};

for (const [theme, t] of [['light', light], ['dark', dark]]) {
  // overlay is the bottom sheet, which carries the same rarity chips and stat colours as a
  // card, so it has to clear the same bar.
  for (const surface of ['page', 'raised', 'overlay']) {
    for (const name of TEXT_ON_PAGE) check(`${theme} ${name} on surface-${surface}`, t[name], t[`surface-${surface}`], 4.5);
  }
  // Sunken is the ground under a pressed row, a stepper key and an unselected segment.
  for (const name of ['text-primary', 'text-secondary']) {
    check(`${theme} ${name} on surface-sunken`, t[name], t['surface-sunken'], 4.5);
  }
  check(`${theme} text-on-accent on accent-base`, t['text-on-accent'], t['accent-base'], 4.5);
  check(`${theme} text-on-danger on danger-base`, t['text-on-danger'], t['danger-base'], 4.5);
  check(`${theme} line-strong on surface-page`, t['line-strong'], t['surface-page'], 3.0);
  check(`${theme} line-focus on surface-page`, t['line-focus'], t['surface-page'], 3.0);
  console.log('');
}

// A critical result must read as more intense than its plain counterpart, or the four degrees
// of success collapse into two. Intensity means distance from the page in either theme.
for (const [theme, t] of [['light', light], ['dark', dark]]) {
  for (const [crit, plain] of [['critical-failure', 'failure'], ['critical-success', 'success']]) {
    const c = ratio(t[`outcome-${crit}`], t['surface-page']);
    const p = ratio(t[`outcome-${plain}`], t['surface-page']);
    if (c <= p) failures++;
    console.log(`${c > p ? 'PASS' : 'FAIL'}  ${`${theme} ${crit} outranks ${plain}`.padEnd(38)} ${c.toFixed(2)} vs ${p.toFixed(2)}`);
  }
}

console.log(`\n${failures} failure(s)`);
process.exit(failures ? 1 : 0);
