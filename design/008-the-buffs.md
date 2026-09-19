# 008 — The buffs

**Status**: accepted, partly unverified
**Supersedes nothing. Extends 005.**

## What happened

The owner opened the tracker and said: *"stuff like raise shield is not something you have
defined."*

They were right twice over.

Raise a Shield existed in the catalogue three times, once per printing, because the ingest was
seeding every record Archives of Nethys itself flags `exclude_from_search`. That is fixed in the
merge that precedes this record: actions went from 3,921 to 551 and every superseded duplicate
across every category is gone.

And Raise a Shield existed nowhere in the engine. The registry held fourteen conditions and no
bonuses at all. A bard could sing and the tracker showed nothing, which is the opposite of the
thing this product is for.

## The decision

One registry, two kinds. `Effects.All` is `Conditions.All` plus `Buffs.All`, and everything that
resolves an effect by key goes through it. A buff and a condition are the same shape because the
stacking rule does not care which a modifier came from; `EffectKind` exists only so a picker can
say which is which instead of offering nineteen rows in one undifferentiated list.

`ModifierTemplate` gains `Bonus` and `ScalingBonus` beside the existing `Flat` and `Scaling`. The
penalty templates negate their value; the bonus ones do not. That asymmetry is deliberate: a
condition's value is its severity and a buff's value is its size, so clumsy 2 is -2 and a raised
tower shield at 4 is +4.

Three buffs carry a value rather than a fixed number, because the printed rule does.

- **Raise a Shield** is the shield's own bonus. A buckler is 1, most shields are 2, a tower
  shield behind which you have also Taken Cover is 4. Storing 2 for everyone would quietly
  overstate a buckler and understate a tower shield.
- **Cover** is 1 lesser, 2 standard, 4 greater.
- **Heroism** is 1 at rank 3, 2 at rank 6, 3 at rank 9.

An effect can link to a record that is not called what the effect is called. Cover is the effect;
Take Cover is the action the book prints it under. `RuleCategory` and `RuleName` carry that, and
`RuleCategory` is also what stops the Shield *spell* linking to a steel shield's shopping entry,
because both are named "Shield".

## What is in, and what the numbers are

| Effect | Type | Applies to | Where it is printed |
|---|---|---|---|
| Raise a Shield | circumstance, valued | armour class | Player Core pg. 419 |
| Cover | circumstance, valued | armour class | Player Core pg. 432 |
| Shield | circumstance +1 | armour class | Player Core pg. 349 |
| Bless | status +1 | attack rolls | Player Core pg. 322 |
| Courageous Anthem | status +1 | attack rolls, damage | Player Core pg. 330 |
| Rallying Anthem | status +1 | armour class, saving throws | Player Core pg. 341 |
| Heroism | status, valued | attack rolls, Perception, saving throws, skills | Player Core pg. 337 |

Every one of them carries `Verified = false`. Archives of Nethys publishes the name and the page
of each and the structured effect of none, and this project does not import rule text, so these
numbers were written out by hand against the printed rule. Check them before a session that
matters. The page is in the record so checking one takes a minute.

## What is deliberately absent, and why

**Aid and Guidance.** Both grant a bonus to one check the player names. This engine can say
"every check" and cannot say "the next one". Applying them would inflate every number on the
sheet instead of the one that was aided, which is a larger error than their absence. Use a custom
effect until a modifier can wait for the roll it belongs to.

**Haste.** An action, not a modifier. Nothing to compute.

**The half of a rule a predicate would need.** Courageous Anthem also covers saves against fear;
cover also covers Reflex saves against area effects and Stealth to Hide and Sneak; Rallying
Anthem also grants resistance to physical damage. None of those can be said without a predicate
on the trait of the thing being saved against, or on the action being taken, or a resistance
model. Each omission is written into the definition's own comment rather than left for a reader
to discover from a wrong number.

## The gap this opened, and how it was closed

