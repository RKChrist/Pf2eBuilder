# Pf2eBuilder

A Pathfinder 2e character builder. Blazor client, ASP.NET Core API, SignalR for live
updates, a pure rules engine that derives every number on a sheet.

**The design document is the entry point.** Read it before touching anything here:
<https://claude.ai/code/artifact/2a04bda1-2d30-45ad-80e5-37e55f7a8d51>

It holds the stack decisions, the folder rules, the domain model, the configuration
contract, the Pathbuilder import contract, the phase plan and the open decisions.

## Layout

| Folder | Holds |
| --- | --- |
| `design/` | Numbered decision records, one per decision not covered by the design document |
| `docs/` | How to run it, how to add a vertical slice, how to add a rule |
| `Sources/` | Archived upstream material, never edited |
| `src/` | The application |
| `tests/` | `Pf2e.Behaviour.Tests` (one scenario per rule) and `Pf2e.Unit.Tests` |
| `tools/` | Style Dictionary token build, rules-data import scripts |

## Running it

Double-click `run.cmd`, or read `docs/running-locally.md`.

Running `src/Pf2e.Client` alone gives you a client that cannot reach anything. The
client is a separate deployable that talks to the API over HTTP, which is the
architecture rather than a bug, so one project is never enough.

## Current state

A rules browser over 24,940 seeded Archives of Nethys records, a 22-component kit
with its own gallery, and a rules engine whose every computed number can explain
itself. The party tracker is being built now; until it lands there are no
characters and nothing to update.
