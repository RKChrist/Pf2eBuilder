# 010 — Exploration, Downtime and Reference

**Status**: accepted
**Completes the mode shell from 007 and 009.**

Encounter was built first because it is the mode with the most rules in it. This record covers
the other three, and the two things the design document asks for that I first said the data could
not support and was wrong about twice.

## Exploration

One picker per character, nine activities, and the consequence printed under the choice.

Three of the nine reach the fight. **Avoid Notice** rolls the skill it names for initiative, so a
rogue sneaking ahead goes first off their own Stealth with every condition and buff already on
it. **Scout** hands the party a +1 circumstance bonus. **Defend** leaves a shield raised whose
bonus this engine does not know, so it becomes a reminder at the top of the fight rather than a
number invented for it. The other six change nothing, and the screen says so by printing what
they do rather than implying a mechanical effect.

**The rule is a pure function, not a branch in the handler that rolls.** `Initiative.Modifier`
takes a Perception, an activity, a way to look up a skill, a party bonus and whether the creature
is an ally. It is there because the first version of these tests asserted the rule through the
die — twenty rolls staying inside a range — which proves nothing and fails a correct
implementation about one run in eight. The persistence tests assert the wiring; the domain tests
assert the arithmetic, with no die in sight.

The scout's bonus is returned inside the character branch so a monster cannot reach it. "You and
your allies" is the printed wording and an ogre is neither.

## Camp

Four ten-minute activities and a full night, split the way the rules split.

**None of the ten-minute activities changes a number.** Treat Wounds heals 2d8 on a success and
4d8 on a critical one, and this app does not roll for a table with dice in front of it. What it
keeps is the half people forget: the target is immune for an hour, the button goes off while it
runs, and the refusal says how many minutes are left.

**A full night is the opposite case and the engine does all of it**, because there is nothing to
roll. Constitution times level in hit points, at least one per level. Temporary hit points
expire. Fatigued ends, drained and doomed step down by one, and everything else is still there in
the morning — a lookup rather than a "clear everything", so that sleeping does not quietly delete
the GM's curse.

The clock only goes up, and reads in words. Nobody has ever said "four hundred and eighty
minutes" out loud about a night's sleep.

## Downtime

A day counter, one activity per character per day, and the level-based DC.

**The DC table is the oracle and the formula is the implementation.** The twenty-six printed rows
are typed out in `DowntimeRules.PrintedTable` and the code is checked against them, not the other
way round. A formula that had drifted would put a wrong DC on every downtime roll and nobody
would catch it, because the app would be confidently consistent. Off the end of the table it
clamps: a task level of 40 is somebody mistyping, and answering 70 with a straight face is worse
than answering the highest number the book prints.

## Reference

The catalogue already answers "what does this condition do" for every condition, action and
skill, searchable, with trait sheets and deep links. The half that was missing is the party's
own.

A character's feats and spells now come off the Pathbuilder export and are joined to the seeded
records **by name, at import**, so a row opens the rule. All 26 of this bard's open, which took
closing the gap below.

## The names that did not join, and now do

Three did not open: `Inspire Competence`, `Inspire Heroics` and `Dimension Door`. All three are
pre-Remaster names whose records are called something else now — Uplifting Overture, Fortissimo
Composition and Translocate.

The first version of this record said there was no alias table to follow, because the pull drops
every superseded record **at pull time** and the snapshot holds only survivors. That was true of
the snapshot and not true of the source, and the difference is the fix.

`rules-import aliases` asks Archives of Nethys for every record carrying a `remaster_id` and
takes **the name, the category and the successor id, and nothing else**. Not a level, not a
trait, not a price: the superseded record's mechanics are not wanted and are not taken. Names are
what the Community Use Policy covers and names are what the file is. Check 12 of the licensing
audit enforces exactly that, failing on any fourth key.

The raw answer is 11,881 names. The transform trims it twice:

- **8,650 are renumberings, not renames.** The record moved and kept its name, so a direct name
  match already finds it and an alias agreeing with the name would only be a second route to the
  same answer.
- **13 are ambiguous.** "Ability Boosts" was a class feature on twenty-one classes and each
  renamed to its own; answering with one of the twenty-one would be a coin toss dressed as a
  lookup. An old name with two answers is not a rename.

What survives is 1,367 genuine renames, 151 KB, seeded into `RuleAliases`. The importer tries the
name first and the index second, so a current name can never be redirected by an old one.

The name on the sheet stays the one the player typed. Their character sheet says Inspire
Competence, and renaming it under them would be its own surprise; what changes is that it now
opens.

## "What can I attempt"

The document asks for a per-character list of every exploration skill action with its requirement
met or not, and its walkthrough is *Rune's PC can attempt Decipher Writing because his sheet says
trained in Society.*

The blocker was real and the conclusion drawn from it was wrong. Of the 38 actions carrying the
Exploration trait, 7 state a requirement at all and one of those is a plain proficiency phrase;
Decipher Writing states none, because its "trained in Arcana, Occultism, Religion, or Society"
lives in rule prose this project does not import. So the requirement cannot come from the seed.

It does not have to. `Pf2e.Domain.SkillActions` is the same shape as `Buffs`, `Conditions`,
`ExplorationActivities`, `CampActivities` and `DowntimeActivities`: hand-written against the
printed rules, every entry `Verified` false and naming its page. Forty-six actions, each with the
skills it can be rolled with and the rank it needs in one of them.

Two things the model has to keep apart, which the walkthrough itself shows:

- **Whether you may attempt it** is met by *any* of the skills the action allows. This bard is
  trained in Society and untrained in Arcana, so Decipher Writing is open to him.
- **What you roll** is the *best* of them. He is expert in Occultism, so that is the number,
  not Society. My first test asserted Society and the code was right.

The server decides both, from the character's own ranks, and hands the screen a list. A screen
that decided it would be a second copy of the rules and the one that goes stale. Seek is rolled
with Perception, which is not a skill, so a name the skill list does not hold falls through to the
sheet's own Perception rather than to zero.

The ones a character can try come first, sorted by the number, because a player scanning this is
looking for the thing they are best at. The ones they cannot are behind a summary rather than
hidden, because "why can nobody here read this?" is a question a table asks.
