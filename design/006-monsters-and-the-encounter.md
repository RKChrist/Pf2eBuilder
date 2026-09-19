# 006. Monsters, and the encounter tracker

Decided 19 September 2026. This is the plan for building what the design PDF describes, and it
reverses two earlier decisions. Both reversals are the owner's call and both have consequences
worth stating before any code.

## What changed

Open decision 1 scoped this to a character builder with a party tracker, explicitly excluding
the encounter side. The owner has now asked for the tracker the PDF defines, monsters included.
`Sources/pf2e-table-companion/table-companion-design.txt` is that document, extracted from the
PDF and archived beside the Claude Doc version already there.

## The consequence nobody should skip

`design/001` chose standalone WebAssembly, and the deciding argument was that the archived
brief's objection to it protects monster hit points from players, and this product had no
monsters. That premise is now false.

The PDF is unambiguous about what it requires:

> Monster details are not merely hidden in the UI; in the shared version they are stored where
> a player's client cannot read them at all.

WebAssembly still holds, but only because the rule is enforced at the API rather than in the
client. The API computes a per-viewer projection and a player's payload never contains an
unrevealed monster at all, nor any monster's hit points or stat line. Nothing is filtered in
the browser, because anything the browser receives, the browser has. That was already written
down as Open decision 2 and is now load-bearing rather than hypothetical.

**A test asserts the absence, not the presence.** The player projection of an encounter
containing a 50 hit point ogre must not contain the number 50 anywhere in its serialised form.
Asserting that the UI hides it would pass while the data sat in the payload.

## Roles without building accounts

A projection needs to know who is asking, and there is still no authentication. Full accounts
are phase 3 and are not a prerequisite here. The PDF offers the smaller step itself: the DM
view is bound to a secret, and a single-device version splits by honour.

So a table gains a second secret. The player code is what everyone types. The DM key is a
separate, longer secret that the table's creator keeps, and it is what promotes a connection to
the DM role. Anyone without it is a player. This is proportionate to a table of friends, it is
honest about being a shared secret rather than an identity, and phase 3 replaces it without
changing the projection.

## Undo comes back, and I argued against it before

Open decision 3 dropped undo and the command log, and the owner's "the log is not needed"
supported that. The PDF lists Undo as a DM control in the encounter screen, and it is right to:
damage applied to the wrong combatant mid-fight is the single most common mistake at a table.

What comes back is the smaller half. A bounded in-memory stack of inverses for the DM, capped,
per encounter, lost on restart. Not a durable command log, which is what the earlier decision
actually rejected.

## Phases, in dependency order

| # | Phase | Why it is here |
| --- | --- | --- |
| A | Bestiary data | Everything else needs monsters that exist. 4,791 creature records carry hp, ac, the three saves, perception, speed, traits and attribute modifiers as structured fields, so a DM searches rather than types |
| B | Roles and the projection boundary | Must land before any monster reaches a client, or the first version leaks and the fix is a retrofit |
| C | The encounter | Combatants, initiative with the tie rule, round and turn marker, and the turn clock that expires effects |
| D | Undo | Needs C to have something to reverse |
| E | Multi-target effects | "all PCs" and "all monsters" need both C and the effect model that already exists |
| F | The screens | DM and player views, and the encounter UI the PDF sketches |

Two rules from the PDF that are easy to lose and are not optional. Ties in initiative put
adversaries before PCs. And players apply their own buffs, with the DM watching it land, which
is the design choice that removes "who is tracking the +1" from the table.

## Also asked for, and small

Trait chips become tappable and filter the catalogue. The query already supports a trait filter
and the chips are already rendered; wiring one to the other is the whole change. It turns
reading a feat into finding every feat like it.

## Not in this plan

Exploration and Downtime modes. The PDF defines both and neither is needed to run a fight.
They come after the encounter works.

## Three corrections from the owner, all of which change the screen

**Hit points are typed, not tapped.** The stepper sends one point per tap, so losing 100 hit
points is 100 taps and 100 round trips. That is unusable in the moment it exists for. The
primary control becomes a numeric field with Damage and Heal, one round trip per application
whatever the amount. The steppers stay for the nudge case, and the field carries
`inputmode="numeric"` so a phone offers the keypad.

**Party-wide buffs read as party-wide.** Rallying Anthem is one effect on five characters, and
five identical chips on five cards says something different from what happened. An effect
applied to many targets is shown once, above the party, with the targets it reached; an effect
on one character stays on that character. The model already needs multi-target application
because the PDF asks for "all PCs" and "all monsters", so this is a display consequence of a
decision already made rather than new state.

**Add a player or add a monster, from the tracker.** Putting a combatant into the fight is the
encounter screen's own job, not something you leave to do elsewhere. Adding a monster searches
the 3,832 seeded creatures and drops in real numbers. Adding a player pulls from the table's
roster. Both are DM actions.
