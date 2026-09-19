# 005. Effects and buffs

Decided 19 September 2026.

## The gap

Fourteen conditions produce modifiers. Nothing else does. A bard casting Courageous Anthem
gets a `+1` status bonus to attack and damage in the printed rules and nothing at all in this
app, which means a table cannot use the tracker for the thing that actually happens most
turns.

## A condition and a buff are the same thing

There is one model, not two. An **effect** is a name, a set of typed modifiers, an optional
value it scales with, and a duration. `Conditions.All` becomes the seeded subset of effects
that happen to come from the conditions appendix. Prone, Courageous Anthem, a potency rune and
a GM's homebrew all reach `Stacking.Resolve` the same way, because the stacking rule does not
care where a modifier came from.

This is not a refactor for tidiness. Two models would mean two code paths through the
calculator, and the second one would be the one that gets the stacking rule wrong.

## Where the modifiers come from

Three sources, in order of how much they cover.

**Extracted at ingest.** 6,533 records state a typed bonus or penalty in their own text, and
the phrasing is strikingly regular: "a +1 status bonus to attack rolls", "a +4 status bonus to
damage rolls". A conservative parser over that phrasing covers the overwhelming majority of
what a table actually applies.

The parser runs inside `tools/rules-import`, where the prose is briefly in memory, and emits
only the derived structure: `{ type: status, value: 1, applies: [attack] }`. **The sentence is
never stored.** That keeps the licence position exactly where `NOTICE.md` puts it, since ORC
covers mechanics explicitly and reserves expression. A mechanic extracted from a sentence is a
mechanic.

Conservative means it declines rather than guesses. A phrase it cannot parse with confidence
produces no modifier and is counted, so the miss rate is a number we can look at rather than a
silence. Everything extracted ships `verified: false`, like the condition table before it.

**Hand-authored.** The fourteen conditions already are this, and anything the parser declines
that a table needs often can join them.

**Custom, by the player, in seconds.** This is the part that makes the tracker usable on day
one rather than after someone has curated thousands of records. A player types a name, picks
bonus or penalty, a value, a type and what it applies to, and it behaves exactly like a seeded
effect. The vocabulary is already built: `ModifierType` and `Selector` are the two dropdowns.

Homebrew, a GM ruling, and a buff the parser declined are all the same gesture. A table that
can express anything in ten seconds never hits a wall, which a curated list always eventually
does.

## One application, not two

The rules browser and the tracker are the same app and the same WebAssembly deployable. They
are not two tabs that happen to ship together.

Concretely, the tracker's "add an effect" control **is** the rules search. You are on a
character, you search the same 24,940 records, you tap Courageous Anthem, and its extracted
modifiers apply. Browsing and playing are the same motion, and the rules data stops being a
reference section and becomes the thing the tracker is made of.
