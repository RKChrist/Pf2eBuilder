// A sign-in round trip, through a real browser, across the origin the client actually sits on.
//
//   node tools/ui-check/verify-account.mjs [client] [api]
//
// Exit code is the number of failures.
//
// The plan's phase 3 is green when "a sign-in round trip sets and reads the auth cookie", and
// the decision behind it (design/012) is that the browser must never hold a readable token. Both
// halves are asserted here: the session works, and script cannot see what carries it.
//
// Driven from the client origin rather than by curl, because the cookie has to survive a
// cross-origin request with credentials, which is the arrangement that actually ships: the
// client is a separate deployable and the API is somewhere else.
import { launch, openPage, reporter, sleep } from './cdp.mjs';

const client = process.argv[2] ?? 'http://localhost:5173';
const api = process.argv[3] ?? 'http://localhost:5092';

const check = reporter();
const browser = await launch({ headless: process.env.HEADED !== '1' });

const person = async ({ isolated = false } = {}) => {
  const page = await openPage(browser, { isolated });
  await page.viewport(390, 844, true);
  await page.goto(client);
  for (let i = 0; i < 100; i++) {
    if (await page.eval(`!!document.querySelector('.pf-bottomnav__item')`)) break;
    await sleep(200);
  }
  return page;
};

// Every call goes out with credentials, which is the only way a cookie set by another origin
// comes back. A test that forgot this would pass on same-origin and fail the day it shipped.
const call = (page, method, path, body) => page.eval(`(async () => {
  const answer = await fetch(${JSON.stringify(api)} + ${JSON.stringify(path)}, {
    method: ${JSON.stringify(method)},
    credentials: 'include',
    ...(${JSON.stringify(body ?? null)} === null ? {} : {
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(${JSON.stringify(body ?? null)}),
    }),
  });
  const text = await answer.text();
  let parsed = null;
  try { parsed = text.length > 0 ? JSON.parse(text) : null; } catch { parsed = null; }
  return JSON.stringify({ status: answer.status, body: parsed, text: text.slice(0, 300) });
})()`).then((raw) => JSON.parse(raw));

const email = `gm-${Date.now()}@example.test`;
const password = 'a rope of onions';

const me = await person();

const registered = await call(me, 'POST', '/accounts', {
  email,
  displayName: 'Gnibbo',
  password,
});
check.eq('registering answers created', registered.status, 201, registered.text);
check('and says who it made', registered.body?.displayName === 'Gnibbo', registered.text);
check('with the email it was given, lower-cased',
  registered.body?.email === email.toLowerCase(), String(registered.body?.email));

// The whole point of the ticket. If script can read this, the token inside it is in the page.
const readable = await me.eval(`document.cookie`);
check('no part of the session is readable by script',
  !readable.includes('pf2e.auth') && !readable.includes('eyJ'), readable.slice(0, 200) || '(empty)');

const mine = await call(me, 'GET', '/accounts/me');
check.eq('the session comes back on the next request', mine.status, 200, mine.text);
check('as the same account', mine.body?.id === registered.body?.id,
  `${registered.body?.id} -> ${mine.body?.id}`);

// A second device is a second browser context. Sharing one would make every check above pass
// against a session it never established.
const stranger = await person({ isolated: true });
const theirs = await call(stranger, 'GET', '/accounts/me');
check.eq('a browser that never signed in has no session', theirs.status, 401, theirs.text);

const taken = await call(stranger, 'POST', '/accounts', {
  email,
  displayName: 'Somebody else',
  password: 'a different rope entirely',
});
check.eq('an email that is already an account is refused', taken.status, 409, taken.text);

// The two refusals have to read the same. Two different sentences say which addresses exist.
const wrongPassword = await call(stranger, 'POST', '/accounts/session', {
  email,
  password: 'not the password',
});
const noSuchAccount = await call(stranger, 'POST', '/accounts/session', {
  email: `nobody-${Date.now()}@example.test`,
  password,
});
check.eq('a wrong password is refused', wrongPassword.status, 401, wrongPassword.text);
check.eq('an unknown account is refused the same way', noSuchAccount.status, 401, noSuchAccount.text);
check('and both say exactly the same thing',
  JSON.stringify(wrongPassword.body) === JSON.stringify(noSuchAccount.body),
  `${wrongPassword.text} | ${noSuchAccount.text}`);

const back = await call(stranger, 'POST', '/accounts/session', { email, password });
check.eq('the right password signs a second browser in', back.status, 200, back.text);
check('as the account that was registered', back.body?.id === registered.body?.id,
  `${registered.body?.id} -> ${back.body?.id}`);

const out = await call(stranger, 'DELETE', '/accounts/session');
check.eq('signing out answers no content', out.status, 204, out.text);
const after = await call(stranger, 'GET', '/accounts/me');
check.eq('and the session is gone', after.status, 401, after.text);

// The first browser was never signed out, and one browser's sign-out must not reach another's.
const stillMine = await call(me, 'GET', '/accounts/me');
check.eq('the other browser is still signed in', stillMine.status, 200, stillMine.text);

const errors = [...me.consoleErrors(), ...stranger.consoleErrors()]
  .filter((e) => !e.includes('ERR_BLOCKED_BY_CLIENT'))
  // A 401 and a 409 are the answers this file asks for, and Chrome logs every one of them.
  .filter((e) => !e.includes('401') && !e.includes('409'));
check('no console errors beyond the refusals asked for', errors.length === 0,
  errors.join(' | ').slice(0, 300));

await browser.close();
process.exit(check.done());
