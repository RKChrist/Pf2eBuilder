# Superseded phase 1 implementation

These files came from an orphaned delegate of a phase 1 attempt that died on a
rate limit. The surviving phase 1 tool was built from them, so their good parts
live on in `tools/rules-import/`. Four defects were fixed on the way: a wire
exclude list of 13 fields where the census found 59, a deny-list seed projection
that would have shipped anything the list missed, a User-Agent carrying personal
contact details, and a dangling `remaster_id` dropping a record instead of
keeping it.

The `aon-snapshot/` directory that sat here was deleted on 18 Sep 2026. Its
manifest recorded only 13 excluded fields, so it held rule prose we had decided
not to store. Keeping it would have defeated the exclusion.

Nothing here is referenced by the build. Delete the whole folder whenever you
like.
