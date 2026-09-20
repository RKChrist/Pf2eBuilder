# 017. A camping session

Decided 20 September 2026, at the owner's request: a Camp tab that shows camping in its phases,
with a diagram, cooking and homebrew, that follows the rules and is easy to understand.

## What was decided

**The phases are the rules' five steps, in the rules' order.** Kingmaker Companion Guide,
chapter 2: prepare a campsite, Camping activities, eating, resting, daily preparations. A first
draft had five phases of its own invention (make camp, around the fire, the meal, the watch, the
night), which read well and were not the rules. The owner said it has to follow the rules, so
the steps were re-read at the source before the model was written.

**The order is drawn as a line.** Five stops, a tick on the ones behind you, a lantern on the one
the table is at. Looking at a step is each screen's own business, so a player can read ahead.
Moving the table along the line is the DM's, and every screen follows. On a phone the line stands
up and the stops run down it.

**The app keeps the rules that five people talking lose track of, and rolls nothing.**

- How Prepare Campsite went decides the evening. A critical success makes tonight's Encounter DC
  two higher. A failure puts a -2 penalty on Camping checks. A critical failure allows no Camping
  activities at all. The page draws the four degrees as four doors and says what each one opens.
- A Camping activity takes two hours, and the campaign clock moves when one is taken.
- Nobody takes more than four in a day. Each character's four are drawn as four pips.
- Once anybody succeeds at an activity it is closed until the next camp. Cook Special Meal is
  excepted, because the rules except it.
- The table rolls the check and taps the degree of success. The app records it.

**Cooking is a recipe book the table writes.** The ruleset holds Cook Basic Meal, Cook Special
Meal, Discover Special Meal and Hunt and Gather. It holds none of the special meals themselves,
which Archives of Nethys does not index as records. So a recipe is an entry in the campaign's
camp book: a name, a cooking DC, and what it does in the table's own words. Each character chooses
their own meal at step 3, as the rules have it, from rations, a basic meal, or a recipe in the
book. Basic and special ingredients are two counters.

**Homebrew is the same book.** An activity the table wrote appears in the step 2 list beside the
ruleset's twenty-three and follows the same rules.

**Tables are drawn.** The Watches and Rest table is eight rows that are one formula: eight hours
shared among everyone but the one on watch. For one party it is one bar, cut into as many watches
as there are people, with a mark every four hours where the GM checks for an encounter. The
Camping Zones table is a picker that fills in the two DCs, which are then shown as two large
numbers and not as a row in a grid.

**Camp is a tab.** It sat as a small link on the far side of the header and was missed. It is in
the strip beside Explore, Fight and Downtime now, after a rule, because it is not a mode: the DM
opening it moves nobody.

**One value on the campaign.** `CampSite` is an immutable record stored as one column, so undo
puts a whole evening back by keeping a reference. The undo snapshot gained the campaign clock at
the same time, which it never carried.

**A night's rest follows the watches table.** It was a flat eight hours. It is now the table's
total for the party's size, which is eight hours for one person and sixteen for two.

## The alternatives

**Phases of our own.** Easier to name, and wrong.

**Seed the special meals.** They are not in the data this project pulls, and typing Paizo's
recipes into the repository is the redistribution `design/016` declined.

**Roll the checks in the app.** The app has the modifiers. But a table rolls dice, a Camping check
has circumstances the app cannot see, and recording a result is one tap.

**Enforce "no two characters take the same activity at once".** It is a rule about simultaneity,
and the app has no notion of who is acting at the same moment. It is stated on the page and left
to the table.

## What would make us revisit it

Tables wanting the app to roll. Special ingredients needing to be spent automatically when a meal
is cooked. A campaign that is not in the Stolen Lands wanting a zone list of its own.
