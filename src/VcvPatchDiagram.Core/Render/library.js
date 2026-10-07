// Panel previews for the VCV Library icons on boxes: a module's link (.lib[data-library]) or a folded group's
// panels (.lib[data-panels], "plugin/model*count …").
// Shared: embedded in the static HTML export, and copied to the Blazor app's wwwroot/js at build time.
// Nothing is fetched until an icon is hovered; a loaded image is kept for the page's life (and the browser's HTTP cache
// keeps it longer), and a module the Library doesn't have is remembered, so it is never asked for again.
window.vpdLibraryPreview = function (container) {
  if (!container || container.dataset.vpdLibrary) return;
  container.dataset.vpdLibrary = '1';
  const site = 'https://library.vcvrack.com/screenshots/';
  const missingKey = 'vpd-library-missing';
  const groupLimit = 12;
  const images = new Map();
  let missing;
  try { missing = new Set(JSON.parse(localStorage.getItem(missingKey) || '[]')); } catch { missing = new Set(); }
  const remember = () => { try { localStorage.setItem(missingKey, JSON.stringify([...missing])); } catch { } };
  const url = (key, size) => site + size + '/' + key.split('/').map(encodeURIComponent).join('/') + '.webp';
  const selector = '.lib[data-library], .lib[data-panels]';

  const card = document.createElement('div');
  card.className = 'vpd-preview';
  card.hidden = true;
  // On the body, out of the app's rendered markup; it takes the diagram's theme colors when shown.
  document.body.appendChild(card);
  let link = null, timer = 0;

  function hide() {
    clearTimeout(timer);
    link = null;
    card.hidden = true;
  }

  function place() {
    if (!link) return;
    // The app re-renders the diagram on a click (unfolding a group): the icon may be gone without a pointerout.
    if (!link.isConnected) { hide(); return; }
    const box = link.getBoundingClientRect();
    const width = card.offsetWidth, height = card.offsetHeight;
    const left = box.right + 8 + width <= window.innerWidth ? box.right + 8 : Math.max(4, box.left - 8 - width);
    card.style.left = left + 'px';
    card.style.top = Math.max(4, Math.min(window.innerHeight - height - 4, box.top + (box.height / 2) - (height / 2))) + 'px';
  }

  function markMissing(key) {
    container.querySelectorAll('.lib[data-library="' + CSS.escape(key) + '"]').forEach(l => l.classList.add('missing'));
  }

  // The image of one module, created on first use and shared by every preview showing it; null once known missing.
  function image(key) {
    if (missing.has(key)) return null;
    let img = images.get(key);
    if (!img) {
      img = new Image();
      img.alt = key;
      img.src = url(key, 200);
      img.srcset = url(key, 400) + ' 2x';
      img.addEventListener('load', place);
      img.addEventListener('error', () => {
        images.delete(key);
        missing.add(key);
        remember();
        markMissing(key);
        if (link) render(link);
      });
      images.set(key, img);
    }
    return img;
  }

  function note(text) {
    const div = document.createElement('div');
    div.className = 'note';
    div.textContent = text;
    return div;
  }

  function render(target) {
    const key = target.dataset.library;
    if (key) {
      const img = image(key);
      if (!img) markMissing(key);
      card.replaceChildren(img || 'Not in the VCV Library');
    } else {
      // A group: each distinct module once, with a badge when there are several of it.
      const panels = target.dataset.panels.split(' ').filter(p => p).map(p => {
        const star = p.lastIndexOf('*');
        return { key: p.slice(0, star), count: +p.slice(star + 1) };
      });
      const shown = panels.slice(0, groupLimit);
      const others = panels.length - shown.length;
      let unlisted = +(target.dataset.unlisted || 0);
      const row = document.createElement('div');
      row.className = 'panels';
      shown.forEach(p => {
        const img = image(p.key);
        if (!img) { unlisted += p.count; return; }
        const figure = document.createElement('figure');
        figure.title = p.key;
        figure.appendChild(img);
        if (p.count > 1) {
          const badge = document.createElement('span');
          badge.className = 'count';
          badge.textContent = '×' + p.count;
          figure.appendChild(badge);
        }
        row.appendChild(figure);
      });
      const content = [row];
      if (others > 0) content.push(note('+ ' + others + ' other module' + (others > 1 ? 's' : '')));
      if (unlisted > 0) content.push(note('+ ' + unlisted + ' not in the VCV Library'));
      card.replaceChildren(...content);
    }
    place();
  }

  function show(target) {
    link = target;
    const style = getComputedStyle(container);
    ['--vpd-panel', '--vpd-muted', '--vpd-ink'].forEach(v => card.style.setProperty(v, style.getPropertyValue(v)));
    card.hidden = false;
    render(target);
  }

  // Delegated, so it keeps working when the app re-renders the diagram. A short delay: sweeping the mouse across
  // the diagram doesn't fetch every panel it crosses.
  container.addEventListener('pointerover', e => {
    const target = e.target.closest && e.target.closest(selector);
    if (!target || target === link) return;
    clearTimeout(timer);
    timer = setTimeout(() => { if (target.isConnected) show(target); }, 250);
  });
  container.addEventListener('pointerout', e => {
    const target = e.target.closest && e.target.closest(selector);
    if (!target || (e.relatedTarget && target.contains(e.relatedTarget))) return;
    hide();
  });
  // A click (which may unfold or fold the box), a pan or a zoom: the preview no longer belongs where it is.
  ['pointerdown', 'wheel', 'scroll'].forEach(type => container.addEventListener(type, hide, { capture: true, passive: true }));
};
