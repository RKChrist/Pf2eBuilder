# 011 — A page per mode

**Status**: accepted
**Amends 009.**

The campaign screen was one page that swapped its middle depending on the mode. The owner asked
for the combat tracker to be its own page, and for the rest to be pages or at least subpages.
They were right, and the reasons are worth writing down because the first version had a good
argument behind it.

## What was wrong with one page

The mode decided the screen, which reads well in a design document and badly at a table:

- **A fight had no address.** Nobody could send "we're here" to somebody who missed a turn, and
  the back button did nothing.
- **Looking somewhere cost everybody something.** The DM wanting to check tomorrow's downtime had
  to move the whole table into Downtime to see it.
- **One file held 1,431 lines** and four unrelated panels that never rendered together.

## What it is now

Five routes.

| Route | What is on it |
|---|---|
| `/campaign` | The party: cards, effects, editing, what everybody is carrying |
| `/campaign/encounter` | Initiative, monsters, turns, undo |
| `/campaign/exploration` | What each character is doing, and what they can attempt |
| `/campaign/camp` | The ten-minute activities, the clock, a night's rest |
| `/campaign/downtime` | The day and what each character spends it on |

`Components/CampaignPage.razor` is the shell all five render inside: the heading, the code, the
mode strip, the offline and error banners, and the join form. A deep link to
`/campaign/encounter` with no campaign open lands on the join form rather than on an empty fight,
because the shell decides that once and no page repeats it.

## Where you are, and where the table is

These are two different things and the screen says both.

**The strip is navigation for everybody.** A player can open the camp page while the party is
still walking. Their tap moves them and nothing else.

**A dot marks the mode the table is in.** For the DM the same tap also sets it, so saying "we're
in a fight" and going to look at it is one action rather than two.

**Nothing drags anybody.** An earlier draft navigated every screen when the mode changed, which
is defensible at a table and unusable for a DM: they would be yanked back the instant they looked
at Downtime while the party was fighting. The dot tells a player where the table went; the tap is
theirs.

`Party` and `Camp` sit beside the round and the DM badge rather than in the strip, because
neither is a mode. The party is where you always come back to and camp is something you do in
any of the three.

## What the split cost

Two things broke on the way and both were caught by the browser check rather than by the compiler.

The bottom bar reads the browser's active group, and the campaign page used to set it on arrival.
The four new pages did not, so the bar pointed at Build while the reader was in a fight. Arrival
work belongs to the shell, and that is where it is: the group, the conditions and the exploration
activities, asked for once by whichever of the five pages you came in on.

The mode links carry the dot inside their own text, so the check's exact-text match stopped
finding them. They carry `data-mode` now and the check matches on that, which is what it meant
all along.
