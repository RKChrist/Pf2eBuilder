# 003. Wide screens

Decided 19 September 2026. This amends `design/001`, which said there was no desktop target.

## What changed

The owner said players do not use computers, and the layout was built phone-only on that basis.
They have since asked that desktop and tablet look good too. Both are true at once: a player at
a table is on a phone, and a GM planning a session, or the owner reviewing the work, is on a
laptop. Phone-first stays the design priority. Looking broken on a wide screen stops being
acceptable.

## The failure it fixes

At 1440px the app is a 560px column centred in a sea of empty space, with a six-item bottom bar
stretched across the full width. Nothing is broken and nothing is nice. The bottom bar is the
worst of it: bottom navigation exists because a thumb reaches the bottom of a phone, and a mouse
pointer has no thumb arc. On a desktop that bar is the furthest thing from the cursor.

## The rule

Do not centre the phone. Spend the width on information rather than on padding.

| Width | Navigation | Categories | A record |
| --- | --- | --- | --- |
| Under 600 | Bottom bar, thumb arc | One column | Bottom sheet |
| 600 to 1023 | Bottom bar | Two columns | Bottom sheet |
| 1024 and up | Left rail, near the cursor | Three columns | Side panel beside the list, not over it |

Prose keeps a measure near 65 characters whatever the viewport, because a 1440px line of text is
unreadable however much room there is. Lists of short labels do not, which is why the category
grid gains columns while a record's description does not.

At 1024 and up the detail becomes a panel beside the list rather than a sheet over it. That is
the real desktop win: you keep your place in 6,390 feats while reading one of them, which is
exactly what a bottom sheet costs you on a phone and cannot avoid there.

## What does not change

Tap sizing. A 44px floor costs a mouse user nothing and a touch laptop is real. Hover states
stay fenced behind `@media (hover: hover)` so a tap never leaves one stuck. The tokens do not
gain a desktop set; the same scale is reused at more columns.
