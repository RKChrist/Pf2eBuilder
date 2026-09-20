// A secret nobody set must stop the process, and say which line to add.
//
//   node tools/ui-check/verify-boot.mjs [port]
//
// Exit code is the number of failures. Default port 5199, never 5092: the dev server and the
// dev database belong to whoever is already running them.
//
// This is the one thing in the auth design that cannot be asserted from inside a test host. A
// test host is handed a signing key because it needs one to be useful, so it can prove what a
// configured process does and never what an unconfigured one does. The failure this guards is a
// deployment that starts without Auth:Jwt:SigningKey, signs sessions with whatever it found, and
// signs everybody out again at the next restart. Refusing to start is only half of it: the
// operator staring at a crashed boot log has to be given the name of the setting, because
// "options validation failed" sends them reading source instead.
//
// Development is deliberately exempt and so is deliberately not what this runs. Program.cs
// generates a throwaway key there, so a check that booted in Development would pass while the
// thing it is about was switched off.
import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { reporter } from './cdp.mjs';

const port = Number(process.argv[2] ?? 5199);
const check = reporter();
const scratch = mkdtempSync(join(tmpdir(), 'pf2e-boot-'));

const project = 'src/Pf2e.Api';
const binary = resolve(
  project, 'bin/Debug/net10.0',
  process.platform === 'win32' ? 'Pf2e.Api.exe' : 'Pf2e.Api',
);

// Nothing here is about the database or the ruleset, and seeding one would add twenty seconds to
// a check about a string. The temp file also keeps this away from the database the dev server
// has open.
const quiet = {
  ASPNETCORE_ENVIRONMENT: 'Production',
  ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
  Database__ConnectionString: `Data Source=${join(scratch, 'boot.db')}`,
  Database__MigrateOnStartup: 'false',
  Seeding__Enabled: 'false',
};

const run = (command, args, options) => new Promise((done) => {
  const child = spawn(command, args, options);
  let output = '';
  child.stdout.on('data', (d) => (output += d));
  child.stderr.on('data', (d) => (output += d));
  child.on('close', (code) => done({ code, output }));
});

// Built once up front so a compile error cannot be mistaken for the refusal this is looking for:
// both leave a non-zero exit code behind and only one of them is the point.
const built = await run('dotnet', ['build', project, '-v', 'q', '--nologo'], { shell: true });
check('the api builds', built.code === 0, built.output.slice(-300));

/**
 * Runs the built binary until it says something worth knowing, then kills it. The binary rather
 * than `dotnet run`, because that wrapper starts the app as a child of itself and killing the
 * wrapper would leave the app holding the port. Nothing here waits on a timer for success: a
 * slow machine must not read as a refusal.
 */
const boot = (env) => new Promise((done) => {
  const child = spawn(binary, [], {
    // The content root, so appsettings.json is read from where a real run reads it.
    cwd: resolve(project),
    env: { ...process.env, ...env },
  });

  let output = '';
  let listening = false;
  const watch = (data) => {
    output += data;
    if (!listening && output.includes('Now listening')) {
      listening = true;
      child.kill();
    }
  };

  child.stdout.on('data', watch);
  child.stderr.on('data', watch);

  const giveUp = setTimeout(() => child.kill(), 60000);
  child.on('close', (code) => {
    clearTimeout(giveUp);
    done({ code, output, listening });
  });
});

const without = await boot({ ...quiet, Auth__Jwt__SigningKey: '' });
check('no signing key stops the process', without.code !== 0 && !without.listening,
  `exit ${without.code}, listening ${without.listening}`);
check('and the refusal names the setting to add', without.output.includes('Auth:Jwt:SigningKey'),
  without.output.slice(-400));

const withKey = await boot({
  ...quiet,
  Auth__Jwt__SigningKey: 'a-boot-check-signing-key-of-at-least-32-characters',
});
check('a signing key lets the process start', withKey.listening, withKey.output.slice(-400));

// The failure that would slip past both checks above: a key short enough that HMAC-SHA256
// refuses it would start, then throw on the first sign-in, which is a much worse place to find
// out.
const tooShort = await boot({ ...quiet, Auth__Jwt__SigningKey: 'too-short' });
check('a key too short to sign with stops the process too',
  tooShort.code !== 0 && !tooShort.listening, `exit ${tooShort.code}`);
check('and that refusal names the setting as well',
  tooShort.output.includes('Auth:Jwt:SigningKey'), tooShort.output.slice(-400));

rmSync(scratch, { recursive: true, force: true });
process.exit(check.done());
