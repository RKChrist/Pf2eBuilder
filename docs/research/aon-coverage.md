# Archives of Nethys category coverage for the character builder

Status: research, read-only. Measured against the live Elasticsearch index `aon-20260902-190924` on 2026-09-18, the same index the existing snapshot came from.

Counts below are raw index counts. Legacy and Remaster records coexist and are linked by `remaster_id`, so most counts roughly halve after filtering. Verified example, `shield` returns 32 documents that are 16 distinct shields duplicated across the two editions.

## Headline

The snapshot's gap is not the three suspected bugs. It is that **every 1st-level subclass choice in the game lives in its own category, and none of them were pulled**. The `class-feature` records we have contain the empty slot, not the options. Searching the local `class-feature.ndjson.gz` finds exactly one record named `Bloodline`, one named `Instinct`, one named `Rogue's Racket`, one named `Doctrine`, one named `Muses`, and one named `Hunter's Edge`. Those are the feature entries that say "choose one". The things you choose are in `bloodline` (28), `instinct` (16), `racket` (10), `doctrine` (5), `muse` (9), and `hunters-edge` (7), none of which are in the snapshot.

A sorcerer cannot be built at all without `bloodline`, because the bloodline sets the spellcasting tradition. A cleric cannot be built without `doctrine`, because cloistered and warpriest differ in armor proficiency, weapon proficiency, and HP. A psychic cannot be built without `subconscious-mind`, because that record carries the key attribute.

## Recommended final category list

Keep all 18 already pulled. Add the following.

**Tier 1, engine correctness.** shield, item-bonus, source, weapon-group, armor-group, domain.

**Tier 1, class and subclass selectors.** bloodline, instinct, muse, patron, lesson, mystery, doctrine, racket, hunters-edge, arcane-thesis, arcane-school, druidic-order, way, methodology, conscious-mind, subconscious-mind, research-field, innovation, style, cause, tenet, implement, eidolon, apparition, practice, ikon, epithet, element, hybrid-study, grim-fascination, fatal-method, runesmith-rune, tactic, draconic-exemplar.

**Tier 2, companions and pets.** animal-companion, animal-companion-specialization, animal-companion-advanced, familiar-ability, familiar-specific.

**Tier 2, optional rules a character can opt into.** mythic-calling, deviant-ability-classification, hellknight-order, follower, animal-companion-unique.

**Tier 3, content the sheet displays but does not compute.** relic, set-relic, class-kit, rules, curse.

**Declined.** class-sample, disease, plane, vehicle, siege-weapon, campsite-meal, cult-activity, warfare-army, warfare-tactic, weather-hazard, creature-theme-template, article, sidebar, category-page, deity-category, tradition, skill-general-action, and the out-of-scope bestiary set.

Total records added at Tier 1 and Tier 2, before remaster deduplication, is roughly 2,900. Most of that is `item-bonus` at 1,369.

## Verdict table

### The three suspected bugs

| Category | Count | Verdict | Rule-grounded reason |
|---|---|---|---|
| `shield` | 32 | **Confirmed bug. Add.** | Raise a Shield grants a circumstance bonus to AC equal to the shield's AC bonus, and Shield Block absorbs damage up to the shield's Hardness, applies the remainder to the shield's HP, and breaks the shield at its Broken Threshold. Only these 32 records carry `ac`, `hardness`, and `hp_raw`. Base shields are absent from the snapshot entirely. `equipment.ndjson.gz` has zero records named `Buckler`. It has 186 records with `item_category` "Shields", but every one of them is a specific or magic shield, and **zero of the 186 carry an `ac`, `hardness`, or `hp` field**. Their defensive numbers exist only in prose. AC cannot be computed correctly for any shield user today. |
| `item-bonus` | 1369 | **Confirmed bug. Add.** | These are not a separate rules concept. They are a per-bonus projection of `equipment`, one row per bonus an item grants, with the document id encoding the parent, for example `equipment-257-bonus-103` for Horn of Blasting. They carry `item_bonus_value`, `item_bonus_note`, `item_bonus_consumable`, and `skill`. It is **not derivable** from what we pulled. The parent `equipment` record in the snapshot has a `skill` field naming the affected skill but no value, no note, and no consumable flag, and its `skill_mod` object is empty on every record in the file. Since only the highest item bonus of a given type applies under the stacking rules, the engine needs the number, and the number is only here. |
| `source` | 254 | **Confirmed. Add.** | This is the rulebook list the enable and disable feature needs. Each record is `{id, name, source_category, release_date, url}`, with `source_category` values such as Rulebooks, Lost Omens, Adventure Paths, and Adventures, which is the natural grouping for the toggle UI. Every content record already carries `source`, `primary_source`, and `source_category`, so the filter joins on the source name. |

