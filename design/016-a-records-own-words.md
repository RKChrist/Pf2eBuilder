# 016. A record's own words

Decided 20 September 2026, at the owner's request. It narrows a guarantee that
`tools/rules-import/FieldPolicy.cs` and `NOTICE.md` make, and does not remove it.

## What was decided

The panel shows a record's description. The owner's example was Courageous Anthem, whose panel
listed an area, a duration and a link out, and never said what the spell does.

The description is fetched for one record, when somebody opens it, from the Archives of Nethys
document that the seed's id already names (`spell-1763` is their id as well as ours). The API
keeps what it fetched as a file under `App_Data/rule-text`, which git ignores, so each record is
asked for once per machine.

What stays true:

- The importer still withholds every prose field at download time, and the tracked snapshot in
  `Sources/` still holds none. The repository contains no rule text.
- Only an id the seed knows is ever sent to their index. The endpoint cannot be used to make the
  server fetch anything else.
- The stat header is dropped from what they send, because the seed already holds it as mechanics.
  Their layout tags come out. A link keeps its words and loses its address.
- The client encodes the text before adding any markup of its own, and draws a fixed subset of
  Markdown: paragraphs, bold, italics, lists, rules and plain tables.
- A description that cannot be had is not an error. The mechanics and the link out still work.

`RuleText:Enabled=false` turns it off, and deleting the cache folder removes every fetched word.

## The alternatives

**Pull the prose in bulk and seed it.** Fast and offline, but it puts Paizo's text for 25,595
records into a tracked snapshot, in a repository that may be public, while `NOTICE.md` still says
the ORC notices are a draft. That is redistribution. Fetching one record for one reader is closer
to what a browser does.

**A local-only bulk pull into the ignored `out/` folder.** Offline without the tracked copy. It is
the natural next step if the table turns out to play without a network, and the cache this
decision adds is already the shape it would fill.

**Leave the link out as the only way to read it.** That is what the owner asked to stop.

## Why this one

It answers the request with the smallest change to the project's licensing position: nothing new
is stored in git, nothing is downloaded that nobody asked to read, and the source site is named
and linked on every panel that shows its words.

## What is still the owner's to settle

`NOTICE.md` is a draft. Showing descriptions makes finishing it more pressing, because the app now
displays Paizo's expression and not only mechanics. While the project stays free and
non-commercial this sits under the same Community Use cover the names already rely on.

## What would make us revisit it

Playing somewhere with no network. Archives of Nethys asking tools not to read their index this
way. The project stopping being free.
