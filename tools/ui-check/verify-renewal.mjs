// A session has to outlive the token inside it, or a GM is signed out mid-fight.
//
//   node tools/ui-check/verify-renewal.mjs [port]
//
// Exit code is the number of failures. Default port 5199, never 5092: the dev server and the dev
// database belong to whoever is already running them. Takes about a minute, which is why it is
// not in the loop that runs after every change.
//
// The unit tests decide when a token should be re-minted. What they cannot show is that the
// decision is wired to anything: that OnValidatePrincipal is registered, that the renewed ticket
// is written back into a Set-Cookie, and that a browser carries the new one afterwards. That is
// what this drives, against a real API configured with a one-minute token, so the last half of a
// token's life arrives in thirty seconds instead of half an hour.
//
// Before this existed the token was minted once at sign-in and never again, so a session ended
// after Auth:Jwt:AccessTokenMinutes however long Auth:Cookie:ExpireMinutes claimed, and
// SlidingExpiration renewed an envelope around a credential that was already dead.
import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { launch, openPage, reporter, sleep } from './cdp.mjs';

const port = Number(process.argv[2] ?? 5199);
const api = `http://127.0.0.1:${port}`;
const check = reporter();
const scratch = mkdtempSync(join(tmpdir(), 'pf2e-renewal-'));

const binary = resolve(
  'src/Pf2e.Api/bin/Debug/net10.0',
  process.platform === 'win32' ? 'Pf2e.Api.exe' : 'Pf2e.Api',
);

// A minute of token against two minutes of cookie, so the cookie cannot be what ends the
// session and anything that goes wrong here is the token's doing.
const server = spawn(binary, [], {
  // From the binary's own folder, because a content root elsewhere is a process with no
  // appsettings.json, which fails on a required setting that has nothing to do with this.
  cwd: resolve('src/Pf2e.Api/bin/Debug/net10.0'),
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Production',
    ASPNETCORE_URLS: api,
    Database__ConnectionString: `Data Source=${join(scratch, 'renewal.db')}`,
    Database__MigrateOnStartup: 'true',
    Seeding__Enabled: 'false',
    Cors__AllowedOrigins__0: api,
    Auth__Jwt__SigningKey: 'a signing key long enough to sign with',
    Auth__Jwt__AccessTokenMinutes: '1',
    Auth__Jwt__RefreshTokenMinutes: '60',
    Auth__Cookie__ExpireMinutes: '2',

    // No skew. The default thirty seconds would keep a one-minute token acceptable until
    // ninety, and the last check below would pass without renewal doing anything at all: the
    // first run of this file did exactly that.
    Auth__Jwt__ClockSkewSeconds: '0',
  },
});

let log = '';
server.stdout.on('data', (d) => (log += d));
server.stderr.on('data', (d) => (log += d));

const up = async () => {
  for (let i = 0; i < 120; i++) {
    try {
      const answer = await fetch(`${api}/health`);
      if (answer.ok) return true;
    } catch {
      // Not listening yet.
    }
    await sleep(500);
  }
  return false;
};

const stop = async () => {
  server.kill();

  // The process still has the database open for a moment after kill returns, and a temp
  // directory left behind is not a failure of the thing being checked.
  await sleep(1500);
  try {
    rmSync(scratch, { recursive: true, force: true });
  } catch {
    // Windows will release it eventually.
  }
};

if (!(await up())) {
  check('the api came up', false, log.slice(-400));
  await stop();
  process.exit(check.done());
}
check('the api came up', true, `one-minute tokens on ${port}`);

const browser = await launch({ headless: process.env.HEADED !== '1' });
const page = await openPage(browser, { isolated: true });

// Served from the API's own origin, because this run has no client process and the page only
// has to be somewhere a cookie for this origin can live.
await page.goto(`${api}/health`);

// Network, not Storage: Storage.getCookies reads the default browser context and this page has
// one of its own, so it answered "no cookie" for a browser that was holding one. Two checks
// below then compared nothing against nothing and passed on it.
await page.send('Network.enable', {});

const call = (method, path, body) => page.eval(`(async () => {
  const answer = await fetch(${JSON.stringify(api)} + ${JSON.stringify(path)}, {
    method: ${JSON.stringify(method)},
    credentials: 'include',
    ...(${JSON.stringify(body ?? null)} === null ? {} : {
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(${JSON.stringify(body ?? null)}),
    }),
  });
  const text = await answer.text();
  return JSON.stringify({ status: answer.status, text: text.slice(0, 200) });
})()`).then((raw) => JSON.parse(raw));

// The cookie is HttpOnly, so this is the debugger reading it rather than the page. Its value is
// the encrypted ticket: when the token inside is re-minted the ticket is rewritten, so a changed
// value is the renewal arriving at the browser.
const ticket = async () => {
  const { cookies } = await page.send('Network.getCookies', { urls: [api] });
  return cookies.find((c) => c.name === 'pf2e.auth')?.value ?? null;
};

const email = `renewal-${Date.now()}@example.test`;
const registered = await call('POST', '/accounts', {
  email,
  displayName: 'Vesk',
  password: 'a rope of onions',
});
check.eq('a session starts', registered.status, 201, registered.text);

const first = await ticket();
check('and the browser is holding a ticket it cannot read', first !== null && first.length > 0,
  first ? `${first.length} characters` : 'nothing');

// Inside the first half of the token's life nothing should be rewritten. A re-mint on every
// request would make the token's stated lifetime meaningless by never letting one get old.
await sleep(5000);
const early = await call('GET', '/accounts/session');
check('a request early in the token holds the session', early.text.includes('Vesk'), early.text);
const unchanged = await ticket();
check('and does not rewrite the ticket', unchanged !== null && unchanged === first, 'unchanged');

// Past halfway, where renewal is supposed to happen.
await sleep(32000);
const renewed = await call('GET', '/accounts/session');
check('a request past halfway still holds the session', renewed.text.includes('Vesk'), renewed.text);

const second = await ticket();
check('and the ticket was rewritten, so the token was re-minted',
  second !== null && second !== first, second === first ? 'unchanged' : 'changed');

// The point of the whole thing. One minute after sign-in the original token has expired, and
// without renewal this is where the GM was signed out.
await sleep(30000);
const after = await call('GET', '/accounts/session');
check('and past the first token\'s own expiry the session is still there',
  after.status === 200 && after.text.includes('Vesk'), after.text);

// This page is an API origin with no site on it, so the browser asks it for a favicon it does
// not serve and Chrome logs the 404. Every call above asserts its own status, so nothing here
// rests on a 404 being caught by this line.
const errors = page.consoleErrors()
  .filter((e) => !e.includes('ERR_BLOCKED_BY_CLIENT') && !e.includes('404'));
check('no console errors', errors.length === 0, errors.join(' | ').slice(0, 200));

await browser.close();
await stop();
process.exit(check.done());