### Also decided

| Category | Count | Verdict | Rule-grounded reason |
|---|---|---|---|
| `familiar-ability` | 191 | Add | The witch's familiar is a class feature at 1st level, not an option, and the witch selects familiar abilities each day against a numeric budget. The wizard's `arcane-thesis` option "Improved Familiar Attunement" grants extra abilities, and the Familiar Master archetype builds on the same list. `ability_type` separates Familiar abilities from Master abilities, which are chosen from different pools. A witch sheet is **wrong** without this, because the familiar's ability budget is part of the character. |
| `familiar-specific` | 47 | Add | Specific familiars such as the Fey Dragonet come with a fixed `familiar_ability` list and a `required_abilities` count that consumes part of the same budget. Without it a builder can offer generic familiars only, which makes the sheet **incomplete**, not wrong. |
| `animal-companion` | 115 | Add | Required by the ranger (Animal Companion feat), the druid (Animal order), the champion (Blessed Mount line), and the Beastmaster and Cavalier archetypes. The record is a full stat block with six attribute modifiers, `hp`, a `speed` map, `sense`, `size`, `skill`, and a `mount` flag. A sheet with a companion class feature and no companion stats is **wrong**. |
| `animal-companion-specialization` (17), `animal-companion-advanced` (8) | 25 | Add | Mature, nimble, savage, and specialized companions are the companion's level progression. Omitting them freezes every companion at its 1st-level form, which is **wrong** past roughly 6th level. |
| `animal-companion-unique` | 2 | Tier 2 | Adventure-path companions, carrying a `spoilers` field. Optional. |
| `domain` | 124 | Add | The cleric's deity grants domains, the Domain Initiate feat grants a domain spell, and Advanced Domain grants the advanced one. The `deity` records we pulled name their domains but do not map a domain to its spells. The `domain` record carries `domain_spell`, `advanced_domain_spell`, and `apocryphal_spell`. Partially recoverable from the 126 spells tagged `domain` in the snapshot, but the authoritative mapping is here. A cleric sheet is **wrong** if Domain Initiate cannot resolve to a spell. |
| `curse` | 92 | Tier 3 | These are afflictions and cursed items, GM-applied. One sample has `usage` "curses armor or shield", so a cursed item can end up on a sheet, but nothing in character creation selects one. Display only. |
| `disease` | 44 | Decline | Afflictions applied in play, with `stage` and `saving_throw`. No character option grants or selects one. |
| `relic` (219), `set-relic` (14) | 233 | Tier 3 | Relic gifts are an optional subsystem a GM enables. When enabled, gifts do change a character, so store them, but no class requires them. Sheet is **incomplete**, not wrong. |
| `class-sample` | 116 | Decline | Sample builds are editorial guidance, carrying a `class` and a name and nothing mechanical. A builder generates these, it does not consume them. |
| `class-kit` | 32 | Tier 3 | Starting equipment packages with `price` and `bulk`, a genuine convenience at character creation. The itemized contents exist only in the markdown body, so using it means parsing prose. Nice to have, never a correctness issue. |
| `runesmith-rune` | 44 | Add | The runesmith class applies runes as its core loop, and the class is unplayable without the rune list. Records carry `element`, `usage`, `level`, and traits. A runesmith sheet is **wrong** without it. |
| `draconic-exemplar` | 44 | Add | The Remaster's dragon-type table, served from `/Bloodlines.aspx`, carrying `spell`, `skill`, and `tradition`. Selected by the draconic sorcerer bloodline, the dragon barbarian instinct, and dragon-themed archetypes. Any character that picks a dragon type is **wrong** without it, because the granted spells and tradition come from here. |
| `tactic` | 37 | Add | The commander class prepares a tactics deck. Records carry `tactic_type` and `actions_number`. A commander sheet is **wrong** without it. |
| `plane` | 47 | Decline | Setting reference. Nothing on a character sheet reads it. |
| `vehicle` | 138 | Decline | Not character equipment. Piloting a vehicle is a GM-side subsystem with crew, passengers, and its own AC and saves. |

