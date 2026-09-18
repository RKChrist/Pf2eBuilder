# Importing characters from Demiplane

Research note, 18 September 2026. Explanation, not a how-to. Nothing here has been built.

## The question

The design brief archived at `Sources/pf2e-table-companion/design-document.md` says two things about
Demiplane. Line 448: "Demiplane has no export." Line 509: "Demiplane means manual entry." On that
basis the project defaulted to Pathbuilder 2e JSON, and `todo.md` carries "Phase 4: Pathbuilder
import" as the only planned importer.

Is that default right, and what is the best available Demiplane path?

## The answer

The brief is wrong on the facts and right on the conclusion, for a reason it did not give. Demiplane
does export: since August 2024 every Pathfinder 2e Nexus character can be exported to a PDF, and that
PDF is not a flat rendering. It carries an AcroForm field layer of 762 named fields whose names are
semantic and stable across game systems, and any PDF library reads them in three lines. A Demiplane
character is therefore machine-readable today without OCR, without an account of ours, and without
touching Demiplane's servers, because the user hands us the file. What Demiplane does not have is a
public API or a JSON export. The GraphQL endpoint its own app uses does return character JSON and
does answer anonymous callers, and one author's client library and Foundry module both use it, but
it is undocumented, needs a bearer token lifted by hand from a logged-in browser session, and
Demiplane staff were still
saying "nothing we can share yet" about a public API in October 2025. Pathbuilder should stay the
default, because it is the sheet most tables already use and its export is richer. Demiplane should
move from "manual entry" to "PDF import, second priority", because the PDF gives names, numbers and
proficiency ranks, which is exactly the shape the brief's importer contract already consumes.

## Evidence

### 1. No machine-readable export, and no public API

Demiplane's own help centre describes exactly one export. The article "Getting Started on Demiplane"
(created 25 June 2025, last edited 25 June 2025, read through the Zendesk REST API at
`https://support.demiplane.com/api/v2/help_center/en-us/articles/33046325857815.json`) says: "Most
NEXUS Platforms have PDF Export > Print option for those who would instead print and play with a
physical copy." The word JSON does not appear in the article. A search of the whole help centre for
"JSON" through `https://support.demiplane.com/api/v2/help_center/articles/search.json?query=JSON`
returns `{"count":0}`.

The Pathfinder 2e Nexus FAQ
(`https://support.demiplane.com/api/v2/help_center/en-us/articles/25811633557399.json`, created
21 August 2024, edited 24 June 2025) never mentions character export, JSON, import or Foundry. Its
single use of the word "API" is about Paizo's store API for the ownership discount: "we can only
connect to what Paizo has 'available' in their store API".

Note that `https://support.demiplane.com/hc/en-us/...` returns HTTP 403 to a plain fetch. The Zendesk
REST API under `/api/v2/help_center/` serves the same articles and answers normally.

