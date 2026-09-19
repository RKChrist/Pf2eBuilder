// Writes NOTICE-works.md: every work the seeded records are attributed to, with how many
// records came from each.
//
//   node tools/rules-import/works.mjs
//
// ORC attribution names each Licensor and each work. The list of works is in the data, so it is
// derived here rather than typed, which is the half that can be got right mechanically. The
// author list for each work cannot be: it has to be copied from that book's own legal page, and
// this script leaves a blank line for it rather than inventing one.
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const seed = join(import.meta.dirname, 'out', 'seed');
const out = join(import.meta.dirname, '..', '..', 'NOTICE-works.md');

const counts = new Map();
let records = 0;
let unattributed = 0;

for (const file of readdirSync(seed)) {
  if (!file.endsWith('.json')) continue;
  for (const record of JSON.parse(readFileSync(join(seed, file), 'utf8'))) {
    records++;
    const work = record.primary_source;
    if (typeof work !== 'string' || work.length === 0) {
      unattributed++;
      continue;
    }
    counts.set(work, (counts.get(work) ?? 0) + 1);
  }
}

const works = [...counts].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
const grouped = (n) => n.toLocaleString('en-US');

const lines = [
  '# Works',
  '',
  'Derived, not written. Regenerate with:',
  '',
  '    node tools/rules-import/works.mjs',
  '',
  `${grouped(records)} seeded records are attributed to ${grouped(works.length)} works.`,
  unattributed > 0
    ? `${grouped(unattributed)} carry no \`primary_source\` and are listed under Unattributed below.`
    : 'Every record names the work it came from.',
  '',
  'ORC attribution names each Licensor **and** each work. The works are here because the data',
  'knows them. The author line under each is blank on purpose: it has to be copied from that',
  "book's own legal page, and a reconstructed author list is worse than a missing one.",
  '',
  '| Records | Work | Authors |',
  '| ---: | --- | --- |',
  ...works.map(([work, n]) => `| ${grouped(n)} | ${work} | |`),
  '',
];

if (unattributed > 0) {
  lines.push(
    '## Unattributed',
    '',
    `${grouped(unattributed)} records carry no \`primary_source\`. They are mostly derived rows such as`,
    "an item's bonus split out from its parent, which inherit the parent's attribution.",
    '',
  );
}

writeFileSync(out, lines.join('\n'));
console.log(`${works.length} works, ${records} records, ${unattributed} unattributed -> ${out}`);