### Additional gaps not in the brief

These were absent from both the pulled list and the not-pulled list in the brief, because they sit in the "about 355 more in smaller buckets" tail. They are the most important omissions in the whole survey.

| Category | Count | Class it belongs to | Verdict |
|---|---|---|---|
| `bloodline` | 28 | Sorcerer | Add. Sets spellcasting tradition, granted spells, blood magic, and two trained skills. Without it the sorcerer has no tradition. |
| `instinct` | 16 | Barbarian | Add. Sets rage damage, specialization ability, and anathema. |
| `muse` | 9 | Bard | Add. Grants a feat and additional spells. |
| `patron` | 27 | Witch | Add. Carries `tradition`, `hex_cantrip`, `skill`, and granted `spell`. Without it the witch has no tradition. |
| `lesson` | 31 | Witch | Add. `lesson_type` separates basic, greater, and major. Each grants a hex. |
| `mystery` | 22 | Oracle | Add. Carries `spell`, `skill`, and `domain`. |
| `doctrine` | 5 | Cleric | Add. Cloistered and warpriest differ in armor proficiency, weapon proficiency, and HP progression. |
| `racket` | 10 | Rogue | Add. Sets the key attribute option and the extra trained skill. |
| `hunters-edge` | 7 | Ranger | Add. Flurry, precision, and outwit change the attack math. |
| `arcane-thesis` | 10 | Wizard | Add. |
| `arcane-school` | 27 | Wizard | Add. |
| `druidic-order` | 13 | Druid | Add. Grants an order spell, a skill, and a feat. |
| `way` | 11 | Gunslinger | Add. Carries `skill`, grants a slinger's reload and a deed. |
| `methodology` | 9 | Investigator | Add. |
| `conscious-mind` | 12 | Psychic | Add. Carries the surge and the granted `spell` list. |
| `subconscious-mind` | 8 | Psychic | Add. Carries `attribute`, which **is the psychic's key attribute**. |
| `research-field` | 8 | Alchemist | Add. |
| `innovation` | 7 | Inventor | Add. Armor, weapon, or construct changes what the character wields. |
| `style` | 11 | Swashbuckler | Add. Sets the panache-granting skill action. |
| `cause` | 13 | Champion | Add. Carries `alignment` and the remaster rename in `remaster_name`. |
| `tenet` | 2 | Champion | Add. Two records, trivial cost. |
| `implement` | 19 | Thaumaturge | Add. |
| `eidolon` | 26 | Summoner | Add. A full creature entity with `size`, `sense`, `speed_raw`, `language`, `tradition`, `skill`, and `home_plane`. |
| `apparition` | 14 | Animist | Add. Carries the granted `spell` list and `skill`. |
| `practice` | 4 | Animist | Add. |
| `ikon` | 21 | Exemplar | Add. Carries `usage` for worn, weapon, and body ikons. |
| `epithet` | 18 | Exemplar | Add. Carries `level`, separating root from later epithets. |
| `element` | 6 | Kineticist | Add. Element selection gates every impulse feat. |
| `hybrid-study` | 15 | Magus | Add. Carries the granted `spell` list. |
| `grim-fascination` | 4 | Necromancer | Add. |
| `fatal-method` | 9 | Necromancer | Add. |
| `weapon-group` | 17 | All martial characters | Add. Weapon critical specialization effects are keyed by group, and `weapon` records already carry `weapon_group`, so this is the missing lookup half. |
| `armor-group` | 7 | All armored characters | Add. Armor specialization effects at 13th level are keyed by group. |
| `mythic-calling` | 15 | Mythic rules | Tier 2. Carries `edict` and `anathema`. Only relevant when the GM enables mythic play. |
| `deviant-ability-classification` | 10 | Deviant feats | Tier 2. Optional Dark Archive subsystem. |
| `hellknight-order` | 14 | Hellknight archetypes | Tier 2. Carries `favored_weapon`. |
| `follower` | 6 | Captain archetype | Tier 2. Confirmed by the Battlecry! feats Additional Follower, Experienced Follower, and Veteran Follower, all tagged `archetype: ["Captain"]`. Records are minion stat blocks with four attribute modifiers and HP. |
| `deity-category` | 40 | none | Decline. `deity` records already carry `deity_category` and `deity_category_order`. |
| `tradition` | 5 | none | Decline. Spells already carry `tradition`. |
| `skill-general-action` | 25 | none | Decline. Already duplicated into `action`. Decipher Writing is present in the snapshot's `action.ndjson.gz`. |
| `siege-weapon` | 84 | none | Decline. Crewed equipment with its own AC, saves, and crew count. |
| `campsite-meal` (27), `cult-activity` (5), `warfare-army` (11), `warfare-tactic` (21), `weather-hazard` (12), `creature-theme-template` (16) | 92 | Decline | Adventure-path subsystems, tagged with `source_group` Kingmaker or Myth-Speaker. Same class of thing as the kingdom categories already ruled out. |
| `rules` (3659), `sidebar` (709), `article` (107), `category-page` (287) | 4762 | `rules` Tier 3, rest decline | `rules` is the body text of the rulebooks, chunked and linked by `next_link`. It computes nothing, but it is the only place the prose of a rule such as bonus stacking lives, so it is worth having behind an in-app reference view. `sidebar` and `article` are editorial. `category-page` is AoN's own navigation. |

