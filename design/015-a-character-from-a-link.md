# 015. A character from a link

Decided 20 September 2026, at the owner's request.

## What was decided

Pasting a Wanderer's Guide link imports the character it points at. The owner's example was
`https://wanderersguide.app/stat-block/character/70618`. A stat-block link, a sheet link, a
builder link and the bare number all work, in the same box that takes a pasted export and in a
field of its own on the party page.

**Only the number leaves the link.** `WanderersGuideCharacterId` accepts `wanderersguide.app` and
`www.wanderersguide.app` and nothing else, with no user-info trick and no other scheme. The server
then asks one fixed host, `api.wanderersguide.app`, about that integer. It never fetches the
address somebody pasted, so a link cannot be used to make this server call somewhere.

**The character has to be public.** Their `find-character` call answers without credentials for a
character its owner shares, and refuses otherwise. A refusal, a missing character and an
unreachable site each get their own sentence, and each says what to do next.

**A link brings totals, so totals are what the sheet keeps.** What their API returns is the raw
character. The compiled sheet an export carries under `content` only exists once their rules
engine has run in a browser. What the raw character does carry is
`meta_data.calculated_stats`: maximum hit points, armour class, and a finished total with a rank
for every save, skill, perception and DC. There are no attributes in it.

Rebuilding a save from a guessed Constitution would print a number the player's own sheet
disagrees with. So `Character` gained `Stated`, a `StatedTotals`, and `Sheet.Compute` takes a
stated total whole as the base and stacks only the session's modifiers on it. That is what a
weapon's bonus and the stated hit points already did. Frightened 2 on a stated +21 Fortitude is
+19. Their DC totals leave the ten out and ours carry it.

**What a link does not bring:** attacks, feats, spells and attributes. The export file is how
those arrive, and the panel says so. Refreshing from a link a character that first came from a
file keeps the file's attacks, feats and spells instead of emptying them.

## The alternatives

**Back-derive the attributes from the totals.** A save gives an attribute only if no item bonus
is folded into it, and Dexterity then has to agree with Reflex, armour class and three skills at
once. It does not, on the owner's own character.

**Run their rules engine on the server.** It is a browser application under GPL-3.0. Too much
machinery and the wrong licence for a convenience.

**Fetch the pasted URL and read the page.** The page is an application shell with no character
in it, and fetching a user-supplied address is how a server gets pointed at its own network.

## Why this one

One request, to one host, about one integer, for a character whose owner made it public and
whose player asked for it. `docs/research/rules-data-sources.md` records that Wanderer's Guide
states no licence for its content. This reads one character, not their content, and never in bulk.

## What would make us revisit it

Wanderer's Guide putting the compiled sheet behind the same public call, which would let a link
bring everything a file does. Their API asking for a key. A rank letter that disagrees with the
total beside it turning out to matter: their cache marks one of the owner's skills untrained
beside a master's total, and the total is what this app shows.
