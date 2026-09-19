// Drives the tracker API the way the party screen will, and listens on the hub the way a
// second page at the same table does. It needs no browser, because none of this is UI.
//
//   dotnet run --project src/Pf2e.Api --urls http://localhost:5692
//   node tools/ui-check/verify-tracker-api.mjs http://localhost:5692
//
// Exit code is the number of failures.
import { readFileSync } from 'node:fs';

const api = process.argv[2] ?? 'http://localhost:5692';
const code = process.argv[3] ?? `T${Math.floor(Math.random() * 90000 + 10000)}`;
const fixture = 'tests/Pf2e.Persistence.Tests/Fixtures/gnibbo.json';

let failures = 0;
const check = (label, actual, expected) => {
  const ok = JSON.stringify(actual) === JSON.stringify(expected);
  if (!ok) failures += 1;
  console.log(`${ok ? 'pass' : 'FAIL'}  ${label}: ${JSON.stringify(actual)}${ok ? '' : ` expected ${JSON.stringify(expected)}`}`);
};

const send = async (method, path, body) => {
  const response = await fetch(`${api}${path}`, {
    method,
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return { status: response.status, body: await response.json() };
};

const pushes = [];
const hub = await (async () => {
  const negotiated = await fetch(`${api}/hub/table/negotiate?negotiateVersion=1`, { method: 'POST' });
  const { connectionToken } = await negotiated.json();
  const socket = new WebSocket(`${api.replace('http', 'ws')}/hub/table?id=${connectionToken}`);
  await new Promise((resolve) => socket.addEventListener('open', resolve, { once: true }));
  socket.addEventListener('message', (event) => {
    for (const frame of String(event.data).split('').filter(Boolean)) {
      const message = JSON.parse(frame);
      if (message.type === 1) pushes.push(message);
    }
  });
  socket.send('{"protocol":"json","version":1}');
  socket.send(`${JSON.stringify({ type: 1, target: 'JoinTable', arguments: [code] })}`);
  await new Promise((resolve) => setTimeout(resolve, 400));
  return socket;
})();

const imported = await send('POST', `/tables/${code}/characters`, { pathbuilder: readFileSync(fixture, 'utf8') });
check('import status', imported.status, 200);
check('maximum hit points', imported.body.maxHitPoints, 76);
check('armour class', imported.body.armorClass.total, 25);
check('armour class base', imported.body.armorClass.base, 22);
check('armour modifier', imported.body.armorClass.applied.map((m) => [m.source, m.type, m.value]),
  [['Studded Leather Armor', 'Item', 3]]);

const id = imported.body.id;
// The slot id is the effect's primary key, so the client draws a fresh one per slot.
const slot = crypto.randomUUID();
const anthemSlot = crypto.randomUUID();
const heroismSlot = crypto.randomUUID();
const emptySlot = crypto.randomUUID();
const clumsy = { effect: { name: 'Clumsy', kind: 'Seeded', key: 'clumsy', value: 2, duration: '1 minute', modifiers: [] } };

const applied = await send('PUT', `/tables/${code}/characters/${id}/effects/${slot}`, clumsy);
check('clumsy 2 status', applied.status, 200);
check('clumsy 2 armour class', applied.body.armorClass.total, 23);
check('clumsy 2 reflex falls', applied.body.reflex.total, imported.body.reflex.total - 2);
check('clumsy 2 leaves will alone', applied.body.will.total, imported.body.will.total);
check('clumsy 2 is in the breakdown', applied.body.armorClass.applied.map((m) => m.source).sort(),
  ['Clumsy 2', 'Studded Leather Armor']);
check('clumsy carries its duration', applied.body.effects.map((e) => [e.name, e.kind, e.value, e.hasValue, e.duration]),
  [['Clumsy', 'Seeded', 2, true, '1 minute']]);

const again = await send('PUT', `/tables/${code}/characters/${id}/effects/${slot}`, clumsy);
check('the same effect twice leaves one', again.body.effects.length, 1);

const anthem = {
  effect: {
    name: 'Courageous Anthem', kind: 'Custom', key: null, value: 0, duration: '1 round',
    modifiers: [{ type: 'Status', value: 1, applies: [{ kind: 'Exactly', stat: 'Will', attribute: null, skillName: null }] }],
  },
};
const custom = await send('PUT', `/tables/${code}/characters/${id}/effects/${anthemSlot}`, anthem);
check('a custom effect lands', custom.body.will.total, imported.body.will.total + 1);
check('a custom effect names itself', custom.body.will.applied.map((m) => [m.source, m.type, m.value]),
  [['Courageous Anthem', 'Status', 1]]);

const heroism = {
  effect: {
    name: 'Heroism', kind: 'Custom', key: null, value: 0, duration: null,
    modifiers: [{ type: 'Status', value: 2, applies: [{ kind: 'Exactly', stat: 'Will', attribute: null, skillName: null }] }],
  },
};
const stacked = await send('PUT', `/tables/${code}/characters/${id}/effects/${heroismSlot}`, heroism);
check('the larger status bonus wins', stacked.body.will.total, imported.body.will.total + 2);
check('the smaller is visibly suppressed', stacked.body.will.suppressed.map((s) => s.modifier.source), ['Courageous Anthem']);
check('and it says why', stacked.body.will.suppressed[0].reason.includes('does not stack'), true);

const hurt = await send('POST', `/tables/${code}/characters/${id}/hit-points`, { delta: -12 });
const hurtAgain = await send('POST', `/tables/${code}/characters/${id}/hit-points`, { delta: -12 });
check('two deltas sum', hurtAgain.body.currentHitPoints, 52);

const removed = await send('PUT', `/tables/${code}/characters/${id}/effects/${slot}`, { effect: null });
check('emptying a slot removes it', removed.body.effects.length, 2);
const absent = await send('PUT', `/tables/${code}/characters/${id}/effects/${emptySlot}`, { effect: null });
check('emptying an empty slot succeeds', absent.status, 200);

const second = JSON.parse(readFileSync(fixture, 'utf8'));
second.build.name = 'Tarrow';
const added = await send('POST', `/tables/${code}/characters`, { pathbuilder: JSON.stringify(second) });
check('a second character on an existing table', added.status, 200);

const table = await send('GET', `/tables/${code}`);
check('the table holds both', table.body.characters.map((c) => c.name), ['Gnibbo', 'Tarrow']);
check('and remembers the damage', table.body.characters[0].currentHitPoints, 52);
check('and the two surviving effects', table.body.characters[0].effects.map((e) => e.name).sort(),
  ['Courageous Anthem', 'Heroism']);

const junk = await send('POST', `/tables/${code}/characters`, { pathbuilder: 'paste your character here' });
check('a bad paste is a 400', junk.status, 400);
check('and reads as a sentence', junk.body.title.includes('Pathbuilder'), true);

const badCode = await send('POST', '/tables/no/characters', { pathbuilder: readFileSync(fixture, 'utf8') });
check('a code that is not a code is refused before the parser', badCode.status, 400);
const noSelector = await send('PUT', `/tables/${code}/characters/${id}/effects/${crypto.randomUUID()}`,
  { effect: { name: 'Nothing', kind: 'Custom', key: null, value: 0, duration: null,
              modifiers: [{ type: 'Status', value: 1, applies: [] }] } });
check('a modifier that applies to nothing is refused', noSelector.status, 400);
check('and the refusal names the field', noSelector.body.errors.map((e) => e.field), ['Effect.Modifiers[0].Applies']);

const unknown = await send('GET', '/tables/NOBODY');
check('an unknown code is a table waiting to start', [unknown.status, unknown.body.exists], [200, false]);


await new Promise((resolve) => setTimeout(resolve, 600));
const changed = pushes.filter((p) => p.target === 'CharacterChanged');
// Ten writes, counting the empty slot emptied again, which answers with the same state.
check('the hub pushed every change to the group', changed.length, 10);
check('the last push carries the whole sheet', changed.at(-1).arguments[0].name, 'Tarrow');
check('a push carries a breakdown, not an identifier', changed.at(-1).arguments[0].armorClass.total, 25);

// The races go last, because they write far more than they read and every count above would
// have to know how many. design/004 makes hit points a delta so two people applying damage at
// once sum. A delta on the wire is necessary and not sufficient, and only real parallelism at
// this layer tells the two apart: a handler that reads, adds and writes back passes every
// sequential check ever written.
const race = await send('POST', `/tables/${code}/characters`, { pathbuilder: readFileSync(fixture, 'utf8') });
const racer = race.body.id;
const blows = 20;
const answers = await Promise.all(Array.from({ length: blows }, () =>
  send('POST', `/tables/${code}/characters/${racer}/hit-points`, { delta: -1 })));
check('every concurrent delta is answered', answers.every((a) => a.status === 200), true);
const afterRace = await send('GET', `/tables/${code}`);
check('twenty concurrent deltas all land',
  afterRace.body.characters.find((c) => c.id === racer).currentHitPoints,
  race.body.currentHitPoints - blows);

// Doing that arithmetic in the database leaves the instance the handler holds stale, so the row
// can be right while the answer is wrong. Reading the table back cannot see that. Comparing the
// answer to the table can.
const one = await send('POST', `/tables/${code}/characters/${racer}/hit-points`, { delta: -1 });
const fresh = await send('GET', `/tables/${code}`);
check('a command answers with the number the table holds',
  one.body.currentHitPoints, fresh.body.characters.find((c) => c.id === racer).currentHitPoints);

// The slot id is the client's so a retry converges. A retry is what arrives twice at once, and
// a find-then-insert lets both requests see the slot empty and both insert the same key.
const retriedSlot = crypto.randomUUID();
const retriedSpec = { name: 'Clumsy', kind: 'Seeded', key: 'clumsy', value: 2, duration: null, modifiers: [] };
const retries = await Promise.all(Array.from({ length: 12 }, () =>
  send('PUT', `/tables/${code}/characters/${racer}/effects/${retriedSlot}`, { effect: retriedSpec })));
check('a retried apply never answers with a fault', retries.every((r) => r.status === 200), true);
check('and fills the slot once, not twelve times',
  retries.at(-1).body.effects.filter((e) => e.id === retriedSlot).length, 1);

hub.close();
console.log(failures === 0 ? '\nall checks passed' : `\n${failures} failed`);
process.exit(failures);
