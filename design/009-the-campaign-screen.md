# 009 — The campaign screen

**Status**: accepted
**Implements the client half of 007.**

The backend became a campaign in one pass: a DM key, a mode, an encounter behind a projection,
undo, monsters. The client did not. It still said "Party tracker" at the top, showed the mode as
a word nobody could change, and had no fight in it at all. This record is what the screen became.

## The shell

Three things, above everything, on every screen:

    [ Explore | Fight | Downtime ]        Round 3   DM

The mode belongs to the campaign, not to the screen, so it reads the same on every phone at the
table. **The DM gets a switch and a player gets a sentence.** Not a disabled switch: the server
refuses a player either way, and a control that always fails is worse than no control. A player
reads "In a fight" where the DM reads a strip of three.

The round only appears once initiative has been rolled, because a round number before a fight is
a zero that means nothing.

## The fight

One panel, shown when the mode is Encounter. Initiative down the left, the creature beside it,
and the row whose turn it is marked with a rule down its edge rather than with colour alone.

A monster's row carries its hit points, its armour class, and the same typed damage field the
character cards use. The same field, because a monster loses fifty hit points as easily as a
player does, and because a combatant's id is the creature's id, so `HitPointDrafts` keyed by
Guid already worked for both without a line of new state.

**The reveal toggle is labelled "Players can see it".** A monster the players have not been
shown is not in their payload at all, so this switch is the only thing that puts it there, and a
bare toggle at the end of a row would not say so.

Reminders from the last turn change sit under the order in a quiet box, not a warning box. They
are normal and frequent, and a red border round "Gnibbo is frightened 1" would cry wolf every
round.

## Adding somebody

One sheet, two sources, in the order a DM uses them: the roster first, because it is short and
needs no search, then the bestiary, which needs one. The roster only offers people who are not
already in the fight, since offering somebody twice is offering to give them two turns. A
creature row says its level, armour class, hit points and size, which is what a DM choosing a
monster wants and is not which book it came from.

## Editing a character

The reason this is a campaign and not a tracker. A level-up between sessions, a number
Pathbuilder got wrong, a homebrew ancestry: none of those can wait for a re-export at the table.

Two decisions inside it.

**The sheet now carries its own build.** `CharacterSheetView` gained a `CharacterBuildEdit`,
which is exactly the shape an edit sends back. Without it the editor had nothing to open with: a
sheet states Fortitude +11 and an edit states Trained, and one cannot be worked back from the
other without knowing the level and the attribute it came from. Sending the build with the sheet
is cheaper than guessing and it makes the round trip symmetric.

**The editor works in attribute modifiers, not scores.** A player reading their own sheet sees
Dexterity +3. Making them type 16 to mean the same thing is a conversion the app can do.

**Every stepper in the editor got a visible caption.** `NumberStepper` carries its label as an
`aria-label` and draws nothing, which is right beside a hit point readout that names itself and
wrong in a grid of twelve identical minus-number-plus controls.

## What the widths look like

Checked at 390, 768, 1280 and 1920, light and dark. The fight panel spends 92 to 96 per cent of
the width at every one of them; the phone column is the phone's alone.

Two things were wrong at 390 and are fixed. The monster row laid its typed field, two buttons and
a switch beside the name and they overlapped it, so the row is a two-column grid now and the DM
controls take the second column on a row of their own. And the hit point field's label reads
"Hit points to apply to Ogre Warrior", which wrapped over three lines and pushed the buttons off
the edge; it is visually hidden now and still read aloud, because a screen reader otherwise hears
"Hit points" once per card and cannot tell them apart. That rule lives in `app.css` rather than
the screen's own sheet, because a scoped stylesheet cannot reach inside a component it only
passed a class to.

## The verifier

`tools/ui-check/verify-campaign.mjs` drives both roles in one browser, which is what a table is.
It starts a campaign, imports the bard, switches to Fight, adds him and an Ogre Warrior, rolls
initiative, advances a turn, undoes it, edits the armour bonus and watches armour class follow;
then a second tab joins with the code alone and is checked for the absence of the mode switch,
the encounter controls, the monster's hit points and the unrevealed monster itself.
