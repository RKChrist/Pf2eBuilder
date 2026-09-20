// One record from every category in the ruleset: it opens, it has mechanics, and it has words.
//
//   node tools/ui-check/verify-records.mjs
//
// Talks to the API only. A description is fetched from Archives of Nethys the first time a record
// is opened and kept on disk after that, so the first run makes one polite request per category
// and later runs make none. Exit code is the number of categories with a problem.
const api = process.env.API ?? 'http://localhost:5092';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const get = async (path) => {
  const response = await fetch(`${api}${path}`);
  return response.ok ? response.json() : null;
};

const counts = await get('/rules/counts');
let problems = 0;
const wordless = [];

for (const { category, count } of counts.categories.sort((a, b) => a.category.localeCompare(b.category))) {
  const found = await get(`/rules?Category=${encodeURIComponent(category)}&PageSize=3`);
  const pick = found?.items?.[Math.min(1, (found?.items?.length ?? 1) - 1)];
  if (!pick) {
    problems++;
    console.log(`FAIL  ${category.padEnd(34)} ${String(count).padStart(5)}  lists nothing`);
    continue;
  }

  const detail = await get(`/rules/${encodeURIComponent(pick.id)}`);
  const text = await get(`/rules/${encodeURIComponent(pick.id)}/text`);
  const words = text?.markdown?.length ?? 0;
  const ok = !!detail && text?.reached !== false;
  if (!ok) problems++;
  if (ok && words === 0) wordless.push(`${category} (${pick.name})`);

  console.log(`${ok ? 'PASS' : 'FAIL'}  ${category.padEnd(34)} ${String(count).padStart(5)}  ` +
    `${pick.name.slice(0, 30).padEnd(30)} ${String(detail?.mechanics?.length ?? 0).padStart(2)} fields, ${String(words).padStart(5)} chars of text` +
    `${text?.reached === false ? '  SOURCE UNREACHABLE' : ''}`);
  await sleep(250);
}

console.log(`\n${counts.total} records in ${counts.categories.length} categories`);
if (wordless.length) console.log(`no description at the source for: ${wordless.join(', ')}`);
console.log(`${problems} problem(s)`);
process.exit(problems);
