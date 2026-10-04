// Pan (drag the background) and Ctrl+wheel zoom for a diagram's scroll box.
// Shared: embedded in the static HTML export, and copied to the Blazor app's wwwroot/js at build time.
window.vpdPanZoom = function (scroller, onZoom) {
  if (!scroller || scroller.dataset.vpdPan) return;
  scroller.dataset.vpdPan = '1';
  let drag = null;
  let suppressClick = false;

  scroller.addEventListener('pointerdown', e => {
    // Boxes and cables keep their own clicks (unfold, select); everything else pans.
    if (e.button !== 0 || e.target.closest('.vpd-node, .vpd-edge')) return;
    drag = { x: e.clientX, y: e.clientY, left: scroller.scrollLeft, top: scroller.scrollTop, moved: false, id: e.pointerId };
  });
  scroller.addEventListener('pointermove', e => {
    if (!drag) return;
    const dx = e.clientX - drag.x, dy = e.clientY - drag.y;
    if (!drag.moved && Math.abs(dx) + Math.abs(dy) > 4) {
      drag.moved = true;
      scroller.classList.add('panning');
      scroller.setPointerCapture(drag.id);
    }
    if (drag.moved) {
      scroller.scrollLeft = drag.left - dx;
      scroller.scrollTop = drag.top - dy;
    }
  });
  const end = () => {
    if (drag && drag.moved) suppressClick = true;
    drag = null;
    scroller.classList.remove('panning');
  };
  scroller.addEventListener('pointerup', end);
  scroller.addEventListener('pointercancel', end);
  // A drag must not also count as a click (which would fold the lane under the pointer).
  scroller.addEventListener('click', e => {
    if (suppressClick) { e.stopPropagation(); e.preventDefault(); suppressClick = false; }
  }, true);

  scroller.addEventListener('wheel', e => {
    if (!e.ctrlKey || !onZoom) return;
    e.preventDefault();
    // Keep the point under the cursor in place while zooming.
    const rect = scroller.getBoundingClientRect();
    const px = e.clientX - rect.left + scroller.scrollLeft;
    const py = e.clientY - rect.top + scroller.scrollTop;
    const before = scroller.scrollWidth;
    Promise.resolve(onZoom(e.deltaY < 0 ? 1.15 : 1 / 1.15)).then(() => requestAnimationFrame(() => {
      const ratio = scroller.scrollWidth / before;
      scroller.scrollLeft = px * ratio - (e.clientX - rect.left);
      scroller.scrollTop = py * ratio - (e.clientY - rect.top);
    }));
  }, { passive: false });
};

// Zoom that fits the diagram's width in the scroll box, never below the readable minimum.
window.vpdFitZoom = (scroller, width, minZoom, maxZoom) =>
  Math.max(minZoom, Math.min(maxZoom, (scroller.clientWidth - 2) / width));
