// The app's own behaviour, beside the kit's. Keys the header search needs that Blazor cannot
// express: a default to prevent on only some keys, and a shortcut that reaches in from anywhere.
(() => {
  'use strict';

  const field = () => document.querySelector('[data-site-search]');

  const typing = (target) =>
    target instanceof HTMLElement &&
    (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName));

  document.addEventListener('keydown', (event) => {
    const search = field();
    if (!search) return;

    // Arrows move through the suggestions rather than the caret, and Escape closes them rather
    // than wiping what was typed, which is what a search input does with it by default.
    if (event.target === search && ['ArrowUp', 'ArrowDown', 'Escape'].includes(event.key)) {
      event.preventDefault();
      return;
    }

    // A pointer device has a keyboard beside it; a phone's on-screen keyboard has no reason to
    // be summoned by a key it cannot press.
    if (event.key === '/' && !typing(event.target) && !event.ctrlKey && !event.metaKey && !event.altKey
        && matchMedia('(pointer: fine)').matches) {
      event.preventDefault();
      search.focus();
      search.select();
    }
  });
})();
