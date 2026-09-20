// Every check in this folder, in one command.
//
//   node tools/ui-check/verify-all.mjs            everything
//   node tools/ui-check/verify-all.mjs camp modes  only those
//   node tools/ui-check/verify-all.mjs --quick     everything that takes under a minute
//
// Exit code is the number of checks that failed, not the number of assertions.
//
// Written because there was no way to ask whether the tree was green. Nineteen files each with
// its own arguments meant everybody ran the four they remembered, and two checks sat broken for
// hours without anybody noticing: verify-sliders.mjs was driving about:blank after a signature
// change, and verify-modes.mjs was failing on a clock that belonged to another run's campaign.
// Both were found by accident. A list that forgets a check is the same problem one layer up, so
// the list is read off the directory rather than typed out, and a file nobody has classified
// stops this rather than being skipped quietly.
import { spawn } from 'node:child_process';
import { readdirSync } from 'node:fs';
import { resolve } from 'node:path';

const here = resolve('tools/ui-check');
const client = process.env.CLIENT_URL ?? 'http://localhost:5173';
const api = process.env.API_URL ?? 'http://localhost:5092';

// What each check needs, and roughly what it costs. "slow" is over a minute and is what --quick
// leaves out; nothing here is optional in a full run.
//
// No addresses are passed. Every check already defaults to localhost:5173 and 5092, and they do
// not agree on the shape: some build "${client}campaign" and want the trailing slash, others
// build "${client}/campaign" and must not have it. Handing each one a URL meant knowing which,
// and the first run of this file navigated to an invalid one. Set CLIENT_URL or API_URL to point
// a check somewhere else and it is passed through as typed.
const KNOWN = {
  account: { needs: 'servers' },
  boot: { needs: 'build', slow: true },
  camp: { needs: 'servers' },
  campaign: { needs: 'servers' },
  campaigns: { needs: 'servers' },
  client: { needs: 'servers' },
  concurrency: { needs: 'servers' },
  effects: { needs: 'servers' },
  'link-import': { needs: 'servers' },
  links: { needs: 'servers' },
  mobs: { needs: 'servers' },
  modes: { needs: 'servers' },
  party: { needs: 'servers' },
  'party-into-fight': { needs: 'servers' },
  records: { needs: 'servers' },
  reload: { needs: 'servers' },
  renewal: { needs: 'build', slow: true },
  'scoped-css': { needs: 'nothing' },
  sliders: { needs: 'gallery', args: [resolve('src/Pf2e.Components/gallery/index.html')] },
  walk: { needs: 'servers', slow: true },
};

const addresses = [process.env.CLIENT_URL, process.env.API_URL].filter(Boolean);

const asked = process.argv.slice(2).filter((a) => !a.startsWith('--'));
const quick = process.argv.includes('--quick');

const found = readdirSync(here)
  .filter((f) => f.startsWith('verify-') && f.endsWith('.mjs') && f !== 'verify-all.mjs')
  .map((f) => f.slice('verify-'.length, -'.mjs'.length));

const unclassified = found.filter((name) => !(name in KNOWN));
if (unclassified.length > 0) {
  console.log(`These checks are not in this file's list, so it cannot say how to run them:
  ${unclassified.join(', ')}

Add them to KNOWN in tools/ui-check/verify-all.mjs. Skipping them quietly is how a check
stops being run at all.`);
  process.exit(1);
}

const wanted = found
  .filter((name) => (asked.length === 0 ? true : asked.includes(name)))
  .filter((name) => !(quick && KNOWN[name].slow));

if (asked.length > 0) {
  const unknown = asked.filter((name) => !found.includes(name));
  if (unknown.length > 0) {
    console.log(`No such check: ${unknown.join(', ')}`);
    process.exit(1);
  }
}

const reachable = async (url) => {
  try {
    const answer = await fetch(url, { signal: AbortSignal.timeout(2000) });
    return answer.status > 0;
  } catch {
    return false;
  }
};

// Named before anything runs, because nineteen checks failing on a server that is not up reads
// like nineteen defects.
if (wanted.some((name) => KNOWN[name].needs === 'servers')) {
  const [apiUp, clientUp] = await Promise.all([reachable(`${api}/health`), reachable(client)]);
  if (!apiUp || !clientUp) {
    console.log(`Not running: ${[!apiUp && api, !clientUp && client].filter(Boolean).join(' and ')} `
      + `${!apiUp && !clientUp ? 'are' : 'is'} not answering. Start both with run.cmd.`);
    process.exit(1);
  }
}

const run = (name) => new Promise((done) => {
  const started = Date.now();
  const args = KNOWN[name].args ?? (KNOWN[name].needs === 'servers' ? addresses : []);
  const child = spawn('node', [resolve(here, `verify-${name}.mjs`), ...args], {
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  let output = '';
  child.stdout.on('data', (d) => (output += d));
  child.stderr.on('data', (d) => (output += d));
  child.on('error', (failure) => done({ code: 1, output: failure.message, seconds: 0 }));
  child.on('close', (code) =>
    done({ code: code ?? 1, output, seconds: Math.round((Date.now() - started) / 1000) }));
});

let failed = 0;
const trouble = [];

for (const name of wanted) {
  process.stdout.write(`${name.padEnd(18)}`);
  const { code, output, seconds } = await run(name);

  // Every check in this folder exits with its own failure count, so a non-zero code is the
  // number of assertions that failed rather than a crash. Zero with no output is not a pass.
  if (code === 0) {
    console.log(`ok    ${seconds}s`);
  } else {
    failed++;
    console.log(`FAIL  ${seconds}s  ${code} failure(s)`);
    trouble.push([name, output]);
  }
}

for (const [name, output] of trouble) {
  console.log(`\n---- ${name} ----`);
  console.log(output.split('\n').filter((line) => /FAIL|Error|error/.test(line)).slice(0, 12).join('\n')
    || output.slice(-600));
}

console.log(`\n${wanted.length} check(s), ${failed} failing`);
process.exit(failed);
