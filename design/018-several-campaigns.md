# 018 — Several campaigns, in the browser

**Status**: accepted
**Follows 012, whose list of what comes next opens with the larger version of this.**

012 ended by naming a GM's list of their own campaigns as the first thing accounts make
possible, and by saying it changes what a campaign *is*. This is not that. This is the smaller
change underneath it: what a browser loses today, fixed where it is lost, with nothing on the
server touched.

## The defect, which cost somebody a table

A browser remembered exactly one campaign, under `pf2e.campaign`. Joining or creating another
overwrote it. There was no way to leave a campaign except clearing the browser's storage, which
is the same overwrite performed by hand.

Either of those alone is an inconvenience. Together they lose a GM their first table
permanently. The DM key is shown on no screen and exists nowhere else, so the browser that
created a campaign is the only thing in the world holding its key, and losing the key is losing
the campaign. No Add, no Roll, no Next turn. The monsters that GM had placed and not yet
revealed disappear from their own screen, because 006 puts an unrevealed monster outside the
projection of everybody who is not presenting the key. The table is still running. Nobody can
run it.

## What a browser holds now

A list, under `pf2e.campaigns`, newest first. Each entry is a code, the DM key this browser
holds for that campaign if it holds one, and when it was last opened.

It is capped at eight, because it is a convenience list and not a record of anything. Uncapped
it grows for as long as the browser lives, and nobody is scrolling to the campaign they played
in March. What is at stake if the cap is wrong is one extra row on the join form.

The head of the list is the campaign this browser was last in, so the plain reload answers
exactly as it did before: a phone that dropped its tab comes back into the fight, with the key,
on the screen it left. That is the one thing the single slot did well and the thing a change
here is likeliest to break, so it is the behaviour the list is shaped around rather than a
consequence of it.

## Why the list is in the browser and not on the server

This is the decision the rest of the record follows from.

Accounts owning campaigns is the next step, and it has the seam it needs because 012 built it.
It is also a different change. An owner, an invitation, a key recovered on a device that never
held it: each of those changes what a campaign is, which is why 012 listed them instead of
doing them.

What is broken today is narrower than any of that, and it is broken in one place. A browser
that already holds a DM key is the right thing to hold the list of the keys it holds, because
that is all the list is. Nothing is published and nothing is shared between devices, so there
is no question to answer about who may read it — which is precisely the question a list on the
server forces, and precisely the question 012 declined to open.

So the server does not change at all. No new endpoint, no schema change, no change to the
projection. The whole of this lives in the client and the rollback is one file.

## The old key is folded in, and that was not optional

Somebody has a campaign open right now, under `pf2e.campaign`, mid-session. A migration that
started every browser on an empty list would take that person's key out from under them, which
is this record's own defect committed deliberately.

So the first read of the list also reads the old key. If it holds a campaign, that campaign
becomes the head of the list, stamped as opened now, and the old key is deleted. It is folded
in even when the list already has campaigns in it, because the old key is the campaign the
person is looking at, and a second tab that happened to write the list first does not change
that. A test asserts exactly that ordering rather than only asserting that nothing was lost.

Reading the old key once and deleting it is what keeps the fold from running twice. A campaign
forgotten after the migration stays forgotten instead of reappearing on the next read.

## Leaving, and what it deliberately does not do

Leave sits on the shell beside the campaign code, not on one of the five mode pages. The code
is what says which table you are at, so the way out belongs next to it, and there is one of it
wherever you happen to be standing.

Leaving forgets nothing. The browser keeps the campaign and its key, and the button's own words
say so. Forgetting is a separate act on the list, and it is the only thing in this app that
drops a DM key. That sentence is on the screen under the list, in the words describing what the
button does, rather than in a confirmation dialog. A dialog is read the first time and dismissed
every time after, and it arrives too late to be an explanation; the sentence is there before
anybody reaches for the button.

Leaving also empties what was typed at that table and leaves the hub group. One connection
carries several tables over a session now, so a change arriving for the campaign somebody has
just left would otherwise put that campaign back on their screen, which is the leave undone by
the network. A half-typed damage number still sitting in a field when the next table opens is
the smaller version of the same mistake.

## Two smaller decisions worth recording

**A key-less open keeps the key the browser already holds.** Typing your own table's code back
into the join box arrives with no key attached, because the key is on no screen and never was.
Reading that as "this browser is a player now" would throw away the only copy of it. So an open
of a campaign the browser already holds a key for keeps that key, and the only way to drop one
stays the one that says it drops one.

**The recall is asked once per page load, not once per campaign screen.** The five mode pages
all build the same shell. Asking again on the next screen would walk somebody who had just left
a campaign straight back into it on their next tap of the bottom bar, which is the whole of
leaving undone by navigation.

## What would make us revisit this

A GM who wants the same campaigns on a phone and on a laptop gets nothing from this, and
neither does one who loses the browser or reinstalls it. Both need the account-owned campaign,
which is the first item on 012's list and a change to that record rather than to this one.

A browser that habitually clears site data makes the list a lie rather than a gap. It will
appear to remember and will not, and nothing here can detect the difference.

And a shared or public game makes the DM key the wrong shape to be holding at all, at which
point a list of keys is the wrong thing to be keeping carefully. That is 012's question, and it
stays 012's.
