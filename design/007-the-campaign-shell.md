# 007. The campaign is the app

Decided 19 September 2026. This replaces "party tracker" as a feature bolted beside a rules
browser with the structure the design document actually describes.

## The noun is a campaign

A table was a code you typed to see some characters. A **campaign** is the thing the group
plays: a persistent roster, a mode it is currently in, characters you can edit, and a DM who
drives it. Everything else hangs off that.

This is a rename with consequences, so it happens in one wave rather than as a synonym that
lingers. `Table` becomes `Campaign` in the domain, the API, the database and the client, and
the old noun is deleted rather than aliased.

## Mode is campaign state, and the DM owns it

From the design document:

> The app has one persistent party roster and three modes that follow the game's own structure;
> a Reference tab sits beside them. Switching mode is a DM action and every screen follows.

Exploration is the resting state. Encounter is entered by rolling initiative and returns to
Exploration when the DM ends it. Downtime is entered deliberately and tracked in days.

Mode lives on the campaign, not in each client's local state. The DM switches it and every
phone follows, because a table where half the group is on a different screen from the other
half is worse than no app.

## Navigation becomes the document's shell

The document sketches it directly:

```
[ Encounter | Exploration | Downtime | Reference ]  Round 3 [DM]
```

So the six rules-browsing groups stop being the top level. **The rules catalogue becomes the
Reference tab of the campaign shell.** That is the reconciliation of "one app": browsing rules
and running a session were never two products, and making the catalogue a mode rather than a
peer says so.

The round counter and the DM badge sit in the shell because both answer a question the DM asks
constantly, and neither is worth a tap.

## Characters are edited, not only imported

Import is how a character arrives. It is not how a character stays correct. A level-up between
sessions, a number Pathbuilder got wrong, a homebrew ancestry: all of these need an edit. The
fields that feed the calculator are editable, the build layer is what changes, and the session
layer survives it exactly as it survives a re-import.

## Why this makes the DM faster

Every choice above removes a tap from the thing a DM does most. Mode is one tap and moves
everyone. The round is visible rather than remembered. A character's sheet opens from the
tracker rather than from a different section. Reference is a sibling of play, not somewhere you
leave the session to reach.
