# UI checks

`measure.mjs` reads the running client's real layout through the Chrome DevTools Protocol.
Node 24 ships a WebSocket client, so it needs no Puppeteer, no Playwright and no driver.

    chrome --headless=new --remote-debugging-port=9222 --user-data-dir=<temp> about:blank
    dotnet run --project src/Pf2e.Client --launch-profile http
    node tools/ui-check/measure.mjs http://localhost:5173/ 320 740

It reports the viewport, every navigation item's box, whether any sits offscreen, whether the
page scrolls sideways, and the smallest tap target on the page. The exit code is the number of
problems, so it works as a check.

## Why it exists

A screenshot is not a measurement. Reading one led to a confident diagnosis of a navigation
overflow at 320px that did not exist, and the fix was written before anything was measured.

`INJECT_CSS` is the part that catches that. It puts a rule back before measuring, so a layout
fix can be shown to matter instead of assumed:

    INJECT_CSS='nav.bar{grid-template-columns:repeat(6,1fr) !important}' \
      node tools/ui-check/measure.mjs http://localhost:5173/ 320 740

If the numbers do not move, the change was not a fix. That is exactly what happened here.

## Screenshots

Set `SHOT` to a path and the run also captures the page through the same emulation override the
measurements use:

    SHOT=/tmp/browse.png node tools/ui-check/measure.mjs http://localhost:5173/ 390 844

Do not use Chrome's `--screenshot` flag with `--window-size` for this. It renders at its own
viewport and crops to the window, which made this app look like it clipped content and dropped
two navigation items at 390px when it did neither. Two separate false bugs came from reading
those images. The image and the numbers have to come from the same viewport or one of them lies.

## Slider and gesture checks

`verify-sliders.mjs` drives the component gallery in a real Chrome and asserts the behaviour
that cannot be read off the source:

    node tools/ui-check/verify-sliders.mjs <absolute path to src/Pf2e.Components/gallery/index.html>

46 assertions covering pointer-media sizing under genuine touch emulation, keyboard operation
including Home, End and the page keys, the invariant that range thumbs cannot cross, that a touch
drag moves the thumb without scrolling the page, hold-to-confirm on destructive actions, and that
reduced motion zeroes transitions while deliberately leaving the hold delay alone. `cdp.mjs` is
the shared driver; both scripts use only Node builtins.