On a public API, the primary source is the forum thread
[API support](https://forums.demiplane.com/t/api-support/3474), opened 20 May 2024. Staff replied on
21 May 2024: "We have no news we can share at this moment regarding an API, but when that changes
we'll definitely shout from the rooftops." Staff replied again on 27 October 2025: "Nothing we can
share yet. As soon as we have more that we can say about it we'll make sure and shout it loudly." A
user asked again on 16 January 2026 and no staff reply follows. The older thread
[Is there a Demiplane API](https://forums.demiplane.com/t/is-there-a-demiplane-api/2522) has staff
saying on 8 November 2023 "There is not currently an API, but this is something that we hope to add
on our medium-term roadmap." The 2023 answer settles nothing about 2026. The October 2025 one is
eleven months old and is the most recent primary statement found.

Demiplane's changelog at `https://www.demiplane.com/changelog` cannot be used as evidence either way
for recent features. Its newest dated entry is 11 November 2024 and the page's own publish stamp
reads June 2025. Demiplane's public record of its own changes has been dark for 22 months.

A "Character API" does exist, and it is not for us. The post
[Demiplane x Roll20 Integration](https://www.demiplane.com/blog/demiplane-x-roll20-integration)
(28 January 2025) announces "the first iteration of Demiplane's Character API" with the aim to "bring
your Demiplane characters easily into the space where you play your games". The follow-up
[updated beta roadmap](https://www.demiplane.com/blog/demiplane-x-roll20-integration-update---updated-beta-roadmap)
(10 April 2025) covers the public beta for Pathfinder 2e and six other systems. Neither post offers
third-party access, and `https://pages.roll20.net/roll20-x-demiplane` describes the result as the
Demiplane sheet used inside Roll20 rather than a copy of the data. Roll20 acquired Demiplane in June
2024, which is why this integration is an internal one.

**The undocumented GraphQL endpoint.** `https://apiv4.demiplane.com/v1/graphql` is a Hasura endpoint
that answers unauthenticated callers and leaves schema introspection open. A research subagent probed
it directly today. `{__typename}` returns HTTP 200. The character query fields are `getCharacter`,
`getCharacterCache`, `getCharacterPdf` and several siblings, all returning a `BaseOutput` whose
`result` is a raw `json` scalar, so character state genuinely comes back as JSON. Called anonymously
with an all-zeroes UUID, `getCharacter` returns `{"success":true,"message":"","result":null}` rather
than an auth error. `getCharacterPdf(id: String!, isPregen: Boolean!, templateName: String!)` with a
bogus UUID returns "Character not found". No real user data was requested or returned. I did not
verify myself that a genuinely shared character UUID returns a populated payload to an anonymous
caller; the claim rests on the third-party client's README, not on an observation.

That endpoint is the whole basis of the community tooling in section 3. It has no documentation, no
API keys, no stability promise, and no announcement. Building on it means building on a private
backend that can change or close without notice.

### 2. The PDF is a generated Demiplane layout with a named AcroForm field layer

This is the most useful finding in the note, and the evidence is first-hand.

**It is not the official Paizo sheet.** I downloaded Paizo's current sheet from
`https://downloads.paizo.com/RemasterPlayerCoreCharacterSheet.pdf` today. It is a four-page PDF
version 1.4 containing zero occurrences of `/AcroForm`, `/Widget`, `/Annots` or `/XFA`. It is a
print-only document with no form layer at all. Whatever Demiplane produces, it is not that file
filled in.

**It is a form, not a rendering.** The Foundry module
[Seelenoede/demiplane-pf2e-foundry-importer](https://github.com/Seelenoede/demiplane-pf2e-foundry-importer)
(repo created 30 May 2025, last push 31 May 2025) reads a Demiplane Pathfinder 2e export. Its entire
extraction step, in `scripts/demiplane-import.js`, is three lines:

	const pdf = await pdfJs.getDocument(srcFile).promise;
	const fieldObjects = await pdf.getFieldObjects();
	importCharacter(fieldObjects, targetActor);

`getFieldObjects()` is pdf.js's AcroForm accessor. It returns nothing for a flattened PDF. The repo
also commits `exampleData/pdfData.json`, a 591 KB dump of that call's output for a real level 1
character. I downloaded and analysed it. It holds **762 distinct field names** across 1001 widgets:
584 text fields, 177 checkboxes and 1 button. Each entry carries the pdf.js field shape, with `id`,
`name`, `value`, `rect`, `page` and `type`. The first field is `character_name` with the value
`"Martin Ghaleb"`.

The names are semantic and cover the whole sheet. Identity and build: `character_name`, `ancestry`,
`heritage`, `background`, `class`, `level`, `size`, `traits`, `languages`. Attributes: `strength`
through `charisma`, plus `*_boost` fields. Defences: `armor_class`, `ac_dex_bonus`, `ac_prof_bonus`,
`ac_item_bonus`, `hp_max`, `current_hp`, `temporary_hp`, `wounded`, `dying_*`, shield fields,
`resistances`, `immunities`. Saves and skills carry a value plus their decomposition, as in
`fort_con_bonus`, `fort_prof_bonus`, `fort_item_bonus`. Proficiency rank arrives as four separate
checkboxes per skill, save, armour category and weapon category, as in `athletics_prof_trained`
= `"Yes"` and `athletics_prof_expert` = `"Off"`. There are 27 `feat_N` slots, 27 `class_feature_N`
slots, 40 `item_N` slots, 3 melee and 2 ranged weapon blocks with damage and trait fields, 52
`spells_N` slots with matching `spells_rank_N`, cantrip, focus, innate and ritual blocks, four
currency fields, and the full biography set from `appearance` to `campaign_notes`. The sample is a
level 1 kineticist, and 240 of its 762 fields carry a value.

**The field names are stable across systems and over time.** The Discord bot
[rpg-sage-creative/rpg-sage](https://github.com/rpg-sage-creative/rpg-sage) contains
`src/gameSystems/p20/sf2e/import/pdf/keyMap/getDemiplanePdfKeyMap.ts`, a 31 KB map from Demiplane
**Starfinder 2e** PDF field names to its own character model. It was written by a different author,
for a different Nexus, and last touched 12 March 2026, ten months after the Pathfinder sample above.
I compared the two. Of the 347 field names RPG Sage maps, **318 are byte-identical to names in the
Pathfinder sample, 91.6%**. The 29 that differ are Starfinder-specific (`computers`, `piloting`,
`credits`, a third ranged weapon slot). Both files carry Demiplane's own typo `surival`. Two
independent authors, two game systems, ten months apart, one hand-built template.

A third project, [RSXII/foundryvtt-demiplane-cprimport](https://github.com/RSXII/foundryvtt-demiplane-cprimport),
parses the Demiplane Cyberpunk RED PDF with pdf-lib, which is another form-field reader. Three
systems, three libraries, same conclusion.

**How the PDF is produced.** The `getCharacterPdf(id, isPregen, templateName)` signature on the
GraphQL endpoint shows the file is rendered server-side on demand from a Demiplane-owned template
chosen by game system. Demiplane's changelog entry dated 20 August 2024 reads, verbatim, "PDF exports
are now editable. No one can stop you now!" That is their marketing word for the same thing the field
dump proves.

**What the PDF does not carry.** Feats, class features and items are **bare names with no level,
no source and no quantity**, so a consumer has to resolve names against a rules database. Slot counts
are fixed at 27 feats, 27 class features, 40 items, 3 melee weapons, so a high-level character can
overflow the sheet, and I have not tested where. Prepared spells are deliberately omitted: staff
explained on 10 August 2024 in
[PDF export feedback](https://forums.demiplane.com/t/pdf-export-feedback/3715) that a prepared caster
gets only cantrips and focus spells because "you'd not be able to erase and change them each day when
playing with your printed sheet". Users in that thread on 5 August 2024 also complained that feat and
spell **descriptions** are absent, so the PDF gives you the build, never the rule text.

There is no XFA layer in evidence anywhere, and nobody mentions one. Note the honest limit here: the
762-field sample is one character, from May 2025, and the RPG Sage map is Starfinder. I have not seen
a freshly exported 2026 Pathfinder PDF. The overlap evidence makes drift unlikely but not impossible.

### 3. Community tools, and how healthy they are

The ecosystem is real, extremely young, and carried by one person.

[scooper4711/demiplane-api](https://github.com/scooper4711/demiplane-api) is a TypeScript client for
the GraphQL endpoint. Repo created 21 August 2026, last push 17 September 2026, which is yesterday.
MIT, 0 stars, 0 forks, 2 open issues. Its README states that reads on public characters need no auth
and that writes need a token you obtain yourself, "for example, by extracting it from an
authenticated browser session on app.demiplane.com". Published to npm as
`@scooper4711/demiplane-api`.

[scooper4711/demiplane-pf2e](https://github.com/scooper4711/demiplane-pf2e) is a Foundry module
built on that client, titled "Demiplane PF2e Sync", doing two-way character sync. Last push
15 September 2026, releases `v1.0.0-rc.3` through `rc.5` on 13 and 14 September 2026, 1 star. It
requires Foundry v14. Its README tells the user to capture a bearer token with one of three generic
header-grabber Chrome extensions or from DevTools, then paste it into module settings, and warns
"Demiplane tokens expire, so the GM must repeat these steps when imports begin reporting
authentication errors" and that the module "can result in data loss for the Foundry Actor, the
Demiplane character, or both".

[Seelenoede/demiplane-pf2e-foundry-importer](https://github.com/Seelenoede/demiplane-pf2e-foundry-importer),
the PDF reader analysed above, is dead as a tool. Six commits across two days in May 2025, 1 star,
and its own README says "This is still in the prototype phase... Do not use as is." Its value to us
is the committed field dump, not the code.

[rpg-sage-creative/rpg-sage](https://github.com/rpg-sage-creative/rpg-sage), 16 stars, repo active
into September 2026, holds the Starfinder PDF key map. Its import path was last touched
12 March 2026, so it is six months stale inside an otherwise live repo. A research subagent reports
that Sage normalises the Demiplane PDF into its `PathbuilderCharacterCore` type, which would make it
a working precedent for the exact conversion this project needs. I verified the key map file and its
`PdfJsonFieldManager` type; I did not verify the normalisation target myself.

Also found, all Daggerheart or Cyberpunk rather than Pathfinder, and all young:
[Athen-Player1/Demiplane_Daggerheart_Importer](https://github.com/Athen-Player1/Demiplane_Daggerheart_Importer)
(reads the payload behind a public share URL with no token, last commit 13 September 2026),
`chris-arsenault/foundry-modules` (two commits on 4 August 2026, abandoned), and the pdf-lib
Cyberpunk importer above (three commits on 6 August 2026).

**A distribution fact that should temper all of it.** Not one Demiplane tool is listed in Foundry's
package registry. `https://foundryvtt.com/packages/demiplane-pf2e` returns HTTP 404 and a registry
search for "demiplane" returns nothing. The Forge bazaar returns `{"success":true,"package":null}`
for `demiplane-pf2e`. Every one is manifest-URL install only, with no install counter to judge it by.
For contrast, Pathmuncher is in the registry and installed on **11.07%** of Forge worlds
(`https://forge-vtt.com/api/bazaar/package/pathmuncher`).

Nothing from Reddit. A research subagent was blocked on every route into reddit.com. Treat that
channel as unsearched, not as empty.

### 4. What the terms actually say

Demiplane has no terms document of its own. `https://demiplane.com/terms-of-service` 301-redirects to
`https://help.roll20.net/hc/en-us/articles/360037770793-Terms-of-Service-and-Privacy-Policy`, which I
confirmed first-hand by following the redirect chain. `https://demiplane.com/terms`, `/legal`, `/tos`
and `https://app.demiplane.com/terms` are all 404. The Roll20 document is stamped 27 February 2026
and is a combined terms and privacy policy. It never uses the words "Demiplane" or "Nexus"; it
reaches Demiplane through its preamble, "When you read Roll20 or 'we' below, it refers to Roll20,
LLC, its affiliates, and agents."

**There is no anti-scraping, anti-bot or anti-automated-access clause.** A subagent's full-document
keyword sweep returned zero hits for robot, spider, crawler, scrape, scraping, data mining,
automated means, automated script, bot, API and aggregation. The nearest clauses that exist are four
bullets under "Play Nice Clauses", each scoped to other people's data or to server load:

> "Collect, harvest, mine or engage in any other activity to obtain e-mail addresses, phone numbers,
> personal information or any other information about others."

> "Access or attempt to access any material that you are not authorized to access."

> "Access or use Roll20 in any manner that could damage, disable, overburden or impair any Roll20
> server or the network(s) connected to any Roll20 server."

> "Prepare, compile, use, download or otherwise copy any Roll20 user directory or other user or usage
> information or any portion thereof..."

The clause that does bite is reverse engineering, under "Intellectual Property Rights":

> "You agree not to copy, republish, frame, download, transmit, modify, adapt, create derivative works
> based on, rent, lease, loan, sell, assign, distribute, display, perform, license, sublicense or
> reverse engineer the Roll20 service or Roll20 Materials or any portions of them."

"Roll20 materials" is defined as content "produced by Roll20", and a separate clause says "Roll20
does not claim intellectual property rights over campaigns or uploaded content on Roll20". The GDPR
section grants "The right to obtain and reuse your personal information for your own purposes."

Honest limit on this section. I verified the redirect chain and both `robots.txt` files first-hand.
The clause quotes come from a single subagent fetch of the Roll20 help centre URL, which returned
HTTP 403 when I tried to re-fetch it to check the wording myself. Treat the quotes as one-source.

`https://www.demiplane.com/robots.txt` is a zero-byte file. `https://app.demiplane.com/robots.txt`,
which I fetched directly, disallows five paths for all agents and nothing else:

	Disallow: /nexus/*/character-beacon
	Disallow: /nexus/*/compendium-link
	Disallow: /nexus/*/character-link
	Disallow: /nexus/character-link
	Disallow: /nexus/character-beacon

Those are precisely the character linking surfaces. A crawler convention is not a contract term, and
the terms never reference the file, but it is the closest thing to a stated wish about automated
access, and it points away from the share-link route.

**The practical read.** A user exporting their own PDF and uploading it to our app touches none of
this. It is a file the user downloaded through a feature Demiplane built for that purpose. Reading
the private GraphQL endpoint with a token lifted from a browser session is different: it is not
forbidden by any clause I found, but it is the route the reverse-engineering clause is closest to,
it sits behind a `Disallow` for the linking surfaces, and the real exposure is the termination clause
that lets Roll20 cut access "at any time in Roll20's sole discretion, without prior notice". I am not
a lawyer and this is not legal advice.

### 5. The other direction does not exist

Demiplane's Pathfinder 2e Nexus has no character import from anything. The help centre articles in
section 1 never use the word "import". The forum request
[Pathbuilder JSON import for 3rd party content](https://forums.demiplane.com/t/pathbuilder-json-import-for-3rd-party-conetent/3293),
running 9 to 12 April 2024, drew no staff reply at all. A GitHub search for repositories matching
"pathbuilder demiplane" returns nothing.

There is no shared interchange format. Pathbuilder's JSON is its own shape, Demiplane's PDF field
names are its own, and Foundry's actor JSON is a third. Paizo publishes no character interchange
format; its own sheet, as established in section 2, has no form fields at all.

The one thing that helps, and it helps a lot, is RPG Sage converting a Demiplane PDF into a
Pathbuilder-shaped object. That is a precedent for treating the Demiplane PDF as a second front end
onto the same internal model, rather than as a second importer.

Writing **into** Demiplane is technically possible through `updateCharacter()` on the GraphQL client,
and the Foundry module's session mode pushes HP, focus points, coins and spell slots back. Nobody has
pointed that at Pathbuilder, and it is not a direction this project needs.

### 6. Pathbuilder's export, for contrast

Pathbuilder 2e's export is **undocumented**. I fetched `https://pathbuilder2e.com/` today. It is a
4 KB launcher page whose only links are `app.html`, a stylesheet, some favicons and the Google Play
listing. There is no help page, no FAQ, no documentation section and no mention of export, JSON or an
API anywhere on it. There is no published schema, no version field and no stability promise from the
author that I could find.

The mechanism is community knowledge. Pathmuncher's README
(`https://raw.githubusercontent.com/MrPrimate/pathmuncher/main/README.md`) gives the flow in two
steps: "Export your character using 'Export JSON' from Pathbuilder via the burger menu" and then
"Enter the 6 digit user ID number from the pathbuilder in the bottom section". So the export is not a
file the user hands over. It is a numeric code that a consumer redeems against Pathbuilder's server
at `https://pathbuilder2e.com/json.php?id=NNNNNN`.

**A practical risk that section matters more than it sounds.** I probed that endpoint today with a
plain HTTP client and got a Cloudflare interstitial, "Just a moment...", not JSON. Pathmuncher works
because it runs in a browser inside Foundry. A server-side fetch from an ASP.NET Core `HttpClient`
may well be challenged. This needs a live test before Phase 4 is designed, and the safe design is to
accept a pasted or uploaded JSON body rather than to fetch by ID from the server.

Whether the format actually drifts, I could not establish. Nobody has published a changelog for it,
and I found no dated report of an import breaking on a format change. The one nearby signal is that
consumers design against drift: `nicholepatrisse/nexus-codex`
[issue 256](https://github.com/nicholepatrisse/nexus-codex/issues/256), opened 4 September 2026,
plans to parse Pathbuilder JSON "at an explicit, versioned boundary" and to keep an "adapter version
and import timestamp for provenance". That is one project's caution, not proof the format moved.

The brief already suspects the format. `todo.md` line 38 records that "Pathbuilder's exported derived
numbers do not all reconcile", with `acTotal` checking out but a sample rapier deriving to 13 against
an exported 11, and hit points exported as 90 against a correct 76. That is an undocumented format
whose derived values are advisory.

What Pathbuilder still has over Demiplane is real. It is the sheet most Pathfinder 2e tables use, the
export is JSON rather than a form layer, and it carries structure the PDF does not, because the PDF
flattens a build into 27 feat name slots.

## Options, ranked

| Rank | Option | What it costs | What you get | Risk |
| --- | --- | --- | --- | --- |
| 1 | Pathbuilder JSON, as planned | Phase 4 as scoped. Plus one live test of whether a server-side fetch clears Cloudflare | The richest structured export, from the tool most tables use | Undocumented format, advisory derived numbers, Cloudflare on the fetch path |
| 2 | Demiplane PDF upload, user-supplied | A PDF form reader and a field-name map. PdfPig gives read-only AcroForm access in C# through `document.TryGetForm(out AcroForm form)` and `form.Fields`, with `AcroTextField` and `AcroCheckboxField` types ([PdfPig wiki](https://github.com/UglyToad/PdfPig/wiki/Forms-(AcroForms))). The name map is a few hundred lines and mostly mechanical | Identity, attributes, AC, HP, speed, every save and skill with its decomposition, proficiency rank as four checkboxes, feats, class features, items, weapons, spells, currency, biography | Names only, so resolution against the rules index. Fixed slot counts. No prepared spells. No rule text. One-sample evidence for the 2026 field set |
| 3 | Do nothing, keep "manual entry" | Zero | Nothing | Leaves a working, permitted route unused |
| 4 | Private GraphQL endpoint with a user-pasted token | A GraphQL client, a token-capture flow the user performs in DevTools or with a third-party extension, and token-expiry handling | Live two-way sync, the full character JSON, everything the PDF drops | Undocumented backend, no stability promise, token handling is a credential we would be asking users to paste, closest route to the reverse-engineering clause, and a termination clause with no notice |
| 5 | Public share link, anonymous read | Unknown, and unverified | Possibly the same JSON with no token | `robots.txt` disallows exactly the character-link surfaces. Sharing may require a login anyway. Not established |

Option 2's cost is genuinely small **for this project specifically**, which is why it ranks where it
does. The brief's importer contract already says feats and spells arrive "by name only" and that "the
app matches names to its own FeatDef and EffectDef records and lists unmatched ones as text so nothing
is lost". `todo.md` records 22,532 seeded rules records from the Archives of Nethys ingest. The
name-resolution machinery the PDF needs is the machinery Phase 4 builds anyway.

## Recommendation

Keep Pathbuilder as the default and build it first, unchanged. Add one line to the Phase 4 test plan:
confirm that whatever fetches `json.php` clears Cloudflare, and prefer an uploaded or pasted JSON body
over a server-side fetch by ID.

Change the Demiplane decision from "manual entry" to "PDF upload, after Phase 4". Design Phase 4's
importer so the parse step and the name-resolution step are separate, with a small intermediate
character record between them. Then the Demiplane path is a second parser onto the same record, not a
second importer. RPG Sage already does exactly this, normalising a Demiplane PDF into a
Pathbuilder-shaped object.

Do not build on the GraphQL endpoint. It works today, one person is actively using it, and it is the
only route to a live sync. It is also undocumented, token-gated by a credential we would ask users to
paste out of DevTools, and the nearest thing in the governing terms to a prohibition. Two of those
three are reasons to wait for a public API rather than to pre-empt one.

Before writing any Demiplane code, spend five minutes on the one experiment this note could not run.
Export one real Pathfinder 2e character from Demiplane today and dump its form fields:

	pdftk character.pdf dump_data_fields

or, with no pdftk, `qpdf --json=latest character.pdf`. If the field names still match the 762 in
`exampleData/pdfData.json`, option 2 is confirmed for 2026 and the estimate holds. If they have
drifted, the whole of section 2 needs revisiting and the recommendation weakens to "Pathbuilder only".

## What could not be established

- Whether a 2026 Pathfinder 2e export still carries the same field names. The sample is from May 2025
  and the cross-system check is Starfinder. Nobody has posted a 2026 Pathfinder dump. The experiment
  above settles it.
- Where the PDF's fixed slot counts break. 27 feats and 40 items will not hold a level 20 character,
  and no source says what happens when they overflow.
- Whether a genuinely shared character UUID returns a populated payload to an anonymous GraphQL
  caller. The third-party README asserts it; no observation confirms it.
- Whether the Roll20 terms quotes in section 4 are complete. One subagent fetch reached the document;
  my own re-fetch got HTTP 403.
- Whether Demiplane ever had its own pre-acquisition terms with scraping language. The Internet
  Archive was offline during this research.
- Anything from Reddit, and anything from the Chrome or Edge extension stores. All were blocked.
  Greasy Fork, OpenUserJS and Firefox Add-ons returned genuine zero results for "demiplane".
- Anything behind the Demiplane login. Nobody authenticated. The export UI, the sharing sidebar and a
  real exported file were never seen first-hand.
- Whether Demiplane has private or partner developer documentation. The absence of public docs is
  established; the absence of docs is not.
