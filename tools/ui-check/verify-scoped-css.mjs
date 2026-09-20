// Every scoped stylesheet against the markup it is scoped to.
//
// Blazor stamps a scope attribute on the elements a component writes itself, and on nothing a
// child component renders. So a rule in Page.razor.css only ever applies if the class it names
// appears in Page.razor, or the selector reaches through ::deep. A rule that does neither is
// dead, and it is dead silently: it looks right, it is never applied, and the thing it was
// meant to style has been unstyled since whenever it stopped matching.
//
// This has already happened twice here. The join form stacked on nothing for weeks because its
// rule sat in a page scope that could not reach the shell. Forty-six skill actions ran together
// as plain text because the stylesheet split filed their rules under the wrong page.
//
//   node tools/ui-check/verify-scoped-css.mjs
//
// Exit code is the number of stylesheets with dead rules in them.
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';

const ROOTS = ['src/Pf2e.Client/Pages', 'src/Pf2e.Client/Components', 'src/Pf2e.Components'];

const sheets = [];
const walk = (dir) => {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) walk(path);
    else if (entry.name.endsWith('.razor.css')) sheets.push(path);
  }
};
for (const root of ROOTS) if (existsSync(root)) walk(root);

// The classes a selector is about, minus the ones behind ::deep, which are reaching into a
// child component on purpose and cannot be checked against this file's markup.
const classesIn = (selector) => {
  const reach = selector.indexOf('::deep');
  const own = reach < 0 ? selector : selector.slice(0, reach);
  return [...own.matchAll(/\.([a-zA-Z][\w-]*)/g)].map((m) => m[1]);
};

const blocks = (css) => {
  const out = [];
  let i = 0;
  while (i < css.length) {
    while (i < css.length && /\s/.test(css[i])) i++;
    if (i >= css.length) break;
    while (css.startsWith('/*', i)) {
      i = css.indexOf('*/', i) + 2;
      while (i < css.length && /\s/.test(css[i])) i++;
    }
    const brace = css.indexOf('{', i);
    if (brace < 0) break;
    const selector = css.slice(i, brace).trim();
    let depth = 0;
    let j = brace;
    do {
      if (css[j] === '{') depth++;
      else if (css[j] === '}') depth--;
      j++;
    } while (depth > 0 && j < css.length);
    // An at-rule holds its own rules; check those rather than the query.
    if (selector.startsWith('@')) out.push(...blocks(css.slice(brace + 1, j - 1)));
    else out.push(selector);
    i = j;
  }
  return out;
};

let bad = 0;
for (const sheet of sheets) {
  const markupPath = sheet.replace(/\.css$/, '');
  if (!existsSync(markupPath)) {
    console.log(`FAIL  ${sheet}  has no ${basename(markupPath)} beside it`);
    bad++;
    continue;
  }

  const markup = readFileSync(markupPath, 'utf8');

  // Every identifier the markup contains, rather than only the contents of class attributes.
  // Razor writes class="turn @(now ? "turn--now" : null)", and a reader that stops at the
  // first inner quote calls turn--now dead. Loose on purpose: a rule wrongly called alive
  // costs nothing, and a rule wrongly called dead sends somebody hunting for a bug that is
  // not there. Class names here are distinctive enough that the looseness is cheap.
  const written = new Set(
    [...markup.matchAll(/[a-zA-Z][\w-]*/g)].map((m) => m[0]));

  const dead = [];
  for (const selector of blocks(readFileSync(sheet, 'utf8'))) {
    const wanted = classesIn(selector);
    if (wanted.length === 0) continue;
    if (!wanted.some((name) => written.has(name))) dead.push(selector.replace(/\s+/g, ' '));
  }

  if (dead.length) {
    bad++;
    console.log(`FAIL  ${sheet}`);
    for (const selector of dead) console.log(`        ${selector}`);
  } else {
    console.log(`PASS  ${sheet}`);
  }
}

console.log(`\n${bad} stylesheet(s) with rules that can never apply`);
process.exit(bad ? 1 : 0);
