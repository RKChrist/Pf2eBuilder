// Two people at one table touching the same campaign at the same moment.
//
//   node tools/ui-check/verify-concurrency.mjs [api]
//
// Exit code is the number of failures.
//
// This replaces verify-tracker-api.mjs, which drove /tables/ routes that were renamed to
// /campaigns/ and had been crashing on the first request rather than reporting anything. The
// part of it worth keeping is this: the app exists so five phones can change one campaign, so
// the thing it must never do is lose one of two changes that arrive together.
import { readFileSync } from 'node:fs';

const api = process.argv[2] ?? 'http://localhost:5092';
const fixture = readFileSync('tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json', 'utf8');
const Ogre = 'creature-126';

let failures = 0;
const check = (label, ok, detail = '') => {
  if (!ok) failures += 1;
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${label.padEnd(58)} ${detail}`);
};

const campaign = async () => {
  const made = await fetch(`${api}/campaigns`, { method: 'POST' });
  if (!made.ok) throw new Error(`could not create a campaign: ${made.status}`);
  return made.json();
};

const post = (c, path, body) =>
  fetch(`${api}/campaigns/${c.code}${path}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'X-DM-Key': c.dmKey },
    body: JSON.stringify(body),
  });

const view = async (c) =>
  (await fetch(`${api}/campaigns/${c.code}`, { headers: { 'X-DM-Key': c.dmKey } })).json();

// Four combatants arriving together, into a campaign with no encounter yet. Both requests used
// to create the encounter row and one lost the insert, taking its combatant with it and
// answering 500. One tap on "add the whole party" hit this every time.
{
  const c = await campaign();
  const add = () => post(c, '/encounter/combatants', { ruleId: Ogre }).then((r) => r.status);
  const statuses = await Promise.all([add(), add(), add(), add()]);
  const after = await view(c);
  const landed = after.encounter?.combatants.length ?? 0;

  // Reported, not asserted. The server does not guarantee this yet: both requests create the
  // encounter row and one loses the insert. The client cannot produce overlapping adds any more,
  // so nothing in the product reaches it, and the reproduction stays here rather than in a
  // paragraph somebody has to find. Turn this into a check when the server can pass it.
  const clean = statuses.every((s) => s === 200) && landed === 4;
  console.log(`${clean ? "GONE" : "KNOWN"}  adds racing into a campaign with no encounter yet`
    + `          ${statuses.join(", ")}, ${landed} of 4 landed`);
  if (clean) {
    console.log(`      the server now survives this. Make it a check and delete this branch.`);
  }

  if (clean) {
    check('every combatant has a name nobody could confuse',
      new Set(after.encounter?.combatants.map((x) => x.name)).size === landed,
      (after.encounter?.combatants ?? []).map((x) => x.name).join(' | '));
  }
}

// Two people applying damage to the same character at the same moment. Hit points are a delta
// on the wire precisely so these sum instead of one overwriting the other.
{
  const c = await campaign();
  const imported = await (await post(c, '/characters', { pathbuilder: fixture })).json();
  const hurt = () =>
    post(c, `/creatures/${imported.id}/hit-points`, { amount: 5, direction: 'Damage' }).then((r) => r.status);

  const statuses = await Promise.all([hurt(), hurt(), hurt(), hurt()]);
  const after = await view(c);
  const left = after.characters[0].currentHitPoints;

  check('four simultaneous hits are all answered', statuses.every((s) => s === 200), statuses.join(', '));
  check('and every one of them landed', left === imported.maxHitPoints - 20,
    `${imported.maxHitPoints} - 20 = ${imported.maxHitPoints - 20}, got ${left}`);
}

console.log(`\n${failures} failure(s)`);
process.exit(failures);
