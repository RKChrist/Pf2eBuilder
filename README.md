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

## Current state

Design only. No application code exists yet. The phase plan in the design document
says what comes first and what green looks like for each phase.
