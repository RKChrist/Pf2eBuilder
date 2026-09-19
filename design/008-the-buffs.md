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

## The gap this opened

The sheet computes armour class, three saves, Perception and a class DC. Bless and Courageous
Anthem land on attack rolls and damage, so both are applied, both appear as a chip, and neither
changes a number anyone can see.

The data to close this is already in the Pathbuilder export and already discarded at import: a
rank for every skill, `castingOccult` and its three siblings for spellcasting, and a `weapons`
array carrying each weapon's proficiency and item bonus. Adding skills, weapon attacks and a
spell attack and DC to the sheet is the next piece of work, and it is what makes two of these
seven buffs visible.

`BuffsAtTheTable.AnAnthemChangesNothingOnThisSheetYetBecauseTheSheetHasNoAttackRoll` asserts the
gap so that it breaks the moment attacks join the sheet.
