# 001. Blazor render mode

Decided 19 September 2026.

## Decision

Blazor WebAssembly, standalone, installable as a PWA. It is a separate deployable
that talks to `Pf2e.Api` over HTTP, which is the shape the owner asked for.

## The alternatives

| Option | What it costs | Why not |
| --- | --- | --- |
| **WebAssembly standalone** (chosen) | A runtime download on first visit, and no prerender | Accepted. See below |
| Interactive Server | Every tap is a server round trip, and the circuit dies when the phone locks | Players are on phones at a table. A locked phone is the normal case, not the edge case |
| Interactive Auto | Both models, both failure modes, and a render-mode rule per component | Complexity with no payoff here |

## Why the archived brief reached the opposite answer

`Sources/pf2e-table-companion/engineering-notes.md` chose Interactive Server, and
its reasoning was sound for the product it described. "In WebAssembly the whole
component and its data live in the player's browser, so anything the client
renders, the client has." That protects monster hit points from players.

This product has no monsters. The invariant that drove their decision does not
exist here, and the costs they listed do. Their own notes say "A dropped
connection has real consequences" and "Graceful auto-pause is explicitly not a
supported scenario on mobile when the app is backgrounded". For players who are
on phones at a table and nothing else, that is the deciding argument.

## What this does not buy

It does not buy offline. The rules database is 24,940 records and stays on the
server, so the app needs the API for content whatever the render mode.

What it does buy is that a locked phone does not destroy the session, and that a
tap on a filter is instant rather than a round trip. Those are the two things a
player at a table actually feels.

## Revisit if

The encounter tracker is ever built with GM-only data. At that point the
monster-privacy invariant comes back, and the answer is a server-rendered
encounter surface beside this client rather than moving this one.
