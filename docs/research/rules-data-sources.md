# Structured rules data sources for Pf2eBuilder

Research date: 2026-09-18. Read-only investigation. Every claim below cites a page or file fetched
on that date. Quotations are taken from raw HTML or raw files, not from a summariser.

I am not a lawyer. The licensing section names risks; it is not legal advice.

## The short answer

A machine-readable modifier source exists, it is excellent, and it is one project rather than
several. The Foundry VTT Pathfinder 2e system encodes "Courageous Anthem grants a +1 status bonus to
attack rolls" as this, in a file on disk:

```json
{ "key": "FlatModifier", "selector": ["attack-roll", "damage"], "type": "status", "value": 1 }
```

Nothing else comes close. Wanderer's Guide has a real structured operation system but no licence and
no terms at all. Every other community dataset stores the modifier as an English sentence, which is
what we already get from Archives of Nethys.

The licensing answer is less comfortable than the technical one, and it is not the usual "fan
projects do this, so it is fine". It is set out under [Licensing](#licensing-in-plain-language).

## Comparison

| Source | Format | Modifiers machine-readable | Coverage | Licence on the DATA | Maintained |
| --- | --- | --- | --- | --- | --- |
| [foundryvtt/pf2e](https://github.com/foundryvtt/pf2e) | One JSON file per item, `rules[]` array of typed rule elements | **Yes.** 8,354 `FlatModifier` instances across 6,097 files | 29,679 records. 6,285 feats, 5,870 equipment, 1,995 spells, 875 class features, 545 spell effects, 853 feat effects, 43 conditions | **Unclear.** `LICENSE` is Apache-2.0; the README scopes it to "HTML, CSS and Javascript" and routes the content to a Paizo/Foundry bilateral agreement plus OGL 1.0a | Yes. `pushed_at` 2026-09-16 |
| [Wanderer's Guide](https://docs.wanderersguide.app/api-reference/introduction.md) | Postgres rows with an `operations[]` column; public POST API and a 54 MB SQL dump | **Yes.** `addBonusToValue` carries `value`, `type` (`item`/`status`/`circumstance`) and `variable` | Broad, but many riders are `value: null` with English in `text` | **None found.** Code is GPL-3.0; the API spec claims MIT and links to a 404; no ToS page exists | Yes. `pushed_at` 2026-09-15 |
| [Pf2ools/pf2ools-data](https://github.com/Pf2ools/pf2ools-data) | JSON validated by a Zod and JSON Schema companion repo | No. Prose `entries[]` | 1,047 files. Only `background`, `condition`, `divineIntercession`, `event`, `familiarAbility`, `relicGift`, `skill`. No feats, spells, items or classes | MIT on originals, CUP asserted for Paizo content. Cleanest posture here | Slow. `pushed_at` 2026-02-01 |
| [Pf2eToolsOrg/Pf2eTools](https://github.com/Pf2eToolsOrg/Pf2eTools) | 5etools-style JSON | No. Prose `entries[]` | Broad but legacy-era | MIT asserted over deliberately verbatim rules text. See the risk note below | Yes. `pushed_at` 2026-06-07 |
| [devonjones/PFSRD2-Data](https://github.com/devonjones/PFSRD2-Data) | JSON, 35,890 files, strong Remaster coverage | No. Prose `feat.text` | Player Core, Player Core 2, Treasure Vault, with legacy cross-links | **No LICENSE file** (`license: null`). Per-record ORC notices only | Yes. `pushed_at` 2026-08-19 |
| Archives of Nethys Elasticsearch (what we use now) | Elasticsearch documents | No | 22,596 records in our snapshot, 182 distinct fields observed | Site runs under CUP and OGL. No reuse grant served with the endpoint | Yes. Index `aon-20260902-190924` |

## What each source gives us that AoN does not

**Foundry** gives us the thing AoN structurally cannot. AoN tells us a feat exists, what book it is
in, its level and its traits. Foundry tells us what the feat does to a character sheet, as a value,
a bonus type and a target selector, in a schema that is formally defined in TypeScript and validated
on load.

**Wanderer's Guide** gives us the same class of information through a different model, plus
character-build operations that Foundry expresses differently, such as `giveAbilityBlock`,
`giveSpellSlot` and proficiency ranks as `U/T/E/M/L`. Its rows also embed Foundry rule elements under
`meta_data.foundry`, which says something about where the ecosystem's centre of gravity sits.

**pf2ools** gives us one genuinely useful thing and no more. Its background records carry
`abilityBoosts` as structured counts and named attributes, which is character-build data rather than
combat modifiers.

**PFSRD2-Data** gives us per-record ORC attribution notices, which are a useful model for our own
compliance even though its mechanics are prose.

**Nothing new from AoN.** I checked whether we have overlooked a field. We have not. Our own field
census in `C:\Users\lolro\Desktop\Pf2eBuilder\tools\rules-import\EXCLUDED-FIELDS.md` covers 182
distinct fields over 1,928 sample documents spanning all eighteen categories, and the complete
inventory is the 47 excluded, the 28 stored but unseeded, the 103 on the allow-list, plus `id`,
`name`, `category` and `url`. Not one of those 182 encodes a modifier. The closest candidates are
`skill_mod`, `resistance` and `weakness`, and those are creature stat-block fields, not effects that
a feat or item confers. `https://elasticsearch.aonprd.com/aon/_mapping` returns HTTP 403, so the
mapping cannot be read directly, but a 182-field census over every category is strong evidence. The
`summary` field renders "+2 to initiative rolls." as English, which is the same problem in shorter
form.

## The Foundry rule element schema

**Where the data lives.** Branch `v14-dev`, the repository default, holds `packs/pf2e/` and
`packs/sf2e/`, one directory per compendium pack, one pretty-printed JSON file per record. The older
`master` branch holds the same packs flat under `packs/`. The schema lives in
`src/module/rules/rule-element/`, about 40 rule element keys, with `flat-modifier.ts` defining the
one we care about.

**The schema is a real declared schema**, not a convention. From
`src/module/rules/rule-element/flat-modifier.ts` on `v14-dev`:

```typescript
selector: new fields.ArrayField(new fields.StringField({ required: true, blank: false, ... })),
type: new fields.StringField({ required: true, choices: Array.from(MODIFIER_TYPES), initial: "untyped" }),
ability, min, max, force, hideIfDisabled, fromEquipment, damageType, damageCategory,
critical, value: new ResolvableValueField(...), tags, removeAfterRoll, battleForm
```

`MODIFIER_TYPES` in `src/module/actor/modifiers.ts` is exactly the PF2e bonus type set:

```typescript
const MODIFIER_TYPES = new Set(["ability", "circumstance", "item", "potency", "proficiency", "status", "untyped"] as const);
```

**The rule element vocabulary, counted across all packs.** `FlatModifier` 8,354, `RollOption` 5,363,
`ActiveEffectLike` 3,645, `ItemAlteration` 2,914, `GrantItem` 2,501, `DamageDice` 2,382, `Note`
2,000, `ChoiceSet` 1,948, `Aura` 1,539, `Resistance` 1,147, `Strike` 837, `AdjustDegreeOfSuccess`
718, `AdjustModifier` 703, and a long tail. 13,710 of the 29,679 records carry at least one rule
element.

**Remaster coverage is tagged per record**, which is the single most useful property of this dataset
for us. Every record carries a `publication` block:

```json
"publication": { "license": "ORC", "remaster": true, "title": "Pathfinder Player Core" }
```

Counted across all packs, `"license": "OGL"` appears 73,166 times and `"license": "ORC"` 54,363
times; `"remaster": false` 72,202 and `"remaster": true` 55,327. These counts include nested records
inside bestiary actors, so they exceed the 29,679 file count. A Remaster-only builder can filter on
this field rather than guessing from the book name.

**A gotcha worth knowing before anyone plans an import.** Spells carry no `FlatModifier` at all. All
1,995 files in `packs/pf2e/spells/` contain zero. The modifier lives on a separate effect item that
the spell links to. Courageous Anthem the spell has no rules. "Spell Effect: Courageous Anthem" at
`packs/pf2e/spell-effects/spell-effect-courageous-anthem.json` carries them:

```json
"rules": [
  { "key": "FlatModifier", "selector": ["attack-roll", "damage"], "type": "status", "value": 1 },
  { "key": "FlatModifier", "predicate": ["fear"], "selector": "saving-throw", "type": "status", "value": 1 },
  { "key": "DamageDice", "damageType": "sonic", "diceNumber": 1, "dieSize": "d6", "selector": "strike-damage" },
  { "key": "RollOption", "option": "courageous-anthem:origin:signature:{item|origin.signature}" }
]
```

The same split applies to feats, where `feat-effects` holds 853 files with 452 carrying a
`FlatModifier`, and to equipment, where `equipment-effects` holds 721 files with 401 carrying one.
Any importer must follow the link, not just read the primary record.

## What this data says about our own condition registry

This is the part that should change someone's mind.

`src/Pf2e.Domain/Conditions.cs` hand-codes fourteen conditions, and its own doc comment says
`Verified` defaults to false "because the values were transcribed from a brief its own author called
a draft written from memory". Foundry's `packs/pf2e/conditions/` holds 43 condition files, and
exactly fourteen of them carry a `FlatModifier`. The two sets of fourteen are identical. Blinded,
clumsy, deafened, drained, encumbered, enfeebled, fascinated, fatigued, frightened, off-guard, prone,
sickened, stupefied, unconscious.

That makes Foundry a drop-in validation corpus for work we have already done. Diffing the two
surfaces six discrepancies, and in each one Foundry's encoding is the more specific.

| Condition | Our registry | Foundry | Reading |
| --- | --- | --- | --- |
| Frightened | status, all checks | `selector: "all"`, `value: "-@item.badge.value"` | Agrees |
| Sickened | status, all checks | `selector: "all"` | Agrees |
| Off-guard | circumstance -2, AC | `selector: "ac"`, circumstance, -2 | Agrees |
| Prone | circumstance -2, attack | `selector: "attack-roll"`, circumstance, -2, plus grants Off-Guard | Agrees on the modifier; we model no cascade |
| Fascinated | status -2, Perception and skills | `selector: ["perception", "skill-check"]`, -2 | Agrees |
| Fatigued | status -1, AC and three saves | `selector: ["ac", "saving-throw"]`, -1 | Agrees |
| Blinded | status -4, Perception | `selector: "perception"`, -4, plus `Immunity: visual` | Agrees on the modifier |
| Unconscious | status -4, AC, Perception, Reflex | same three selectors, -4, plus grants Blinded, Off-Guard, Prone | Agrees on the modifier; we model no cascade |
| **Clumsy** | AC, Reflex, Acrobatics, Stealth, Thievery | `selector: "dex-based"` | **Differs.** A derived Dex category also covers Dex-based attack rolls, which our enumeration omits |
| **Drained** | Fortitude only | `selector: "con-based"`, plus a second `FlatModifier` on `hp` and a `LoseHitPoints` rule | **Differs.** We model no maximum-HP reduction at all |
| **Enfeebled** | Attack, Damage, Athletics | `selector: ["str-based", "str-damage"]` | **Differs.** We apply to all attack and all damage; Foundry restricts to Strength-based |
| **Stupefied** | Will, Perception, spell attack, spell DC | `selector: ["cha-based", "int-based", "wis-based"]` | **Differs.** We omit Int-, Wis- and Cha-based skill checks |
| **Deafened** | -2 to all Perception | -2 predicated on `item:trait:auditory`, plus `AdjustDegreeOfSuccess` to critical failure | **Differs.** We apply to sight-based Perception too, and model no automatic critical failure |
| **Encumbered** | status -1 to the clumsy targets | untyped -10 on `all-speeds`, plus `GrantItem` Clumsy | **Differs.** We model no speed penalty, and we inline clumsy rather than composing it |

Six of fourteen differ, and each difference has the same root cause. We enumerate affected
statistics. Foundry names a derived category, such as `dex-based`, `str-based`, `con-based`,
`saving-throw`, `all-speeds` or `all`. An enumeration goes stale the moment a new Dex-based skill or
a Dex-based attack enters scope. A derived selector does not. That is a domain-modelling
observation, and it is free to act on regardless of how the licensing question lands, because the
idea of categorising selectors by key attribute is not anyone's property.

## Licensing in plain language

### Foundry's data is the prize and its licence is the problem

The repository's `LICENSE` file is an unmodified Apache License 2.0 and GitHub reports
`spdx_id: Apache-2.0`. But the README narrows it. Quoted verbatim from `git show v14-dev:README.md`:

> **Project Licensing:**
>
> - All HTML, CSS and Javascript in this project is licensed under the Apache License v2.
>
> **Content Usage and Licensing:**
>
> - Any Pathfinder Second Edition information used with permission granted by the license agreement between Paizo. Inc and Foundry Gaming LLC
> - Game system information and mechanics are licensed under the Open Game License (OPEN GAME LICENSE Version 1.0a).

and from the top of the same file:

> This system uses trademarks and/or copyrights owned by Paizo Inc., which are used with permission
> granted as part of the partnership agreement between Foundry Gaming LLC and Paizo Inc.

> If you would like to undertake a similar project, much of what this system includes is covered
> under Paizo's Community Use Policy.

**Read that carefully.** JSON is not HTML, CSS or Javascript. The Apache grant as the README
describes it does not name the pack data. The content is instead attributed to a bilateral agreement
between two companies, and we are party to neither. The `rules` arrays are the original work of the
volunteer development team rather than of Paizo, which is an argument that they fall under the
project's own licence, but the README's own carve-out is the strongest evidence against reading it
that way.

**This is genuinely ambiguous and I will not pretend otherwise.** The unqualified `LICENSE` file
says one thing and the README says a narrower thing. Neither document resolves which governs the
JSON. The concrete risk is that we build a seeding pipeline on 29,679 files whose redistribution
right rests on a reading the authors did not write down.

**The cheapest available action is to ask.** One question to the pf2e maintainers on the Foundry
Discord `#pf2e` channel, which the README itself names as the contact route, would settle whether
the pack `rules` arrays are Apache-2.0. That has a far better return than any amount of further
reading.

### ORC covers the mechanics, and it covers them generously

Quoted from `https://paizo.com/orclicense`, fetched and de-tagged directly. Licensed Material,
Section I.e(ii), is:

> those expressions reasonably necessary to convey functional ideas and methods of operation of a
> game that are contained in a Work that are comprised of systems, procedures, processes, rules,
> laws, instructions, heuristics, routines, functional elements, commands, structures, principles,
> methodologies, operations, devices, and concepts of play, and the limitations, restraints,
> constraints, allowances, and affordances inherent in gameplay

and its enumeration expressly includes:

> conditions, buffs and debuffs, powers, terrain types, challenge ratings, moves, difficulty
> classes, skill checks, saving throws, resting and resource management, classification of magic
> systems, spell and ability effects, looting, items and equipment, diplomacy systems, dialogue
> options, and outcome determination);

The grant, Section II.a:

> Licensor hereby grants You a worldwide, royalty-free, non-sublicensable, non-exclusive,
> irrevocable license to exercise the Licensed Rights in the Licensed Material to Use the Licensed
> Material, in whole or in part that may be terminated only as set forth in Section V.a. for Your
> breach.

And, directly on point for a database project, Section II.c:

> Where the Licensed Rights include Sui Generis Database Rights that apply to Your Use of the
> Licensed Material, this ORC License grants You the right to extract, reuse, reproduce, and Use all
> or a substantial portion of the contents of the database

So conditions, buffs and debuffs, spell and ability effects, skill checks and saving throws are named
Licensed Material, the grant is irrevocable, and database extraction is expressly contemplated.
Everything in our modifier registry sits inside that.

### What ORC reserves is names, and that is where our real exposure is

Section I.h:

> Reserved Material means trademarks, trade dress, and creative expressions that are not essential
> to, or can be varied without altering, the ideas or methods of operation of a game system,
> including works of visual art, music and sound design, and clearly expressed and sufficiently
> delineated characters, character organizations, dialogue, settings, locations, worlds, plots, or
> storylines, including proper nouns and the adjectives, names, and titles derived from proper
> nouns.

"Courageous Anthem" and "Frightened" are not proper nouns and are outside that reservation.
"Desna's Shooting Star", deity names, Lost Omens organisations and anything named after a Golarion
person are inside it. We store `name` on every one of 22,596 records, and our allow-list includes
`deity` and `domain`. Those name fields are not licensed to us by ORC.

Section III conditions the grant on notice. The licence states that the grant

> is expressly conditioned on You including the notice statements described in subsections (a)-(d)
> below

which are the ORC Notice, an Attribution Notice naming each Licensor, a Reserved Material Notice,
and an Expressly Designated Licensed Material statement. We ship none of these today.

### The Community Use Policy is the only cover for names, and it is revocable at will

Quoted from `https://paizo.com/community/communityuse`, "Last Updated Thursday, August 22, 2024":

> You may descriptively reference trademarks, proper names (characters, deities, artifacts, places,
> etc.), locations, dialogs, plots, storylines, language, and incidents based on Paizo Material.

> Your project must be free.

> We reserve the right to deny the use of our IP at any time for any reason or for no reason.

> We reserve the right to amend, modify or terminate this Policy at any time.

The CUP's permission list conspicuously does not mention reproducing rules text. Its FAQ at
`https://paizo.com/community/communityuse/faq` says "You may not, however, simply republish
significant sections of descriptive text from our products--you need to craft new text", but that
FAQ is dated September 2019 and still refers to the Community Use Approved Product List, which Paizo
removed in August 2024. From
`https://paizo.com/blog/updates-on-the-community-use-policy-and-fan-content-policy`, "We have
removed both the Approved Products List and Community Use Registry". The FAQ and the live policy are
out of step, which is a real ambiguity rather than a drafting quibble.

### Mechanics are probably not copyrightable at all

17 U.S.C. 102(b), via `https://www.law.cornell.edu/uscode/text/17/102`:

> In no case does copyright protection for an original work of authorship extend to any idea,
> procedure, process, system, method of operation, concept, principle, or discovery, regardless of
> the form in which it is described, explained, illustrated, or embodied in such work.

Paizo's own AxE explainer, on the ORC licence page, concedes the point. "While there are strong
arguments regarding the degree to which pure game mechanics are subject to copyright, placing this
notice in your work removes any doubt".

"Frightened is a status penalty equal to its value applying to all checks and DCs" is a fact about a
system. Paizo's sentence expressing it is expression. Storing the number, the bonus type and the
target selector as structured fields is on the safe side of that line, and it is the side we are
already on.

### The other three sources, briefly

**Wanderer's Guide.** GPL-3.0 on the code. The OpenAPI spec published at
`https://docs.wanderersguide.app/api-reference/content/find-spell.md` declares `license: name: MIT`
and links to a URL that returns 404. No terms of service page exists. `/terms`, `/privacy`, `/legal`
and `/tos` all return the single-page-app catch-all, byte-identical in length to a nonsense path.
There is no Paizo notice anywhere on the site or in the repository. So there is no stated permission
to use the data, and copying `operations` structures out of a GPL-3.0 repository into a non-GPL
builder is a second, separate copyleft exposure. The data is good. The legal position is worse than
ambiguous, because there is nothing there to read.

**Pf2eTools.** `LICENSE.md` is MIT, "Copyright (c) 2017 TheGiddyLimit and contributors", while the
README instructs contributors to "Prioritise RAW above all else. Aim to provide a 1:1 copy of the
original data". An MIT grant permits sublicensing and commercial sale. Nobody but Paizo can grant
that over a deliberate verbatim copy of Paizo's prose. Do not rely on that grant.

**pf2ools.** The most honest posture of any project reviewed. Its README states "Content maintained
by Pf2ools is reproduced without claim of ownership and under its respective license", "Content
published by Paizo Inc. is reproduced in accordance with the Community Use Policy", and "All
original content (e.g. scripts) is licensed under the MIT license", and it ships the full CUP and OGL
text. Its coverage is too thin to matter to us.

## Where our current line sits

We store mechanics and names and deliberately exclude rule prose. That line is close to right, but it
is miscalibrated in both directions at once.

**We are more cautious than we need to be about mechanics.** ORC names conditions, buffs and debuffs
and spell and ability effects as Licensed Material under an irrevocable grant, and 102(b) says the
system is unprotectable regardless of the licence. Structured modifier data is the safest thing in
the whole project. Withholding `stage` on the grounds that it is affliction rule text remains
correct, because that is expression. But there is no reason to hesitate over storing richer
mechanics.

**We are less cautious than we need to be about names and notices.** Two specific gaps.

1. Our seed carries `name` on every record and includes `deity` and `domain`, and ORC expressly
   reserves "proper nouns and the adjectives, names, and titles derived from proper nouns". Those
   fields rest entirely on the CUP, which Paizo may withdraw "at any time for any reason or for no
   reason". We should know that and be able to strip them on request.
2. The ORC grant is "expressly conditioned on" a four-part notice we do not ship. An application
   relying on ORC without that notice is arguably outside the licence.

**And we record no provenance.** The seed allow-list carries `primary_source`, `source`,
`release_date`, `remaster_id` and `legacy_id`, but no licence field. Paizo is explicit at
`https://paizo.com/licenses` that "The ORC also doesn't allow you to convert content previously
released as Open Game Content under the OGL into what the ORC classifies as Licensed Material", so
OGL-era and ORC-era records must be distinguishable per record. Foundry already does this with
`publication.license`. We should too.

## Recommendation

**1. Use Foundry's condition data now, as a test fixture, not as a shipped seed.** Fourteen
conditions, matching ours exactly, whose values we can diff against a registry whose own author
marked it unverified. Six defects are already visible from the table above. This is a handful of
numbers, it is the least copyrightable material in existence, and it never ships. Do this first, and
set `Verified = true` only on what the diff confirms.

**2. Adopt the derived selector vocabulary into our domain model.** Replace enumerated `StatTarget`
lists with derived categories such as `dex-based`, `str-based`, `con-based`, `saving-throw`,
`all-speeds` and `all`. This is the root cause of four of the six defects, it is an idea rather than
an expression, and it makes any later import a near-mechanical mapping instead of a translation.

**3. Ask the Foundry pf2e maintainers one question before building an import pipeline.** Whether the
`packs/` JSON, and specifically the `rules` arrays authored by the volunteer team, falls under the
repository's Apache-2.0 licence. The README's `#pf2e` Discord channel is the stated contact route. A
written answer converts the single largest unknown in this document into a fact, and it costs one
message. Do not build a seeding pipeline on the pack data before that answer arrives.

**4. Ship ORC compliance regardless of which way point 3 goes.** Add the four-part ORC notice, add a
per-record `license` field derived from the source book, and make the name fields strippable. This is
required to rely on ORC at all, and it is work we owe whether or not we ever touch Foundry data.

**5. Do not use Wanderer's Guide or Pf2eTools as a data source.** Wanderer's Guide has excellent
structured operations and no licence to give us. Pf2eTools asserts a licence it cannot grant. Both
are fine to read for reference and wrong to ingest.

**6. If point 3 comes back negative, author our own rule arrays from ORC-licensed Player Core text.**
That is slower, but it is the only path where we own the result outright, and ORC's irrevocable grant
over conditions, buffs and debuffs and spell and ability effects makes it unambiguously permitted.
Foundry's data remains usable as a correctness oracle in that world, since comparing our numbers
against theirs is neither copying nor redistribution.

## Sources fetched

- `https://github.com/foundryvtt/pf2e` and `https://api.github.com/repos/foundryvtt/pf2e`
- `https://github.com/foundryvtt/pf2e/wiki/Quickstart-guide-for-rule-elements`
- A shallow sparse clone of `foundryvtt/pf2e` at branch `v14-dev`, from which all counts, the README
  quotations and every condition rule array in this document were read directly
- `https://paizo.com/orclicense`
- `https://paizo.com/community/communityuse` and its FAQ
- `https://paizo.com/licenses` and
  `https://paizo.com/blog/updates-on-the-community-use-policy-and-fan-content-policy`
- `https://www.law.cornell.edu/uscode/text/17/102`
- `https://docs.wanderersguide.app/api-reference/introduction.md`, `/authentication.md`,
  `/guides/operations.md`, and the Wanderer's Guide repository `LICENSE.txt`
- `https://github.com/Pf2ools/pf2ools-data`, `https://github.com/Pf2eToolsOrg/Pf2eTools`,
  `https://github.com/devonjones/PFSRD2-Data`
- `https://2e.aonprd.com/Licenses.aspx` and `https://elasticsearch.aonprd.com/aon/_mapping`, which
  returns HTTP 403
- `C:\Users\lolro\Desktop\Pf2eBuilder\tools\rules-import\EXCLUDED-FIELDS.md` and
  `C:\Users\lolro\Desktop\Pf2eBuilder\src\Pf2e.Domain\Conditions.cs`
