# Excluded fields

Archives of Nethys publishes Pathfinder 2e rules data that mixes two kinds of value. What this
project withholds is Paizo's expression, meaning flavour text and rule descriptions. The rule text,
a summary sentence and every rendered markdown block are expression, and this project is not
licensed to redistribute them. A short mechanical label stays, even when it is phrased as a sentence
fragment, because a stat-block header such as a trigger or a prerequisite is something a character
builder cannot work without. Facts such as a level, a price or a trait list stay for the same
reason.

The import applies that split twice.

1. **At the wire.** Every `_search` request sends `_source.excludes`, so the withheld prose never
   leaves the AoN server. The snapshot under `Sources/aon-snapshot/` cannot contain what was never
   downloaded.
2. **At the seed.** `Transform.Project` copies only fields named in `FieldPolicy.SeedAllowList`.
   This is deny-by-default. A field that AoN adds tomorrow is withheld from
   `tools/rules-import/out/seed/` until someone reviews it and adds it to that list.

The second rule is the load-bearing one. The first is an optimisation of it that also keeps prose
off this machine. `Transform` asserts that no stored record carries a prose field and fails the run
if one does, which catches a snapshot pulled without the excludes.

An earlier pass classified thirteen fields as prose, and the project owner has re-included them as
stat-block labels. They are `area_raw`, `cost`, `duration_raw`, `frequency`, `pfs`, `prerequisite`,
`primary_check`, `requirement`, `secondary_check`, `stage`, `target`, `trigger` and `usage`. Twelve
of them were dropped from the wire-exclude list and added to the seed allow-list. `pfs` was never
withheld at the wire, so it only needed adding to the allow-list.

The field inventory below comes from 1,928 sample documents spanning all eighteen categories and
182 distinct fields. Those 182 are the 47 excluded here, the 28 stored but unseeded, the 103 on the
allow-list, and `id`, `name`, `category` and `url`, which every record carries and the transform
always emits. Field names, observed lengths and reasons are recorded here. Sample values are not,
because reproducing the excluded prose in the document that explains the exclusion would defeat it.

## Wire-exclude patterns

These exact strings are sent as `_source.excludes` on every request. AoN honours `*` wildcards
there, so the two patterns cover every rendered-markdown field including ones added later.

- `markdown`
- `*_markdown`
- `access`
- `anathema`
- `area_of_concern`
- `area_of_concern_raw`
- `edict`
- `religious_symbol`
- `sanctification_raw`
- `secondary_casters_raw`
- `summary`
- `text`

The nine rendered siblings of the re-included labels stay excluded under `*_markdown`. They are
`prerequisite_markdown`, `cost_markdown`, `trigger_markdown`, `requirement_markdown`,
`target_markdown`, `usage_markdown`, `primary_check_markdown`, `secondary_check_markdown` and
`stage_markdown`. That is intended. The label is a mechanical value. Its AoN rendered presentation
is Paizo's layout and link markup, which is expression.

## Concrete excluded fields (47)

The patterns above match these 47 field names in the sampled data. `manifest.json` records both
lists, the patterns under `excludedFieldPatterns` and these names under `excludedFields`, so a
reviewer can see what the wildcard covered without re-running the sample.

"Categories" is how many of the eighteen categories carried the field. "Max chars" is the longest
value seen in the sample, which is the size of the omission rather than a limit.