The sheet computed armour class, three saves, Perception and a class DC. Bless and Courageous
Anthem land on attack rolls and damage, so both were applied, both appeared as a chip, and
neither changed a number anyone could see.

The data was already in the Pathbuilder export and already discarded at import: a rank for every
skill, the two Lores the player wrote down, the four casting proficiencies with their attribute,
and a weapons array. The sheet now computes all of it.

Two judgements went into that, and they went opposite ways.

**A skill is recomputed and a weapon is not.** Skills come out of `proficiencies`, which states
a rank for every one of the sixteen, so level plus rank plus attribute is the whole answer.
Weapons do not: a class grants proficiency in the weapons it names, and no field of the export
says so. This bard reads `martial: 0` and hits at +15 with a rapier, because a bard is trained
in rapiers by name. Recomputing gives +4. So the weapon's bonus is the export's own total, taken
whole, with the session's modifiers stacked onto it.

**The governing attribute still comes from the ruleset.** A bonus can be taken on trust; a
selector cannot, because clumsy has to reach a finesse rapier and must not reach a greatsword.
The seeded weapon record carries `weapon_type` and a Finesse trait, so ranged is Dexterity,
finesse is the better of the two, and everything else is Strength. The traits are a column of
their own rather than part of the mechanics JSON, which the first attempt got wrong: reading
`mechanics["trait"]` found nothing and quietly made every weapon Strength-governed.

Storage. Skills and weapons are one JSON column each on the tracked character, not two more
tables. They are read whole, replaced whole on re-import, and nothing queries inside them. An
`ImmutableArray` compares by reference, so the value comparer compares the stored form, which is
what makes EF see a real change and only a real change.

On screen the card now shows the statistics the character actually has, so a fighter gets no
empty spell attack; the attacks; the trained skills; and the untrained ones behind a summary,
because a player reads the four they are good at and a DM occasionally asks for an untrained
Nature. A roll is written with a sign and a DC is not.

`tools/ui-check/verify-effects.mjs` drives all of it in a real Chrome on a phone viewport: it
imports the sample bard, opens the picker, raises a shield, blesses the bard, and asserts that
armour class moved by one, the rapier and the spell attack moved by one, and Performance did
not.

## Why the registry is hand-written, and what the ingest can extract

The obvious alternative to writing these seven out by hand is reading them out of the rule text
at ingest. That is not possible here, and the reason is worth recording so nobody tries it a
second time: **the snapshot carries no prose at all.** The pull does not fetch rule text, by
licence, so a spell record states its tradition, its level, its range and its traits and not one
word of what the spell does. There is no description field to parse. `design/005` said
extraction at ingest would store structure and not prose; what it did not say is that there is
no prose to extract structure from.

What the ingest can extract is the handful of places Archives of Nethys publishes a number as a
field of its own. There is exactly one that yields a modifier today: an item bonus to a skill.
987 `item-bonus` records state `item_bonus_value`, and 829 of them name the `skill` it applies
to. `tools/rules-import/Modifiers.cs` turns those into the `modifiers` array the seed carries and
`RuleModifiers.Of` reads, so the effect picker's rules arm now applies 829 records where it used
to apply none.

The other 158 qualify a Perception bonus by the action being taken, such as "Perception rolls for
initiative". That is a predicate this engine has no way to say, so they state nothing rather than
stating something wrong, the same call as cover's Reflex saves against area effects.

Two other fields look like modifiers and are not. Armour's `check_penalty` and `speed_penalty`
belong to the build rather than to the session, and the check penalty applies to
Strength-based and Dexterity-based *skills*, which needs a selector the engine does not have:
`Governed` reaches attack rolls and damage too. A creature's `ac`, saves, `skill_mod` and
`attack_bonus` are its statistics, not modifiers it grants.

## What is still missing

Damage is not on the sheet, so the damage half of Courageous Anthem still lands nowhere visible.
A damage roll needs the weapon's dice and its striking rune, which the export states and this
import does not yet read.

A dual-class character has two spellcasting blocks and this shows the first.
