# Design tokens

`npm run build` regenerates `dist/tokens.css` from `tokens/` and then proves the result is
accessible. It fails if any pair drops below contrast, so a palette change cannot land
quietly. `dist/tokens.css` is checked in, so a machine with only the .NET SDK can build and
run the app without Node.

## The idea

The thing this app replaces is a pencil-and-paper grid read on a phone, at a table, in bad
light, under time pressure, where misreading a modifier costs something. So it is designed as
an instrument, not a spellbook. Parchment, gold and fantasy serifs are the obvious answer and
they are also bad for dense numerals.

On content, saturation is reserved for game meaning. The frame around it is lit and coloured,
because an instrument that looks switched off does not get picked up. `design/013` is where that
was decided.

- **Ground.** Daylight in light, which is the default. Dusk in dark: a slate lifted well clear of
  black. Never cream, never a warm grey.
- **Accent.** Lagoon, a teal with the grey taken out, chosen by elimination. Red and green belong
  to degrees of success, and amber, blue and violet belong to Paizo's rarity colours, which
  players already read fluently. Teal is what was left.
- **Chrome.** The navigation rail and bottom bar are the accent used as a ground. It is the one
  place that happens.
- **Lantern.** Yellow marks place: where you are, which mode the table is in, whose turn it is.
  A fill under dark type, never a text colour, and yellow rather than amber so it is not read as
  uncommon rarity.
- **Outcome ramp.** Four degrees of success, where a critical result always sits further from
  the page than a plain one. Darker in light, brighter in dark. `contrast-check.mjs` asserts
  that ordering, because if criticals stop outranking plain results the four degrees collapse
  into two.
- **Rank.** Proficiency is drawn as five filled steps, not five colours, because rank is a
  quantity and the structure should say so.
- **Radius.** Not one value on everything. Data cells are square, because a grid has corners.
  Chips are lightly rounded, controls a step softer, panels and bottom sheets softer again.
- **Separation is a border.** A card also casts a small shadow, which says it is a sheet on the
  desk. The larger elevations are for things that genuinely float.

Colour never carries meaning alone. Every outcome and rarity pairs with a glyph.

## Phones only

Players are on phones. There is no desktop target, so tap sizing and thumb reach are the
layout's spine rather than a courtesy.

- `--tap-min: 44px` is a floor nothing interactive goes under. `--tap-default` is 48px and
  `--tap-primary` is 56px for the actions taken over and over mid-session.
- `--tap-separation` keeps adjacent targets far enough apart that a mis-tap needs real travel.
- `--layout-bottom-bar` puts navigation in the thumb arc. `--layout-thumb-reach` marks the
  part of the screen a thumb covers one-handed, and primary actions live inside it.
- `--font-size-body` is 17px, above the threshold where iOS zooms a focused input.
- Design width is 390px and the layout holds at 320px.

## Files

`tokens/primitive/` is the raw palette and scale. `tokens/semantic/` maps those to roles, once
per theme. Components reference semantic tokens only. Reaching past them to a primitive means a
role is missing, so add the role.