## On the out-of-scope list

Agreed, with one caveat. `creature`, `creature-family`, `creature-ability`, `creature-adjustment`, `hazard`, `kingdom-structure`, and `kingdom-event` have no place in a builder with no encounter side. The caveat is `creature-ability`. Some minion and companion abilities may reference it, so if a companion or eidolon stat block turns out to link outward to a shared ability list, a narrow slice may be needed later. Nothing in the `animal-companion` or `eidolon` field shapes inspected here does that, so it stays out for now.

## Field shapes of the newly recommended categories

Common envelope on every record, already handled by the existing ingest. `id`, `name`, `category`, `type`, `url`, `source`, `source_raw`, `source_category`, `primary_source`, `primary_source_raw`, `primary_source_category`, `release_date`, `rarity`, `rarity_id`, `pfs`, `exclude_from_search`, and where applicable `trait`, `trait_raw`, `trait_group`, `remaster_id`, and `remaster_name`. Several empty placeholder objects appear on every record and can be ignored, namely `speed`, `weakness`, `resistance`, `skill_mod`, and `is_standard_ancestry_feat`.

**`shield`.** `ac` (integer, the circumstance bonus), `hardness` (integer), `hardness_raw` (string), `hp` (integer), `hp_raw` (string of the form `"20 (10)"`, HP then Broken Threshold), `bulk` (float), `bulk_raw`, `price` (integer copper), `price_raw`, `level`, `item_category` "Shields", `item_subcategory` "Base Shields".

**`item-bonus`.** `item_bonus_value` (integer), `item_bonus_note` (string describing when it applies), `item_bonus_consumable` (boolean), `skill` (array), plus a copy of the parent item's `level`, `price`, `bulk`, `usage`, `item_category`, and `trait`. The `id` is `<parent equipment id>-bonus-<n>`.

**`source`.** `name`, `source_category` (array), `release_date`, `url`. Nothing else.

**Subclass selectors** (`bloodline`, `patron`, `mystery`, `lesson`, `hybrid-study`, `conscious-mind`, `apparition`, `draconic-exemplar`, `domain`, and the rest). The shape is consistent and thin. `name`, plus some subset of `spell` (array of granted spell names), `skill` (array of granted trained skills), `tradition` (array), `attribute` (array, on `subconscious-mind`), `domain` (array, on `mystery`), `level` (on `epithet`), `usage` (on `ikon` and `runesmith-rune`), `element` (on `element` and `runesmith-rune`), `lesson_type`, `tactic_type`, `hex_cantrip`, `favored_weapon`, `edict`, `anathema`, `alignment`, and `bloodline`. The mechanical body text is in the excluded `markdown` field.

