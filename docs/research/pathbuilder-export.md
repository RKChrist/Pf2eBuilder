# The Pathbuilder export, and why Demiplane is a dead end

Researched 18 September 2026. Every claim below was checked against a live endpoint or a
primary source on that date. Where a finding is second-hand it says so.

## Answer in one paragraph

Pathbuilder's JSON export is an undocumented, unversioned, best-effort convenience that one
developer built for one downstream consumer in 2020 and has maintained since by talking to
importer authors directly. The URL shape has been stable for years and the payload drifts
slowly, so the format is workable. The transport is the real risk: it sits behind Cloudflare
bot management that treats server-side fetchers as attackers, and it has been disabled
outright at least twice in 2026. Demiplane, by contrast, has no inbound path at all. Nothing,
official or unofficial, imports a foreign build into Demiplane.

## The endpoint

`https://pathbuilder2e.com/json.php?id=NNNNNN`. No auth, no key, no cookie.
`access-control-allow-origin: *`. A valid id returns `{"success":true,"build":{...}}`, an
unknown id returns HTTP 404 with `{"success":false,"error":"Build not found."}`, and a
malformed one returns HTTP 400 with `"Invalid ID."`.

It is documented nowhere. The homepage never mentions JSON. `/faq.html`, `/faq.php`, `/faq`
and `/jsonhelp.html` all return a plain Apache 404. The only official prose lives inside the
app bundle itself.

## Five constraints that change how we build the importer

**1. The payload has no version field.** The envelope is `{"success", "build"}` and nothing
else. No `version`, `schema`, `format`, `revision`, `generated`, `timestamp` or `exportDate`.
There is nothing to branch on.

**2. Stored records keep the shape they had when written.** Two live exports fetched 35
seconds apart differ. `id=123456` has no `dualClass`, `xp`, `sizeName`, `rituals`,
`resistances` or `inventorMods`. `id=448265` has all six. An importer does not meet one
schema, it meets a mix of vintages. Every field must be optional.

**3. A server-side fetch gets blocked.** Cloudflare bot management returns 403 with
`Cf-Mitigated: challenge` to a default curl user agent, and 429 on a second request in the
same second. A browser-shaped request with ~30 seconds between calls works. RPG Sage's code
assumes 10 requests per minute per IP, which is one maintainer's belief and documented nowhere
upstream. **The fetch has to happen in the player's browser, not on our API.**

**4. The JSON ID is a mutable slot, not a character identity.** The app stores a single
`prefJsonID` and overwrites it on each export. The developer's own tracker carries a user
report: "I have several new characters that all share the same JSON ID. Trying to import
changes into Foundry brought up an entirely different character." Never key a character on it.

**5. The user must export first, and the JSON ID is not the character ID.** The id only exists
after the player runs Export JSON in the app. Confusing the two numbers is the single most
common support question downstream.

## The transport keeps failing

- 26 Feb 2026: a silent hostname change from `www.pathbuilder2e.com` to `pathbuilder2e.com`
  broke every consumer on the old host. The fix in Pathmuncher is a one-line diff.
- 7 Sep 2026: exports broken across mobile and web, still open at the time of research. The
  maintainer's answer was that the Pathbuilder server was under attack and the function had
  been disabled.
- Outage posts on the author's Patreon on 16 Feb, 11 May and 31 Aug 2026, the last saying
  Cloudflare under-attack mode means "sharing json will not work".
- One consumer, RPG Sage, gave up on server-side fetching entirely and now tells players to
  open the URL in a browser, copy the text into a `.json` file and attach it.

## Format drift is slow but unannounced

Pathmuncher's changelog keys to the Pathbuilder app version, because the payload offers nothing
else to key on. Real entries include "Changes for focus spell move in v65", "Support new
prepared spells in Pathbuilder v66 export" and "rituals will now be added" for v67. The v66
patch note reads "Focus Spell total has moved" in a single clause with no deprecation window.
No structural break was reported between roughly May 2024 and February 2026.

The format also has gaps it simply does not cover, recorded by its consumers: shield grade is
not exported, and prepared spells are not distinguished from spells known.

## It is the de facto standard by adoption, not by agreement

The string `pathbuilder2e.com/json.php` appears in 38 files across roughly 30 repositories,
including Wanderer's Guide, RPG Sage, Pathmuncher and Kobold. A dozen projects independently
reverse-engineering the same types is the signature of an undocumented format.

Correcting a premise we had wrong: the Foundry VTT PF2e system contains **no** Pathbuilder
importer. Zero paths match "pathbuilder" on its default branch. Consumption was deliberately
pushed out to third-party modules, and the live one is Pathmuncher. Its fetch has no version
check and no try/catch; the only guard is the success boolean.

## Demiplane has no inbound path

- The Zendesk helpdesk returns **count 0** for "pathbuilder" and 0 for "wanderer". A full
  enumeration of all 48 articles found nothing about importing or exporting a character.
- The official PF2e Nexus FAQ never mentions import or export in any form.
- The official export is a PDF, announced August 2024. A community project exists purely to
  parse that PDF back into Foundry, which tells you there was no structured export.
- The Roll20 integration is an embedded sheet: "No exporting or rebuilding."
- An undocumented GraphQL API exists at `apiv4.demiplane.com/v1/graphql` with an
  `updateCharacterV2` mutation, but **no `createCharacter`**, and it needs a Hasura JWT
  extracted from an authenticated browser session. The Foundry module built on it defaults to
  read-only and states that a brand-new item is never sent to Demiplane.

So you can push play state onto a character Demiplane already built. You cannot push a build.

## There is no shared interchange format

Paizo has published no character data standard, only printable PDFs. Foundry's actor JSON is
one system's internal model tied to its own compendium ids. Hero Lab Online has a published
spec that is HLO-specific and token-gated. Wanderer's Guide defines FTC, "a universal file
structure for Pathfinder 2e and Starfinder 2e characters", and it is more governed than
Pathbuilder in that it carries `version: '1.0'`, but a GitHub search finds it in Wanderer's
Guide and a fork of Wanderer's Guide.

The direction of travel is one way. Foundry and Roll20 import Pathbuilder, Wanderer's Guide
and Hero Lab Online. Demiplane is a sink.

## What could not be established

Whether the JSON ID expires. A three-year-old id still resolved on the day of research, which
is strong evidence against expiry but is one observation and no policy exists. Whether the
server mints a new id or overwrites the row on re-export; reading the client is not observing
the server, and probing would have meant writing data. Whether the 10-per-minute figure is
real. Whether Demiplane's live UI hides a beta import control, since the builder is
login-gated. Whether Demiplane's terms prohibit third-party API use; nobody fetched them.

Cloudflare's edge behaviour was inconsistent between requests during research. Treat any single
observation of it as a sample rather than a rule.
