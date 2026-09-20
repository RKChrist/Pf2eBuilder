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

const mine = await call(me, 'GET', '/accounts/session');
check.eq('the session comes back on the next request', mine.status, 200, mine.text);
check('as the same account', mine.body?.account?.id === registered.body?.id,
  `${registered.body?.id} -> ${mine.body?.account?.id}`);

// A second device is a second browser context. Sharing one would make every check above pass
// against a session it never established.
const stranger = await person({ isolated: true });
// An answer, not a refusal. Asking who you are is not something to be denied, and a 401 here
// would print a console error on every screen of the app for every signed-out visitor.
const theirs = await call(stranger, 'GET', '/accounts/session');
check.eq('asking who a browser is always answers', theirs.status, 200, theirs.text);
check('and a browser that never signed in is nobody', theirs.body?.account === null, theirs.text);

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
const after = await call(stranger, 'GET', '/accounts/session');
check('and the session is gone', after.status === 200 && after.body?.account === null, after.text);

// The first browser was never signed out, and one browser's sign-out must not reach another's.
const stillMine = await call(me, 'GET', '/accounts/session');
check('the other browser is still signed in',
  stillMine.body?.account?.id === registered.body?.id, stillMine.text);

// The screen. Everything above went through fetch; none of it proves a person can do it.
const screen = await person({ isolated: true });
const typed = `gm-${Date.now()}-screen@example.test`;

const wait = async (page, selector, ms = 20000) => {
  const deadline = Date.now() + ms;
  while (Date.now() < deadline) {
    if (await page.eval(`!!document.querySelector(${JSON.stringify(selector)})`)) return true;
    await sleep(150);
  }
  return false;
};

const press = async (page, text) => {
  const hit = await page.eval(`(() => {
    const el = [...document.querySelectorAll('button')]
      .find(b => b.textContent.trim() === ${JSON.stringify(text)});
    if (!el || el.disabled) return false;
    el.scrollIntoView({ block: 'center' });
    el.click();
    return true;
  })()`);
  await sleep(700);
  return hit;
};

// By the label, because the fields differ between the two arms of this form and their order is
// not a contract.
const fill = (page, label, value) => page.eval(`(() => {
  const field = [...document.querySelectorAll('.pf-field')]
    .find(f => f.querySelector('.pf-field__label')?.textContent.trim() === ${JSON.stringify(label)});
  const input = field?.querySelector('.pf-input');
  if (!input) return false;
  Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(input, ${JSON.stringify(value)});
  for (const t of ['input', 'change']) input.dispatchEvent(new Event(t, { bubbles: true }));
  return true;
})()`);

await screen.goto(`${client}/account`);
check('the account screen is reachable by its own address', await wait(screen, '.account__form'));
check('and says signing in is optional', await screen.eval(
  `document.body.innerText.includes('Optional')`));
check('the header offers a way in before anybody has one',
  await screen.eval(`document.querySelector('.who')?.textContent.trim()`)
    .then(text => text === 'Sign in'),
  await screen.eval(`document.querySelector('.who')?.textContent.trim() ?? 'nothing'`));

const labels = (page) => page.eval(
  `[...document.querySelectorAll('.pf-field__label')].map(e => e.textContent.trim()).join()`);

check('signing in asks for two things and not three',
  await labels(screen) === 'Email,Password', await labels(screen));
check('the form offers a way to make one', await press(screen, 'New here? Create an account'));
check('and making an account asks for a name as well',
  await labels(screen) === 'Email,Name,Password', await labels(screen));
check('the password field hides what is typed in it', await screen.eval(
  `!!document.querySelector('.pf-input[type="password"]')`));

await fill(screen, 'Email', typed);
await fill(screen, 'Name', 'Tarrow of the Vale');
await fill(screen, 'Password', 'a rope of onions');
await sleep(300);
check('creating the account from the screen works', await press(screen, 'Create the account'));
check('and the header says who you are',
  await wait(screen, '.who--in') &&
  await screen.eval(`document.querySelector('.who__name')?.textContent.trim()`) === 'Tarrow',
  await screen.eval(`document.querySelector('.who')?.textContent.trim() ?? 'nothing'`));

// The part that makes an account worth having over a browser that remembers something.
await screen.goto(`${client}/account`);
check('a reload comes back signed in', await wait(screen, '.who--in'),
  'the cookie outlived the page');

check('signing out from the screen works', await press(screen, 'Sign out'));
check('and the header goes back to offering the way in',
  await wait(screen, '.account__form') &&
  !(await screen.eval(`!!document.querySelector('.who--in')`)));

await fill(screen, 'Email', typed);
await fill(screen, 'Password', 'a rope of onions');
await sleep(300);
check('and the account signs back in', await press(screen, 'Sign in') && await wait(screen, '.who--in'));

const errors = [...me.consoleErrors(), ...stranger.consoleErrors(), ...screen.consoleErrors()]
  .filter((e) => !e.includes('ERR_BLOCKED_BY_CLIENT'))
  // A 401 and a 409 are the answers this file asks for, and Chrome logs every one of them.
  .filter((e) => !e.includes('401') && !e.includes('409'));
check('no console errors beyond the refusals asked for', errors.length === 0,
  errors.join(' | ').slice(0, 300));

await browser.close();
process.exit(check.done());
