# 013. Daylight

Decided 20 September 2026. This supersedes the colour and radius sections of
`tools/tokens/README.md` as they stood, and amends `design/003` and `design/004` on where a sheet
opens.

## What was decided

**Light is the default theme.** The app no longer follows the device's colour scheme. A switch in
the header chooses dark, and the choice is kept in the browser. `index.html` sets `data-theme`
before the stylesheets load, so the first paint is already right.

**The dark theme is dusk, not ink.** The page moved from `#0E151C` to a slate, `#1E2536`, with
cards at `#29324A`. Text roles were re-picked against the lifted surfaces, and the contrast check
still gates every pair.

**The accent has the grey taken out of it.** Same hue, chosen for the same reason: red and green
belong to the outcome ramp, and amber, blue and violet belong to rarity. `teal.600` became
`lagoon.600`.

**The chrome is coloured.** The navigation rail and the bottom bar are the accent used as a ground
(`--chrome-*`). On a laptop the rail is 88px (`--layout-rail`), wide enough for a label under an
icon.

**Lantern yellow marks place.** Where you are in the rail, which mode the table is in, whose turn
it is, and the DM badge. It is always a fill under dark type, never a text colour, and it is yellow
rather than amber so it cannot be read as uncommon rarity.

**Cards cast a shadow.** Three elevation tokens: `card`, `hover` and `float`. Borders still do the
separating. The shadow says which surfaces are sheets on the desk.

**Everything a pointer can press answers it.** Buttons had no hover state at all. Hover grounds are
a tint of the accent (`--accent-tint`), not the sunken grey, so "you can press this" and "this is
disabled" stop sharing a colour.

**Radii grew one step.** Chips 3px to 6px, a new 10px control radius for buttons and fields, cards
10px to 14px, sheets 18px to 22px. Data cells stay square.

**Every sheet docks on the right at 1024px and up.** Docking was opt-in, and only the rule and trait
sheets opted in. The breakdown, effect, character editor and add-combatant sheets opened as
full-width bottom sheets on a laptop while the page still reserved the dock's width for a panel
that never arrived.

**A rule's name is a link wherever it appears.** One component, `RuleLink`, takes a reference that
is either an id or a category and a name. A name resolves through the existing exact-name search
on click and falls back to the search page, so a click is never dead.

## The alternatives

**Keep following the device.** A phone in a dim room is usually in dark mode, which put the app
into the theme its owner described as dark and gloomy without anyone choosing it. Following the
device stays one line away: remove the default in `index.html`.

**Parchment and gold.** The tokens README already argued against it, and that argument stands:
warm grounds and serif numerals are bad for dense figures. Brighter did not have to mean warmer.

**A colour per rule group.** Six hues for six groups would collide with rarity and the outcome
ramp, which the palette exists to protect.

## Why this one

The instrument idea survives: saturation on content still means game state. What changed is the
frame around it. A coloured rail, a lit page and lifted cards make the app read as switched on, and
none of them spend a colour that the rules need.

## What would make us revisit it

Players at a real table asking for dark by default. Lantern yellow being misread as uncommon
rarity. The 88px rail costing too much width on a 1024px laptop with a record docked.
