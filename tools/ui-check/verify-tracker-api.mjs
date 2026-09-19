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

const unknown = await send('GET', '/tables/NOBODY');
check('an unknown code is a table waiting to start', [unknown.status, unknown.body.exists], [200, false]);

await new Promise((resolve) => setTimeout(resolve, 600));
const changed = pushes.filter((p) => p.target === 'CharacterChanged');
// Ten writes, counting the empty slot emptied again, which answers with the same state.
check('the hub pushed every change to the group', changed.length, 10);
check('the last push carries the whole sheet', changed.at(-1).arguments[0].name, 'Tarrow');
check('a push carries a breakdown, not an identifier', changed.at(-1).arguments[0].armorClass.total, 25);

hub.close();
console.log(failures === 0 ? '\nall checks passed' : `\n${failures} failed`);
process.exit(failures);
