# UI checks

`measure.mjs` reads the running client's real layout through the Chrome DevTools Protocol.
Node 24 ships a WebSocket client, so it needs no Puppeteer, no Playwright and no driver.

    chrome --headless=new --remote-debugging-port=9222 --user-data-dir=<temp> about:blank
    dotnet run --project src/Pf2e.Client --launch-profile http
    node tools/ui-check/measure.mjs http://localhost:5173/ 320 740

It reports the viewport, every navigation item's box, whether any sits offscreen, whether the
page scrolls sideways, and the smallest tap target on the page. The exit code is the number of
problems, so it works as a check. A strip that scrolls sideways on purpose, such as the trait
filter, may hold content past the edge; anything else past it is a problem.

It opens a tab of its own and closes it afterwards, so two runs against one Chrome never drive
each other's page. It waits a second after the client renders, `SETTLE_MS` to change, because
counts and lists land after the navigation does and move the layout being measured.

A state no URL reaches, such as a category's records or the search dropdown open, is reached with
`ACT`, page script run before measuring. It may await, and `wait(ms)` is in scope:

    ACT="document.querySelector('.pf-bottomnav__item:nth-child(2)').click(); await wait(500);
         document.querySelector('.categories .category').click(); await wait(1500);"       node tools/ui-check/measure.mjs http://localhost:5173/ 320 740

`SCHEME=dark` emulates the dark colour scheme for the run.

## Why it exists

A screenshot is not a measurement. Reading one led to a confident diagnosis of a navigation
overflow at 320px that did not exist, and the fix was written before anything was measured.

`INJECT_CSS` is the part that catches that. It puts a rule back before measuring, so a layout
fix can be shown to matter instead of assumed:

    INJECT_CSS='nav.pf-bottomnav{padding-inline:var(--layout-gutter) !important}' \
      node tools/ui-check/measure.mjs http://localhost:5173/ 320 740

If the numbers do not move, the change was not a fix. That is exactly what happened here.

That example puts back the gutter the bottom bar used to reserve. Six items at the 44px floor
with 8px between them spend 304 of the 320px minimum viewport, so with the gutter the row runs
16..320 and sits a gutter off centre; without it the row runs 0..320 and the items come out at
47px. The numbers moved, so the change was a fix.

## Screenshots

Set `SHOT` to a path and the run also captures the page through the same emulation override the
measurements use:

    SHOT=/tmp/browse.png node tools/ui-check/measure.mjs http://localhost:5173/ 390 844

Do not use Chrome's `--screenshot` flag with `--window-size` for this. It renders at its own
viewport and crops to the window, which made this app look like it clipped content and dropped
two navigation items at 390px when it did neither. Two separate false bugs came from reading
those images. The image and the numbers have to come from the same viewport or one of them lies.

## Client behaviour checks

`verify-client.mjs` drives the running client the way a player does and asserts what the screens
promise, which is the half `measure.mjs` cannot see:

    dotnet run --project src/Pf2e.Api
    dotnet run --project src/Pf2e.Client --launch-profile http
    node tools/ui-check/verify-client.mjs [client url] [api url]

It launches its own headless Chrome on port 9333, or `CDP_LAUNCH_PORT` when another checkout is
running the same check at once.

98 assertions. The header search: six keystrokes make one search and one count carrying the
last one, results open under the field with the exact name first and per-category counts, arrows
and Enter pick a match, Escape closes the list and keeps the words, Enter reaches `/search`, and
`/` focuses the field. The browse screens: the board's blurb, counts and section headings, a
category's total, glance lines on its rows, no COMMON badge, and filters folded behind their
toggle on a phone. Traits: a chip opens the trait sheet without opening its row, the sheet counts
where the trait is found, a place opens that category filtered by it, back closes it, and no
control sits inside another. The record sheet: its kind and level, its gates, its source, a named
record that opens from it with back returning. And the older ones: the category filter's
debounce, paging, the dual-thumb level range and that every row it returns is inside it, the detail sheet
and its title, the deep link that restores a record, back and Escape both closing the sheet
rather than leaving the app, the conditions screen, and a failure whose retry recovers. The
failing service is simulated by blocking the API through the protocol, so nothing has to be
stopped and restarted. It also measures the records screen at 320px under touch emulation, which
`measure.mjs` cannot reach because that screen is three taps in rather than a URL.

## Slider and gesture checks

`verify-sliders.mjs` drives the component gallery in a real Chrome and asserts the behaviour
that cannot be read off the source:

    node tools/ui-check/verify-sliders.mjs <absolute path to src/Pf2e.Components/gallery/index.html>

46 assertions covering pointer-media sizing under genuine touch emulation, keyboard operation
including Home, End and the page keys, the invariant that range thumbs cannot cross, that a touch
drag moves the thumb without scrolling the page, hold-to-confirm on destructive actions, and that
reduced motion zeroes transitions while deliberately leaving the hold delay alone. `cdp.mjs` is
the shared driver; both scripts use only Node builtins.

## Recording a walkthrough

`record.mjs` drives the running app through scripted scenarios and captures frames:

    CLIENT_URL=http://localhost:5173 \
    GALLERY_URL=file:///.../src/Pf2e.Components/gallery/index.html \
      node tools/ui-check/record.mjs <outputDir> [scenario...]

Scenarios are `browse`, `detail`, `conditions` and `sliders`. Frames land as JPEGs beside a
`manifest.json` naming each frame and its caption. Text is typed one key event at a time rather
than by setting `.value`, so the debounce is exercised instead of bypassed.

Frames are captured under the same emulation override `measure.mjs` uses, so a recording and a
measurement always describe the same viewport. That is the whole point: Chrome's `--screenshot`
flag renders at its own viewport and crops, which is how two false bugs got reported here.