| Field | Categories | Max chars | Reason |
| --- | ---: | ---: | --- |
| `access` | 4 | 119 | A sentence stating who may take the option. |
| `ammunition_markdown` | 2 | 57 | AoN rendered presentation of ammunition, carrying site link markup and phrasing. |
| `anathema` | 1 | 245 | Deity anathema written as prose clauses. |
| `area_markdown` | 2 | 79 | AoN rendered presentation of area, carrying site link markup and phrasing. |
| `area_of_concern` | 1 | 136 | Deity flavour prose. |
| `area_of_concern_raw` | 1 | 136 | Deity flavour prose. |
| `armor_group_markdown` | 1 | 35 | AoN rendered presentation of armor_group, carrying site link markup and phrasing. |
| `base_item_markdown` | 1 | 35 | AoN rendered presentation of base_item, carrying site link markup and phrasing. |
| `bloodline_markdown` | 1 | 96 | AoN rendered presentation of bloodline, carrying site link markup and phrasing. |
| `cost_markdown` | 1 | 221 | AoN rendered presentation of cost, carrying site link markup and phrasing. |
| `deity_category_markdown` | 1 | 60 | AoN rendered presentation of deity_category, carrying site link markup and phrasing. |
| `deity_markdown` | 3 | 1118 | AoN rendered presentation of deity, carrying site link markup and phrasing. |
| `divine_font_markdown` | 1 | 58 | AoN rendered presentation of divine_font, carrying site link markup and phrasing. |
| `domain_alternate_markdown` | 1 | 119 | AoN rendered presentation of domain_alternate, carrying site link markup and phrasing. |
| `domain_markdown` | 2 | 246 | AoN rendered presentation of domain, carrying site link markup and phrasing. |
| `domain_primary_markdown` | 1 | 133 | AoN rendered presentation of domain_primary, carrying site link markup and phrasing. |
| `edict` | 1 | 373 | Deity edicts written as prose clauses. |
| `favored_weapon_markdown` | 1 | 65 | AoN rendered presentation of favored_weapon, carrying site link markup and phrasing. |
| `feat_markdown` | 2 | 46 | AoN rendered presentation of feat, carrying site link markup and phrasing. |
| `language_markdown` | 1 | 105 | AoN rendered presentation of language, carrying site link markup and phrasing. |
| `markdown` | 18 | 31540 | The complete rule text in AoN display markup. |
| `pantheon_markdown` | 1 | 216 | AoN rendered presentation of pantheon, carrying site link markup and phrasing. |
| `pantheon_member_markdown` | 1 | 315 | AoN rendered presentation of pantheon_member, carrying site link markup and phrasing. |
| `patron_theme_markdown` | 1 | 38 | AoN rendered presentation of patron_theme, carrying site link markup and phrasing. |
| `prerequisite_markdown` | 3 | 288 | AoN rendered presentation of prerequisite, carrying site link markup and phrasing. |
| `primary_check_markdown` | 1 | 220 | AoN rendered presentation of primary_check, carrying site link markup and phrasing. |
| `religious_symbol` | 1 | 33 | A descriptive phrase with no mechanical use. |
| `requirement_markdown` | 6 | 234 | AoN rendered presentation of requirement, carrying site link markup and phrasing. |
| `sanctification_raw` | 1 | 26 | A sentence clause; sanctification carries the values. |
| `saving_throw_markdown` | 2 | 38 | AoN rendered presentation of saving_throw, carrying site link markup and phrasing. |
| `search_markdown` | 18 | 1572 | AoN rendered presentation of search, carrying site link markup and phrasing. |
| `secondary_casters_raw` | 1 | 39 | A phrase qualifying the count; secondary_casters carries the number. |
| `secondary_check_markdown` | 1 | 327 | AoN rendered presentation of secondary_check, carrying site link markup and phrasing. |
| `skill_markdown` | 4 | 534 | AoN rendered presentation of skill, carrying site link markup and phrasing. |
| `source_markdown` | 18 | 131 | AoN rendered presentation of source, carrying site link markup and phrasing. |
| `speed_markdown` | 1 | 7 | AoN rendered presentation of speed, carrying site link markup and phrasing. |
| `spell_markdown` | 2 | 351 | AoN rendered presentation of spell, carrying site link markup and phrasing. |
| `stage_markdown` | 1 | 201 | AoN rendered presentation of stage, carrying site link markup and phrasing. |
| `summary` | 17 | 290 | A prose sentence describing the record. |
| `summary_markdown` | 17 | 290 | AoN rendered presentation of summary, carrying site link markup and phrasing. |
| `target_markdown` | 2 | 153 | AoN rendered presentation of target, carrying site link markup and phrasing. |
| `text` | 18 | 25143 | The entire rule verbatim. |
| `tradition_markdown` | 2 | 201 | AoN rendered presentation of tradition, carrying site link markup and phrasing. |
| `trait_markdown` | 14 | 248 | AoN rendered presentation of trait, carrying site link markup and phrasing. |
| `trigger_markdown` | 4 | 140 | AoN rendered presentation of trigger, carrying site link markup and phrasing. |
| `usage_markdown` | 1 | 89 | AoN rendered presentation of usage, carrying site link markup and phrasing. |
| `weapon_group_markdown` | 1 | 36 | AoN rendered presentation of weapon_group, carrying site link markup and phrasing. |

## Kept in the snapshot, omitted from the seed (28)

These 28 fields are not prose, so they are downloaded and stored in the snapshot. They are absent
from the allow-list because the builder has no use for them, so the seed stays small and reviewable.
They fall into four groups.

- AoN site plumbing that means nothing outside the website: `exclude_from_search`, `navigation`,
  `image`, `icon_image`, `spoilers`.
- Display-only duplicates of a field the seed already carries in structured form: `rarity_id`,
  `size_id`, `source_raw`, `source_group`, `primary_source_group`, `trait_raw`, `trait_group`,
  `heighten_group`, `item_category`, `item_subcategory`, `archetype_category`,
  `deity_category_order`.
