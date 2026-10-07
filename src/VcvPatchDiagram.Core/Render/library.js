// Panel preview for the VCV Library links on module boxes (.lib[data-library]).
// Shared: embedded in the static HTML export, and copied to the Blazor app's wwwroot/js at build time.
// Nothing is fetched until a link is hovered; a loaded image is kept for the page's life (and the browser's HTTP cache
// keeps it longer), and a module the Library doesn't have is remembered, so it is never asked for again.
window.vpdLibraryPreview = function (container) {
  if (!container || container.dataset.vpdLibrary) return;
  container.dataset.vpdLibrary = '1';
  const site = 'https://library.vcvrack.com/screenshots/';
  const missingKey = 'vpd-library-missing';
  const images = new Map();
  let missing;
  try { missing = new Set(JSON.parse(localStorage.getItem(missingKey) || '[]')); } catch { missing = new Set(); }
  const remember = () => { try { localStorage.setItem(missingKey, JSON.stringify([...missing])); } catch { } };
  const url = (key, size) => site + size + '/' + key.split('/').map(encodeURIComponent).join('/') + '.webp';

  const card = document.createElement('div');
  card.className = 'vpd-preview';
  card.hidden = true;
  // On the body, out of the app's rendered markup; it takes the diagram's theme colors when shown.
  document.body.appendChild(card);
  let link = null, timer = 0;

  function place() {
    if (!link) return;
    const box = link.getBoundingClientRect();
    const width = card.offsetWidth, height = card.offsetHeight;
    const left = box.right + 8 + width <= window.innerWidth ? box.right + 8 : Math.max(4, box.left - 8 - width);
    card.style.left = left + 'px';
    card.style.top = Math.max(4, Math.min(window.innerHeight - height - 4, box.top + (box.height / 2) - (height / 2))) + 'px';
  }

  function showMissing(key) {
    container.querySelectorAll('.lib[data-library="' + CSS.escape(key) + '"]').forEach(l => l.classList.add('missing'));
    card.replaceChildren('Not in the VCV Library');
    place();
  }

  function show(target) {
    link = target;
    const key = link.dataset.library;
    const style = getComputedStyle(container);
    ['--vpd-panel', '--vpd-muted'].forEach(v => card.style.setProperty(v, style.getPropertyValue(v)));
    card.hidden = false;
    if (missing.has(key)) { showMissing(key); return; }
    let img = images.get(key);
    if (!img) {
      img = new Image();
      img.alt = key;
      img.src = url(key, 200);
      img.srcset = url(key, 400) + ' 2x';
      img.addEventListener('load', () => { if (link && link.dataset.library === key) place(); });
      img.addEventListener('error', () => {
        images.delete(key);
        missing.add(key);
        remember();
        if (link && link.dataset.library === key) showMissing(key);
      });
      images.set(key, img);
    }
    card.replaceChildren(img);
    place();
  }

  // Delegated, so it keeps working when the app re-renders the diagram. A short delay: sweeping the mouse across
  // the diagram doesn't fetch every panel it crosses.
  container.addEventListener('pointerover', e => {
    const target = e.target.closest && e.target.closest('.lib[data-library]');
    if (!target || target === link) return;
    clearTimeout(timer);
    timer = setTimeout(() => show(target), 250);
  });
  container.addEventListener('pointerout', e => {
    const target = e.target.closest && e.target.closest('.lib[data-library]');
    if (!target || (e.relatedTarget && target.contains(e.relatedTarget))) return;
    clearTimeout(timer);
    link = null;
    card.hidden = true;
  });
};