**`domain`.** `domain` (array), `domain_spell`, `advanced_domain_spell`, `apocryphal_spell`, `spell` (array of all three), `deity` (array of every deity with that domain).

**`animal-companion`.** `strength`, `dexterity`, `constitution`, `intelligence`, `wisdom`, `charisma` (signed integer modifiers), `hp`, `hp_raw`, `speed` (object keyed by movement type plus a `max`), `speed_raw`, `size` and `size_id` (arrays), `sense` (string), `skill` (array), `mount` (boolean), `level`, `trait`.

**`eidolon`.** `size` and `size_id` (arrays, some eidolons offer two), `sense`, `speed_raw`, `language` (array), `tradition`, `skill`, `home_plane`, `alignment`, `trait`.

**`familiar-ability`.** `ability_type`, either "Familiar" or "Master". Everything else is envelope.

**`familiar-specific`.** `familiar_ability` (array of names), `required_abilities` (integer).

**`weapon-group` and `armor-group`.** The group name in `name`, plus `armor_group` on the armor side. The critical specialization effect is in the markdown body.

**`follower`.** `strength`, `dexterity`, `constitution`, `intelligence`, `wisdom`, `charisma`, `hp`, `hp_raw`, `trait: ["Minion"]`.

**`relic` and `set-relic`.** `aspect` (array), `element` (array), `item_category` "Relics", `trait`.

**`class-kit`.** `price`, `price_raw`, `bulk`, `bulk_raw`, `navigation` (links back to the class). Contents are in the markdown body.

## What needs a new column or entity

1. **Shield is a third equipment kind, not a row in equipment.** It needs `ac_bonus`, `hardness`, `hp`, and `broken_threshold` as typed integer columns. `hp_raw` is a single string `"20 (10)"` that must be split at ingest into HP and Broken Threshold. Nothing in the current equipment schema has a home for any of these, and the 186 shield-flavored equipment rows we already have cannot be back-filled from their own fields.

2. **Item bonuses are one-to-many against equipment.** A single item can grant several, so this is a child table keyed on the parent equipment id parsed out of the `item-bonus` document id, with columns `value`, `note`, `skill`, `consumable`. It is not a column on the equipment row.

3. **Source needs a real foreign key, and the join is dirty.** Content records reference their book by name string, and the names are not normalized against the `source` table. "Guns & Gears" and "Guns & Gears (Remastered)" are distinct entries, and at least one source name in the index carries a trailing carriage return and newline. Resolve names to `source_id` once at ingest and fail loudly on a miss, rather than string-matching at query time inside the rulebook filter.

4. **Companions are creatures, not blobs.** `animal-companion`, `eidolon`, and `follower` each carry six attribute modifiers, HP, a speed map, senses, and size. They need a companion entity with typed columns and a link to the character, plus a level-progression join to `animal-companion-specialization` and `animal-companion-advanced`. Storing a companion as a JSON blob on the character makes the companion's own level progression unqueryable.

5. **Familiar ability selection is a budget, not a list.** `required_abilities` on a specific familiar consumes part of the same pool the character spends on `familiar-ability` picks, and `ability_type` splits that pool in two. The model is a familiar entity with a numeric budget and a many-to-many to abilities, validated against the budget.

6. **The thirty-odd subclass categories are one domain concept.** They are all "the choice a class makes at 1st level that grants spells, skills, a tradition, or a key attribute". Model them as one `class_option` table with a `kind` discriminator and a `class_id`, not thirty tables and not thirty code paths. The columns that actually vary are `granted_spells`, `granted_skills`, `tradition`, and `key_attribute`. Note that `class_option` is the only place a **key attribute can be overridden by a subclass pick**, via `subconscious-mind.attribute` for the psychic, so the character's key attribute cannot be a plain column on the class row.

7. **Remaster deduplication must extend to every new category.** `shield` is exactly 2x duplicated, and `remaster_id` linkage is present on `plane`, `relic`, `domain`, `animal-companion`, `familiar-ability`, `familiar-specific`, `bloodline`, `instinct`, `doctrine`, and most of the subclass set. Whatever filter the existing ingest applies to the 18 pulled categories has to apply unchanged here, or every sorcerer bloodline will appear twice in the picker.
