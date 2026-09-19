# 004. Full width

Decided 19 September 2026. This amends `design/003`, which it does not replace.

## What was decided

At 600px and up, content stops at the viewport, not at the 560px phone column. The width goes on
information:

| Width | Board | A category | Search | A record |
| --- | --- | --- | --- | --- |
| Under 600 | One column | As 003: filters folded under a toggle | As 003 | Bottom sheet |
| 600 to 1023 | Two columns | Full-width rows; name, level and glance on one line | Full-width rows | Bottom sheet |
| 1024 to 1279 | Three columns | Filters stand as a sticky column beside the list | Kinds of record stand as a sticky column | Docked on the right; the list reflows into what is left |
| 1280 to 1679 | Four columns | As above; traits join the row's line when the list is 1040px or wider | As above | As above |
| 1680 and up | Five columns | As above | As above | As above |

Prose keeps its measure. A blurb, a gloss and a sheet's body stay within `--layout-content-max`,
the token 003 already named as the 65-character measure, whatever column they sit in.

A row decides its own layout by the width of its list, with a container query, not by the
viewport. A list squeezed by a docked record therefore reads like a narrower list, rather than
overflowing or being pushed under the panel.

With no record open, the list takes the full width. There is no resting prompt in the right pane.

Phones under 600px render as they did before this change.

## The alternatives

**Keep 003's capped column beside the dock.** 003 capped the list at 560px at laptop width so that
opening a record never moved it. In practice a laptop showed a 560px list, a 560px sheet, and empty
space between them, and with no record open, half the screen was empty. The owner asked for the
width to be used.

**A resting pane.** Reserve the right-hand pane even with nothing open, and show a prompt in our own
words there. This keeps the list's width constant. But it spends 40% of the screen telling the
reader something they already know, which is that clicking a row opens it. A list 40% wider shows
more of the glance line and the traits, and that is information.

**Multiple columns of rows.** A category could flow as two or three columns of phone-style cards.
Records are read down a sorted list, alphabetical or by level, and columns break that reading
order into snakes. One wide row per record keeps the order and puts the facts on one line.

**Viewport media queries for the row.** These are simpler, but they measure the wrong thing. The
same row sits in a 1,100px list or, with a record docked, a 600px list, at the same viewport
width. A container query answers the question the row actually has.

## Why this one

It uses the width for information. The board gains columns of short labels, a row gains a line, and
the filters stop hiding behind a toggle on a screen that has room for them. The one thing 003
protected, that the list does not jump when a record opens, gives way on purpose: the list keeps
its left edge and its scroll position, and only its right edge moves in.

## What would make us revisit it

- Readers lose their place when a record docks and the list reflows. That would argue for 003's
  fixed column again, or for a resting pane.
- A category whose rows need more than one line of facts at any width. The row's container
  breakpoints are 720px and 1040px, and they assume a glance line that fits on one line when
  truncated.
- Tablet users who want the filter column. The column starts at 1024px because below that it would
  take a third of the list's width.
