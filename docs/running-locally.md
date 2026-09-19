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

## When the build says a file is locked

A running API or client holds `Pf2e.Domain.dll` open, and the next build fails with
`MSB3027: could not copy ... the file is locked by "Pf2e.Api"`. Nothing is wrong with the code.

    stop.cmd

`run.cmd` calls it first, so restarting never hits this. Stopping by hand from PowerShell:

    Get-Process Pf2e.Api, Pf2e.Client -ErrorAction SilentlyContinue | Stop-Process -Force

`pkill -f Pf2e.Api` from a bash shell does **not** work here; the process is a Windows
executable and pkill silently matches nothing, which looks like the kill succeeded.

## Serving it to the table, over ngrok or your own wifi

    serve.cmd

One process, one port. The API hosts the published client, so there is a single origin, no
CORS to configure and only one tunnel to open. Then, in another window:

    ngrok http 5092

Open the https URL ngrok prints on every phone and every desktop at the table. Desktop and
phones together is the point: the layout adapts per device, and everyone on the same table code
sees the same numbers update live.

Two details that make it work and are easy to miss.

The client's `BaseUrl` is empty in the published config, which means "wherever this page came
from". That is what makes one tunnel enough. The two-process development run overrides it back
to `http://localhost:5092` through `wwwroot/appsettings.Development.json`.

The API binds `0.0.0.0` rather than `localhost`, because a tunnel and a phone on your wifi are
both a different machine as far as the socket is concerned. It also honours forwarded headers,
since ngrok terminates TLS and forwards plain HTTP; without that the app believes every request
is insecure and any absolute URL it builds comes back as `http` on an `https` page, which the
browser blocks as mixed content.

### What this is not

It is not authenticated. Anyone who knows a table code can read and change every character on
it. That is fine on your own wifi and it is not fine on a public URL, so treat an ngrok link as
something you share with your table and let expire, not as a deployment.
