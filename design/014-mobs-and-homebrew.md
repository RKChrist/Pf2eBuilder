# 014. Mobs, and monsters from no book

Decided 20 September 2026, at the owner's request. It amends `design/006` in one place and leaves
its security rule exactly as it was.

## What was decided

**A player sees "Mob 1", not nothing.** `design/006` dropped an unrevealed monster from a
player's payload entirely, on the reasoning that a blank row still says something is standing
there. The owner wants that said: the table needs to be able to say "I hit Mob 2". So an
unrevealed monster is now a row in a player's order with a number, its place, and whether it is
acting. It has no name, no record id, no traits, no level, no numbers and no conditions, because a
condition's name can say what a creature is. Revealing it turns that row into the monster, under
the same id, so the row does not jump.

006's rule still holds and is still where it was: the server's projection decides, nothing is
filtered in the browser, and the tests assert the absence in the serialised payload. The payload
for an unrevealed fifty hit point Doppelganger contains neither "50", nor its name, nor
`creature-126`.

**The number is stored.** `MonsterCombatant.MobNumber` is assigned when a monster joins: the
lowest number nothing in the fight holds. It is not a position in a list, so Mob 2 is still Mob 2
after Mob 1 dies. A freed number comes back into use only once nothing on the screen holds it,
which is the rule the creature names already follow. The DM's row shows the alias beside the real
name until the monster is revealed.

**A monster can be written by the DM.** `AddCombatant` names exactly one of three things: a
creature record, a character, or a `HomebrewMonster` with a name, hit points and the line a DM
reads off when somebody attacks it. A homebrew monster has no record id, so its row offers no rule
link. To a player it is a mob like any other.

**Several at once is one command.** `Count`, up to twenty. The handler's own comment explains why
it is not several requests: overlapping adds into a campaign with no encounter row race, and one
loses.

**The DM can change a monster in the fight.** `EditMonster` takes the whole line: name, level,
hit points, armour class, the three saves and perception. Hit points the monster currently has
move by as much as its maximum moved, so a fresh monster made elite stays fresh and a wounded one
is not healed by being made weak. The form offers the elite and weak adjustments as the rules
state them. The undo snapshot already carried a monster's name and line, so an edit is undone like
anything else.

**Monsters come first in the add panel.** The owner could not find the bestiary search, which sat
under the party roster. The panel now opens on it, with how many, the search, and "Make your own".

## The alternatives

**Keep players blind until a reveal.** That is 006, and it is what the owner asked to change.

**Number mobs by position.** No storage, and "Mob 2" becomes a different creature the moment
Mob 1 dies, mid-sentence, at a table that is talking.

**Let a player see a mob's conditions.** Useful, and "Undead-only condition on Mob 3" is the
monster's name with extra steps. Conditions arrive with the reveal, as before.

**A separate homebrew library.** Monsters the DM saves and reuses across fights. Worth having and
not needed for the request, which was to get one into this fight.

## What would make us revisit it

Players wanting to see that a mob is frightened or prone, which the table can usually see on the
map. A DM running the same homebrew monster every week and retyping it.
