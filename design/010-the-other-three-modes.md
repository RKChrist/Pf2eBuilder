# 010 — Exploration, Downtime and Reference

**Status**: accepted, with two gaps this record exists to explain
**Completes the mode shell from 007 and 009.**

Encounter was built first because it is the mode with the most rules in it. This record covers
the other three, and the two things the design document asks for that the data cannot support.

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
records **by name, at import**, so a row opens the rule. 23 of this bard's 26 open.

## The first gap: three names that do not join

The three that do not open are `Inspire Competence`, `Inspire Heroics` and `Dimension Door`. All
three are pre-Remaster names whose current records are called something else.

There is no alias table to follow, and the reason is structural rather than an oversight. The
pull drops every record superseded by a `remaster_id` **at pull time**, so the snapshot in this
repo holds 1,836 spells and none of them is the legacy entry that would name the successor. The
mapping exists on Archives of Nethys and does not exist here.

So the row shows the name and says **"no record under this name"**. That wording is deliberate:
homebrew, a feat printed after the snapshot, and a pre-Remaster name are three different reasons
and all three are true of the *name* rather than of the thing it names. Saying "not in the
ruleset" would have been a claim about the feat.

Closing this needs a pull that keeps superseded records for their names alone, which is a
decision about what the seed carries and belongs to whoever next touches the ingest.

## The second gap: "what can I attempt"

The document asks for a per-character list of every exploration skill action with its requirement
met or not met, computed from the sheet, and gives a walkthrough: *Rune's PC can attempt Decipher
Writing because his sheet says trained in Society.*

**This cannot be built from the seed, and the walkthrough's own example is the proof.** Of the 38
actions carrying the Exploration trait, 7 state a requirement at all, and exactly 1 of those 7 is
a plain proficiency phrase. Decipher Writing carries no requirement field: its "trained in Arcana,
Occultism, Religion, or Society" lives in the rule prose, and this project imports no rule prose
by licence. The requirements that are present are mostly equipment — a healer's toolkit, a repair
kit — which is not on the sheet either.

A list titled "what can I attempt" that had not actually checked anything would be worse than no
list, so there is no list. The catalogue already browses actions and filters by the Exploration
trait, and the character card already shows every skill modifier, which is the same information
without the claim.

Closing this needs either the requirement text in the seed, which the licence rules out, or a
hand-written table of action-to-requirement in the shape of `Pf2e.Domain.Buffs` — plausible, and
a decision about scope rather than a thing to do quietly.
