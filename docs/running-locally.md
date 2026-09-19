# Running it locally

## The short version

Double-click `run.cmd` at the repository root, or run it from a terminal. It generates the
seed data if it is missing, starts both processes, waits for the API to answer, and opens the
browser.

## Why one project is never enough

Running `src/Pf2e.Client` on its own gives you a client that cannot reach anything. That is
the architecture rather than a bug. The client is a standalone WebAssembly app and a separate
deployable, and it talks to `src/Pf2e.Api` over HTTP. The reasoning is in
`design/001-blazor-render-mode.md`.

In Visual Studio, right-click the solution, choose Configure Startup Projects, pick Multiple
startup projects, and set both `Pf2e.Api` and `Pf2e.Client` to Start. Then F5 does the right
thing.

## Doing it by hand

    dotnet run --project tools/rules-import -- transform
    dotnet run --project src/Pf2e.Api --urls http://localhost:5092
    dotnet run --project src/Pf2e.Client --launch-profile http

The first line is the one people forget. The rules database is seeded from
`tools/rules-import/out/seed/`, which is generated and deliberately not in git, because it is
17 MB of pretty-printed JSON that a diff would drown in. It is rebuilt in seconds from the
snapshot that *is* tracked, in `Sources/aon-snapshot/`.

The API refuses to start with seeding enabled and no seed files, and says so with the command
to run. That refusal is deliberate: an empty rules database looks healthy and builds silently
wrong characters.

## Ports

| What | Where | Why it is fixed |
| --- | --- | --- |
| API | `http://localhost:5092` | The client's `wwwroot/appsettings.json` names it |
| Client | `http://localhost:5173` | The API's `Cors:AllowedOrigins` allows it |

Change one and you must change the other, or the browser blocks every request and the client
shows its failure state with nothing useful behind it.

## Checking it actually works

    curl http://localhost:5092/health

Returns `{"database":true,"rules":24940}` when the database is up and seeded. A rules count of
zero means seeding was skipped or the seed directory was empty.

## Useful commands

    dotnet test                                        every suite
    bash tools/rules-import/verify.sh                  the ingest is honest
    node tools/ui-check/measure.mjs <url> <width>      the layout is honest
    node tools/ui-check/verify-sliders.mjs <gallery>   the sliders behave

The layout checker needs a Chrome started with `--remote-debugging-port=9222`. Read
`tools/ui-check/README.md` before trusting a screenshot over it; two bugs were reported here
from cropped screenshots that measurement disproved.

## Regenerating the rules data

`Sources/aon-snapshot/` is tracked, so you do not need the network:

    dotnet run --project tools/rules-import -- transform

Only re-pull when Archives of Nethys publishes a new index, and be a polite client when you do:

    dotnet run --project tools/rules-import -- pull --force