- Pre-Remaster naming and alignment that a Remaster-only builder must not act on: `alignment`,
  `follower_alignment`, `legacy_name`, `remaster_name`.
- Flavour and classification detail the builder does not consume: `epithet`, `pantheon`,
  `pantheon_member`, `sacred_animal`, `sacred_color`, `region`, `is_standard_ancestry_feat`.

Adding any of them later is a one-line change to `FieldPolicy.SeedAllowList`, so the omission costs
nothing to reverse.

## Seed allow-list (103)

`Transform.Project` emits `id`, `name`, `category` and `sourceUrl` on every record, then copies
these fields when the record carries a non-empty value for them. Nothing else reaches the seed.
`sourceUrl` is `https://2e.aonprd.com` joined to the record's own `url` field, never a path built
from the id.

| Field | Categories | Type |
| --- | ---: | --- |
| `ac` | 1 | number |
| `actions` | 5 | string |
| `actions_number` | 5 | number |
| `ammunition` | 2 | string |
| `archetype` | 2 | array |
| `area` | 2 | array |
| `area_raw` | 2 | string |
| `area_type` | 2 | array |
| `armor_category` | 1 | string |
| `armor_group` | 1 | string |
| `attack_proficiency` | 1 | array |
| `attribute` | 5 | array |
| `attribute_flaw` | 1 | array |
| `base_item` | 1 | array |
| `bloodline` | 1 | array |
| `bulk` | 3 | number |
| `bulk_raw` | 3 | string |
| `check_penalty` | 1 | number |
| `class` | 1 | string |
| `cleric_spell` | 1 | array |
| `component` | 1 | array |
| `cost` | 1 | string |
| `damage` | 1 | string |
| `damage_die` | 1 | number |
| `damage_type` | 1 | array |
| `defense_proficiency` | 1 | array |
| `deity` | 3 | array |
| `deity_category` | 1 | string |
| `dex_cap` | 1 | number |
| `divine_font` | 1 | array |
| `domain` | 1 | array |
| `domain_alternate` | 1 | array |
| `domain_primary` | 1 | array |
| `duration` | 3 | number |
| `duration_raw` | 3 | string |
| `element` | 3 | array |
| `favored_weapon` | 1 | array |
| `feat` | 2 | array |
| `fortitude_proficiency` | 1 | string |
| `frequency` | 2 | string |
| `hands` | 2 | string |
| `heighten` | 2 | array |
| `heighten_level` | 2 | array |
| `hp` | 2 | number |
| `hp_raw` | 2 | string |
| `is_general_background` | 1 | boolean |
| `item_child_id` | 1 | array |
| `item_parent_id` | 1 | string |
| `language` | 1 | array |
| `legacy_id` | 11 | array |
| `level` | 8 | number |
| `onset` | 1 | number |
| `onset_raw` | 1 | string |
| `patron_theme` | 1 | array |
| `perception_proficiency` | 1 | string |
| `pfs` | 12 | string |
| `prerequisite` | 3 | string |
| `price` | 3 | number |
| `price_raw` | 3 | string |
| `primary_check` | 1 | string |
| `primary_source` | 18 | string |
| `primary_source_category` | 18 | string |
| `primary_source_raw` | 18 | string |
| `range` | 3 | number |
| `range_raw` | 3 | string |
| `rarity` | 18 | string |
| `reflex_proficiency` | 1 | string |
| `release_date` | 18 | string |
| `reload` | 1 | number |
| `reload_raw` | 1 | string |
| `remaster_id` | 18 | array |
| `requirement` | 6 | string |
| `resistance` | 18 | object |
| `sanctification` | 1 | array |
| `saving_throw` | 2 | string |
| `school` | 4 | string |
| `secondary_casters` | 1 | number |
| `secondary_check` | 1 | string |
| `size` | 1 | array |
| `skill` | 5 | array |
| `skill_mod` | 18 | object |
| `skill_proficiency` | 1 | array |
| `source` | 18 | array |
| `source_category` | 18 | array |
| `speed` | 18 | object |
| `speed_penalty` | 1 | string |
| `speed_raw` | 1 | string |
| `spell` | 2 | array |
| `spell_type` | 1 | string |
| `stage` | 1 | array |
| `strength` | 1 | number |
| `target` | 2 | string |
| `tradition` | 2 | array |
| `trait` | 15 | array |
| `trigger` | 4 | string |
| `type` | 18 | string |
| `usage` | 1 | string |
| `vision` | 1 | string |
| `weakness` | 18 | object |
| `weapon_category` | 1 | string |
| `weapon_group` | 1 | string |
| `weapon_type` | 1 | string |
| `will_proficiency` | 1 | string |
