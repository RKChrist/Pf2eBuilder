/* The kit's one behaviour file, the counterpart to the one stylesheet. The static gallery and
   any Blazor host load this same file with one tag and get the same behaviour, so no component
   carries a script of its own and none reaches for IJSRuntime. It writes presentational state
   only: the custom properties the stylesheet reads, the bubble's text, and class flags. */
(() => {
  'use strict';

  /* Blazor re-renders constantly and replaces subtrees wholesale, so every root is marked and
     enhancing twice is a no-op. A root missing the children it needs is left unmarked rather
     than half enhanced. */
  const enhanced = new WeakSet();

  const numberOf = (raw, fallback) => {
    const value = Number(raw);
    return raw !== '' && Number.isFinite(value) ? value : fallback;
  };

  const minOf = (input) => numberOf(input.min, 0);
  const maxOf = (input) => numberOf(input.max, 100);
  const stepOf = (input) => Math.max(numberOf(input.step, 1), 0) || 1;

  const fractionOf = (input) => {
    const min = minOf(input);
    const max = maxOf(input);
    return max > min ? (Number(input.value) - min) / (max - min) : 0;
  };

  const lengthOf = (element, property) =>
    parseFloat(getComputedStyle(element).getPropertyValue(property)) || 0;

  /* One pointer drags one thumb, so the drag lives here rather than per slider. A mouse
     released off the control never fires pointerup on it, and a listener per slider would be a
     pair of permanent window listeners holding a detached root every time Blazor rebuilt one. */
  let dragging = null;
  let touchDrag = false;

  const endDrag = () => {
    if (dragging) dragging.classList.remove('pf-slider--bubbling');
    dragging = null;
    touchDrag = false;
  };

  window.addEventListener('pointerup', endDrag);
  window.addEventListener('pointercancel', endDrag);

  function enhanceSlider(root) {
    const rail = root.querySelector('.pf-slider__rail');
    const inputs = [...root.querySelectorAll('.pf-slider__input')];
    if (!rail || inputs.length === 0) return false;

    const ranged = root.classList.contains('pf-slider--range');
    const low = ranged ? root.querySelector('.pf-slider__input--low') : inputs[0];
    const high = ranged ? root.querySelector('.pf-slider__input--high') : null;
    if (ranged && (!low || !high)) return false;

    const bubble = root.querySelector('.pf-slider__bubble');
    const readouts = [...root.querySelectorAll('.pf-slider__readout')];
    const pairs = (ranged ? [[readouts[0], low], [readouts[1], high]] : [[readouts[0], inputs[0]]])
      .filter(([readout]) => readout);
    const tickCount = root.querySelectorAll('.pf-slider__tick').length;

    let lastTick = -1;

    const tickOf = (input) => (tickCount > 1 ? Math.round(fractionOf(input) * (tickCount - 1)) : -1);

    /* Every value written here is a plain ratio. Nothing is measured, so no sizing decision
       leaves the stylesheet and nothing goes stale when the rail changes width. */
    function paint(active) {
      root.style.setProperty('--pf-fill-start', ranged ? `${fractionOf(low) * 100}%` : '0%');
      root.style.setProperty('--pf-fill-end', `${fractionOf(ranged ? high : inputs[0]) * 100}%`);
      root.style.setProperty('--pf-fraction', String(fractionOf(active)));
      if (bubble) bubble.textContent = active.value;
    }

    function sync(active) {
      if (ranged) {
        if (Number(low.value) > Number(high.value)) {
          if (active === high) high.value = low.value;
          else low.value = high.value;
        }
        /* Both inputs span the whole domain, because two overlapped inputs that do not share one
           domain render their thumbs at the wrong x. So the wall a screen reader is told about
           is the other thumb's value rather than the attribute. */
        low.setAttribute('aria-valuemax', high.value);
        high.setAttribute('aria-valuemin', low.value);
        /* Coincident thumbs leave only the top one grabbable, and dragging it into the other is
           clamped straight back. Raising the low one past the midpoint keeps a thumb that can
           actually move under the finger, so the pair never parks somewhere it cannot leave. */
        root.classList.toggle(
          'pf-slider--met-high',
          low.value === high.value && fractionOf(high) >= 0.5,
        );
      }
      for (const [readout, input] of pairs) readout.value = input.value;
      paint(active);
    }

    function detent(input) {
      if (!touchDrag || dragging !== root || tickCount < 2 || !('vibrate' in navigator)) return;
      const tick = tickOf(input);
      if (tick === lastTick) return;
      lastTick = tick;
      navigator.vibrate(10);
    }

    /* Painting before the bubble is shown, not only on input, because the thumb being pressed
       or tabbed to may not be the one painted last. */
    const show = (input) => {
      paint(input);
      root.classList.add('pf-slider--bubbling');
    };

    for (const input of inputs) {
      input.addEventListener('input', () => {
        sync(input);
        detent(input);
        /* :focus-visible turns on at the first key press without firing focus again, so a
           slider clicked and then driven by the arrow keys asks here rather than there. */
        if (input.matches(':focus-visible')) show(input);
      });
      input.addEventListener('pointerdown', (event) => {
        show(input);
        dragging = root;
        touchDrag = event.pointerType === 'touch';
        lastTick = tickOf(input);
      });
      input.addEventListener('focus', () => {
        if (input.matches(':focus-visible')) show(input);
      });
      input.addEventListener('blur', () => root.classList.remove('pf-slider--bubbling'));
    }

    for (const [readout, input] of pairs) {
      readout.addEventListener('change', () => {
        const typed = Number(readout.value);
        if (readout.value === '' || !Number.isFinite(typed)) {
          readout.value = input.value;
          return;
        }
        const min = minOf(input);
        const max = maxOf(input);
        const step = stepOf(input);
        const bounded = Math.min(Math.max(typed, min), max);
        const snapped = min + Math.round((bounded - min) / step) * step;
        input.value = String(Math.min(Math.max(snapped, min), max));
        sync(input);
      });
    }

    sync(ranged ? low : inputs[0]);
    return true;
  }

  function enhanceSheet(sheet) {
    /* Bound to the sheet rather than the panel, because Blazor renders the panel only while the
       sheet is open and hands back a different element every time. */
    let panel = null;
    let startY = 0;
    let distance = 0;

    sheet.addEventListener('pointerdown', (event) => {
      const handle = event.target.closest?.('.pf-sheet__grab, .pf-sheet__head');
      if (!handle || event.target.closest('.pf-sheet__close')) return;
      const dragged = handle.closest('.pf-sheet__panel');
      if (!dragged) return;
      panel = dragged;
      startY = event.clientY;
      distance = 0;
      sheet.classList.add('pf-sheet--dragging');
      try {
        panel.setPointerCapture(event.pointerId);
      } catch {
        /* A pointer that is already gone cannot be captured, and the drag still works from
           whichever of its events do arrive. */
      }
    });

    sheet.addEventListener('pointermove', (event) => {
      if (!panel) return;
      distance = Math.max(event.clientY - startY, 0);
      panel.style.setProperty('--pf-sheet-drag', `${distance}px`);
    });

    /* Dismissal goes through the close button both hosts already handle, so there is no second
       dismissal path and no custom event for a host to register. */
    const release = () => {
      sheet.classList.remove('pf-sheet--dragging');
      if (!panel) return;
      const dismissed = distance > panel.offsetHeight / 4;
      const close = panel.querySelector('.pf-sheet__close');
      panel.style.removeProperty('--pf-sheet-drag');
      panel = null;
      if (dismissed && close) close.click();
    };

    sheet.addEventListener('pointerup', release);
    sheet.addEventListener('pointercancel', release);

    /* Escape is the keyboard's dismissal, and it takes the same path as the drag so a host
       still only handles the one close. It needs the focus the panel takes below: without it
       the key press goes to whatever is still focused behind the scrim. */
    sheet.addEventListener('keydown', (event) => {
      if (event.key !== 'Escape') return;
      sheet.querySelector('.pf-sheet__close')?.click();
    });
    return true;
  }

  /* A dialog that leaves focus on the page behind it is unreachable by keyboard and silent to a
     screen reader. Blazor renders a new panel each time the sheet opens, so each new element is
     focused once as it arrives. */
  function focusPanel(panel) {
    panel.focus({ preventScroll: true });
    return true;
  }

  function enhanceHold(button) {
    let completed = false;
    let timer = 0;

    /* A keyboard activation reports detail 0 and passes straight through. The mis-tap this
       guards against is a fat finger mid-session, and holding a key down would cost the people
       who need the keyboard more than it protects them. */
    /* The enhancement outlives the class, because Blazor can drop pf-btn--hold on a re-render
       and a guard that kept swallowing taps would leave a button that does nothing at all. */
    const guarded = () => button.classList.contains('pf-btn--hold');

    /* A press and hold is the gesture this button is for, so the platform's own long-press menu
       is a competing interpretation of it and has to be refused. On Android that menu also
       cancels the pointer partway through, which would put the delay out of reach entirely. */
    button.addEventListener('contextmenu', (event) => {
      if (guarded()) event.preventDefault();
    });

    button.addEventListener('click', (event) => {
      if (event.detail === 0 || !guarded()) return;
      if (completed) {
        completed = false;
        return;
      }
      event.preventDefault();
      event.stopImmediatePropagation();
    }, true);

    button.addEventListener('pointerdown', () => {
      if (button.disabled || !guarded()) return;

      /* No resolved delay means the guard is not configured, so the press is refused and the
         click above stays blocked. A destructive button that does nothing is noticed; one that
         silently fires on a tap is noticed after a character is gone. */
      const delay = lengthOf(button, '--motion-duration-hold');
      if (delay <= 0) return;

      button.classList.add('pf-btn--holding');
      clearTimeout(timer);
      timer = setTimeout(() => {
        button.classList.remove('pf-btn--holding');
        completed = true;
        button.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 1 }));
      }, delay);
    });

    const abandon = () => {
      clearTimeout(timer);
      button.classList.remove('pf-btn--holding');
    };

    for (const type of ['pointerup', 'pointerleave', 'pointercancel']) {
      button.addEventListener(type, abandon);
    }
    return true;
  }

  const roots = [
    ['.pf-slider', enhanceSlider],
    ['.pf-sheet', enhanceSheet],
    ['.pf-sheet__panel', focusPanel],
    ['.pf-btn--hold', enhanceHold],
  ];

  function enhance(element, enhancer) {
    if (enhanced.has(element)) return;
    if (enhancer(element) !== false) enhanced.add(element);
  }

  function sweep(node) {
    if (node.nodeType !== Node.ELEMENT_NODE) return;
    for (const [selector, enhancer] of roots) {
      if (node.matches(selector)) enhance(node, enhancer);
      for (const element of node.querySelectorAll(selector)) enhance(element, enhancer);
    }
  }

  new MutationObserver((records) => {
    for (const record of records) {
      for (const node of record.addedNodes) sweep(node);
    }
  }).observe(document.documentElement, { childList: true, subtree: true });

  /* documentElement exists from the first tag onward, so this sweep covers whatever has parsed
     and the observer covers the rest. That holds whether the file runs before DOMContentLoaded
     or is injected long after it. */
  sweep(document.documentElement);
})();
