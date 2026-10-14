// NoteChange: reads the note that is open in iCloud Notes.
// Runs only when you press "Add this note", only on iCloud Notes pages, and never changes the note.
// The host replaces __TOKEN__ and then polls window.__notechange for the result.
(function (token) {
  var isNotes = /(^|\.)icloud\.com$/i.test(location.hostname) &&
    (/notes/i.test(location.pathname) || /^notes/i.test(location.hostname) || /notes/i.test(location.hash));
  if (!isNotes) return 'skip';

  window.__notechange = { token: token, done: false };

  function visible(el) {
    var r = el.getBoundingClientRect(), s = getComputedStyle(el);
    return r.width > 60 && r.height > 20 && s.visibility !== 'hidden' && s.display !== 'none';
  }

  function toDataUrl(blob) {
    return new Promise(function (resolve, reject) {
      var reader = new FileReader();
      reader.onload = function () { resolve(reader.result); };
      reader.onerror = reject;
      reader.readAsDataURL(blob);
    });
  }

  // Other picture types (HEIC previews, WebP, GIF) are redrawn as PNG.
  async function asPng(source) {
    var bitmap = source instanceof Blob ? await createImageBitmap(source) : source;
    var w = bitmap.naturalWidth || bitmap.width, h = bitmap.naturalHeight || bitmap.height;
    var scale = Math.min(1, 2400 / Math.max(w, h));
    var canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(w * scale));
    canvas.height = Math.max(1, Math.round(h * scale));
    canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL('image/png');
  }

  async function picture(img) {
    var src = img.currentSrc || img.src;
    if (!src) throw new Error('no source');
    if (/^data:image\/(png|jpeg);base64,/i.test(src)) return src;
    var url = new URL(src, location.href);
    var allowed = url.protocol === 'blob:' || url.protocol === 'data:' ||
      (url.protocol === 'https:' && (url.hostname === location.hostname || /(^|\.)icloud(-content)?\.com$/i.test(url.hostname) || /(^|\.)apple\.com$/i.test(url.hostname)));
    if (!allowed) throw new Error('not an iCloud picture');
    try {
      var controller = new AbortController();
      var timer = setTimeout(function () { controller.abort(); }, 10000);
      var response = await fetch(src, { signal: controller.signal, credentials: 'include' });
      clearTimeout(timer);
      if (!response.ok) throw new Error('fetch failed');
      var blob = await response.blob();
      if (blob.size > 8 * 1024 * 1024) return await asPng(blob);
      if (blob.type === 'image/png' || blob.type === 'image/jpeg') return await toDataUrl(blob);
      return await asPng(blob);
    } catch (e) {
      // Fall back to the picture already on screen.
      if (img.complete && img.naturalWidth) return await asPng(img);
      throw e;
    }
  }

  async function capture() {
    var roots = [document];
    for (var i = 0; i < roots.length && i < 40; i++) {
      var all = roots[i].querySelectorAll('*');
      for (var j = 0; j < all.length; j++) if (all[j].shadowRoot) roots.push(all[j].shadowRoot);
    }

    var candidates = [];
    roots.forEach(function (root) {
      root.querySelectorAll('[contenteditable]:not([contenteditable="false"]),[role="textbox"][aria-multiline="true"]').forEach(function (el) {
        if (visible(el) && !el.closest('[role="toolbar"],[role="search"],form,[role="dialog"] input')) candidates.push(el);
      });
    });
    var top = candidates.filter(function (el) { return !candidates.some(function (o) { return o !== el && o.contains(el); }); });
    if (!top.length) return { error: 'Open a note in iCloud first. If it is locked, unlock it there.' };

    // Some editors keep the title and the body apart. Accept only a clear title/body pair.
    var selected = top;
    if (top.length > 1) {
      var titles = top.filter(function (el) {
        return /title/i.test((el.getAttribute('aria-label') || '') + ' ' + (el.getAttribute('data-placeholder') || '') + ' ' + el.className);
      });
      var bodies = top.filter(function (el) { return titles.indexOf(el) < 0; });
      if (titles.length !== 1 || bodies.length !== 1)
        return { error: "NoteChange couldn't tell which note is open. Click inside the note and try again, or copy it and use Paste note." };
      selected = [titles[0], bodies[0]];
    }

    var container = document.createElement('div');
    var warnings = [];
    for (var s = 0; s < selected.length; s++) {
      var source = selected[s];
      var clone = source.cloneNode(true);
      var originals = source.querySelectorAll('*'), copies = clone.querySelectorAll('*');

      // Keep emphasis that the editor shows with CSS rather than tags, and tick boxes.
      for (var k = 0; k < copies.length && k < originals.length; k++) {
        var orig = originals[k], copy = copies[k], css = getComputedStyle(orig), parts = [];
        if (Number(css.fontWeight) >= 600 || css.fontWeight === 'bold') parts.push('font-weight:bold');
        if (css.fontStyle === 'italic') parts.push('font-style:italic');
        var deco = css.textDecorationLine || css.textDecoration || '';
        if (deco.indexOf('underline') >= 0) parts.push('text-decoration:underline');
        if (deco.indexOf('line-through') >= 0) parts.push('text-decoration:line-through');
        if (parts.length) copy.setAttribute('style', parts.join(';')); else copy.removeAttribute('style');
        if (orig.getAttribute('role') === 'checkbox' && !orig.querySelector('[role="checkbox"]')) {
          copy.insertBefore(document.createTextNode(orig.getAttribute('aria-checked') === 'true' ? '☑ ' : '☐ '), copy.firstChild);
        }
        if (orig.tagName === 'INPUT' && orig.type === 'checkbox') {
          if (orig.checked) copy.setAttribute('checked', ''); else copy.removeAttribute('checked');
        }
      }

      var sourceImages = source.querySelectorAll('img'), cloneImages = clone.querySelectorAll('img');
      for (var m = 0; m < cloneImages.length; m++) {
        try {
          cloneImages[m].setAttribute('src', await picture(sourceImages[m]));
        } catch (e) {
          cloneImages[m].removeAttribute('src');
          warnings.push("A picture couldn't be brought across. Check the preview, and save that picture separately if you need it.");
        }
      }
      if (source.querySelector('video,audio,object,embed,a[download]'))
        warnings.push('This note has an attachment. Attachments stay in Apple Notes, so save it separately if you need it.');

      var section = document.createElement(selected.length > 1 && s === 0 ? 'h1' : 'div');
      section.innerHTML = clone.innerHTML;
      container.appendChild(section);
    }

    var text = selected.map(function (el) { return el.innerText || el.textContent || ''; }).join('\n');
    if (!text.trim() && !container.querySelector('img[src]')) return { error: 'This note looks empty.' };
    return { html: container.innerHTML, text: text, warnings: warnings };
  }

  capture().then(function (result) {
    window.__notechange = { token: token, done: true, result: result };
  }, function (e) {
    window.__notechange = { token: token, done: true, result: { error: 'Reading the note failed: ' + ((e && e.message) || e) } };
  });
  return 'started';
})(__TOKEN__);
