# 004. The tracker

Decided 19 September 2026. This is the feature the product exists for, and until now it
did not exist. What shipped was a rules browser: you could look things up, but there were
no characters, nothing was saved, and nothing could be updated.

## The two layers, and why they are separate

A character has a build and a session, and they change on completely different clocks.

**Build** changes at level-up. Name, level, class, ancestry, attribute modifiers,
proficiency ranks, armour, the inputs to maximum hit points. It arrives from a Pathbuilder
import and is replaced wholesale on re-import.

**Session** changes every few minutes at the table. Current hit points, temporary hit
points, which conditions are on you and at what value, hero points.

Re-importing after a level-up replaces the build and leaves the session alone. A player who
levels up mid-session does not get their hit points reset to full, which is the bug this
separation exists to prevent.

## The computation is one pure function

```
Sheet Compute(Character build, SessionState session, RuleOptions options)
```

No database, no HTTP, no clock. It lives in `Pf2e.Domain` beside the rules engine and is
testable without any of them. Everything else is plumbing around it.

A `Sheet` carries a `Breakdown` per statistic rather than a number: armour class, the three
saves, Perception, class DC, and the skills. The breakdown already exists and already names
every contributing modifier and everything the stacking rule suppressed, which is what makes
"why is my Will save 14" answerable by tapping it.

Conditions reach the calculator as `(key, value)` pairs, are resolved through
`Conditions.All` into modifiers, and are stacked per statistic. That path is already built
and already correct about clumsy being every Dex-based statistic. The tracker is what finally
makes it visible.

Drained is the exception that proves the separation. It reduces maximum hit points by its
value times the character's level, which no modifier can express, so `HitPoints.DrainedLoss`
is applied to the build's maximum rather than stacked onto a statistic.

## What a table actually does

Four operations, and deliberately only four:

| Operation | What changes |
| --- | --- |
| Import a character | Build layer replaced, session preserved |
| Change hit points | Session, by a delta rather than an absolute, so two people cannot clobber each other |
| Apply or remove a condition | Session |
| Read the party | Nothing |

Hit points move by delta and not by assignment. Two players applying damage at once should
produce the sum, and last-write-wins on an absolute value silently loses one of them.

## Live updates

Any command recomputes that character's sheet and pushes the whole recomputed sheet to
everyone at the table, which is the decision already recorded in Open decision 4. Every
viewer is entitled to every player character's numbers, so there is nothing to filter and no
projection to get wrong. This is the one place the payload carries real data rather than an
identifier.

## Scope, deliberately small

No authentication yet. A table is a short code, and anyone with the code is at that table.
Phase 3 puts accounts underneath without changing any of the above. No monsters, no
initiative, no encounter. Those belong to the encounter tracker that Open decision 1 put out
of scope, and the monster-privacy problem they bring with them is exactly what this product
avoids by not having them.
