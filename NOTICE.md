# Notices

**Draft. Needs your review before the project is public, and one decision only you can make.**

## Why this file exists

The ORC License grant is, in its own words, "expressly conditioned on You including the
notice statements described in subsections (a)-(d)". We ship rules mechanics extracted from
Archives of Nethys and we currently ship none of those four notices, which arguably puts us
outside the licence we are relying on. This file is the fix, drafted to the right shape.

Sections marked NEEDS SOURCING are places where I will not invent text. ORC attribution
requires naming each Licensor and each work, including author lists that must be copied from
the actual books rather than reconstructed.

## The decision, and it has been made

**Free and non-commercial.** The owner chose this on 19 September 2026, which is what the rest
of this file assumes. The reasoning is below because the choice is reversible and whoever
reverses it needs to know what it costs.

The ORC License covers the **mechanics** we store, and covers them well. Section II.c grants
the right "to extract, reuse, reproduce, and Use all or a substantial portion of the contents
of the database", and conditions, buffs, debuffs and ability effects are named Licensed
Material. Our modifier registry sits comfortably inside that.

It does **not** cover names. ORC Section I.h reserves "proper nouns and the adjectives, names,
and titles derived from proper nouns". We store a `name` on all 25,595 records, plus `deity`
and `domain` fields. "Frightened" and "Courageous Anthem" are fine. Deity names, Lost Omens
organisations and anything named after a Golarion person are not.

The only cover for those is Paizo's Community Use Policy, and it carries a condition:

> Your project must be free.

So the choice is yours and it is a business one, not a technical one. Keep the project free and
the names are covered by a policy Paizo may revoke at any time. Charge for it and the names have
to go, which means a character sheet that shows mechanics without the deity or the feat name.
Everything we have built so far assumes free and non-commercial.

## ORC Notice

NEEDS SOURCING. Copy the current canonical text from paizo.com/orclicense verbatim rather than
from memory; it names a Library of Congress registration number that must be exact.

## Attribution Notice

PARTLY SOURCED. ORC attribution names each Licensor and each work.

The works are done. [NOTICE-works.md](NOTICE-works.md) lists all 252 of them with how many of
the 25,595 seeded records came from each, derived from the `primary_source` field rather than
typed, and regenerated with:

    node tools/rules-import/works.mjs

The Licensor for every one of them is Paizo Inc.

NEEDS SOURCING: the author list for each work, which is the third column of that table and is
blank. It has to be copied from each book's own legal page. Do not reconstruct one from memory;
a wrong author list is a worse failure of attribution than a missing one, and it is the half of
this that no script can do.

## Reserved Material Notice

Draft: no Reserved Material is reproduced in this project. Rule prose, artwork, setting text
and storyline are deliberately not stored; the ingest excludes 47 prose fields at the wire and
`tools/rules-import/verify.sh` proves the exclusion held. Proper-noun names that appear are used
descriptively under the Community Use Policy, not under ORC.

## Expressly Designated Licensed Material

Draft: this project designates no material of its own as Licensed Material.

## Community Use

Draft: this project uses trademarks and proper names of Paizo Inc. under Paizo's Community Use
Policy. It is not published, endorsed, or specifically approved by Paizo. For more about
Paizo's Community Use Policy, see paizo.com/communityuse. For more about Paizo Inc. and Paizo
products, see paizo.com.

## A gap to close in the data

We record no provenance per record. Paizo state that ORC "doesn't allow you to convert content
previously released as Open Game Content under the OGL into what the ORC classifies as Licensed
Material", so OGL-era and ORC-era records have to be distinguishable row by row. Our seed
carries `primary_source`, `source`, `release_date`, `remaster_id` and `legacy_id`, but no
licence field. Add one at ingest.

Full analysis, with quoted licence text and citations, is in `docs/research/rules-data-sources.md`.
