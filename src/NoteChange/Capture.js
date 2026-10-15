// NoteChange note reader. Read-only: it copies what's on screen and never changes the note.
// The host wraps these functions: ncCaptureElement runs on one editor element found through the
// browser's inspector; ncScanDocument searches a frame (including open shadow roots and
// same-origin frames inside it); ncCaptureSelection reads whatever the person has selected.

function ncStyle(el) {
  var view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
  return view.getComputedStyle(el);
}

function ncVisible(el) {
  var r = el.getBoundingClientRect(), s = ncStyle(el);
  return {
    ok: r.width > 60 && r.height > 20 && s.visibility !== 'hidden' && s.display !== 'none' && s.opacity !== '0',
    w: Math.round(r.width),
    h: Math.round(r.height)
  };
}

function ncToDataUrl(blob) {
  return new Promise(function (resolve, reject) {
    var reader = new FileReader();
    reader.onload = function () { resolve(reader.result); };
    reader.onerror = reject;
    reader.readAsDataURL(blob);
  });
}

// Other picture types (HEIC previews, WebP, GIF) are redrawn as PNG.
async function ncAsPng(source) {
  var bitmap = source instanceof Blob ? await createImageBitmap(source) : source;
  var w = bitmap.naturalWidth || bitmap.width, h = bitmap.naturalHeight || bitmap.height;
  if (!w || !h) throw new Error('empty picture');
  var scale = Math.min(1, 2400 / Math.max(w, h));
  var canvas = document.createElement('canvas');
  canvas.width = Math.max(1, Math.round(w * scale));
  canvas.height = Math.max(1, Math.round(h * scale));
  canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  return canvas.toDataURL('image/png');
}

async function ncPicture(img, base) {
  var src = img.currentSrc || img.getAttribute('src') || '';
  if (!src) throw new Error('no source');
  if (/^data:image\/(png|jpeg);base64,/i.test(src)) return src;
  var url = new URL(src, base || location.href);
  var allowed = url.protocol === 'blob:' || url.protocol === 'data:' ||
    (url.protocol === 'https:' && (/(^|\.)icloud(-content)?\.com$/i.test(url.hostname) || /(^|\.)apple\.com$/i.test(url.hostname) || url.hostname === location.hostname));
  if (!allowed) throw new Error('not an iCloud picture');
  try {
    var controller = new AbortController();
    var timer = setTimeout(function () { controller.abort(); }, 10000);
    var response = await fetch(url.href, { signal: controller.signal, credentials: 'include' });
    clearTimeout(timer);
    if (!response.ok) throw new Error('fetch failed');
    var blob = await response.blob();
    if ((blob.type === 'image/png' || blob.type === 'image/jpeg') && blob.size <= 8 * 1024 * 1024) return await ncToDataUrl(blob);
    return await ncAsPng(blob);
  } catch (e) {
    // Fall back to the picture already drawn on screen.
    if (img.complete && img.naturalWidth) return await ncAsPng(img);
    throw e;
  }
}

var NC_PICTURE_WARNING = "A picture couldn't be brought across. Check the preview, and save that picture separately if you need it.";

// Copies emphasis shown with CSS, tick boxes and pictures from the live element into its clone.
async function ncProcess(source, clone, warnings, withStyles) {
  if (withStyles) {
    var originals = source.querySelectorAll('*'), copies = clone.querySelectorAll('*');
    for (var k = 0; k < copies.length && k < originals.length; k++) {
      var orig = originals[k], copy = copies[k], css = ncStyle(orig), parts = [];
      if (Number(css.fontWeight) >= 600 || css.fontWeight === 'bold') parts.push('font-weight:bold');
      if (css.fontStyle === 'italic') parts.push('font-style:italic');
      var deco = css.textDecorationLine || css.textDecoration || '';
      if (deco.indexOf('underline') >= 0) parts.push('text-decoration:underline');
      if (deco.indexOf('line-through') >= 0) parts.push('text-decoration:line-through');
      if (parts.length) copy.setAttribute('style', parts.join(';')); else copy.removeAttribute('style');
      if (orig.getAttribute('role') === 'checkbox' && !orig.querySelector('[role="checkbox"]'))
        copy.insertBefore(copy.ownerDocument.createTextNode(orig.getAttribute('aria-checked') === 'true' ? '☑ ' : '☐ '), copy.firstChild);
      if (orig.tagName === 'INPUT' && orig.type === 'checkbox') {
        if (orig.checked) copy.setAttribute('checked', ''); else copy.removeAttribute('checked');
      }
    }
  }
  var sourceImages = source.querySelectorAll ? source.querySelectorAll('img') : [];
  var cloneImages = clone.querySelectorAll('img');
  var base = (source.ownerDocument || document).baseURI;
  for (var m = 0; m < cloneImages.length; m++) {
    try {
      cloneImages[m].setAttribute('src', await ncPicture(sourceImages[m] || cloneImages[m], base));
    } catch (e) {
      cloneImages[m].removeAttribute('src');
      warnings.push(NC_PICTURE_WARNING);
    }
  }
  if (clone.querySelector('video,audio,object,embed,a[download]'))
    warnings.push('This note has an attachment. Attachments stay in Apple Notes, so save it separately if you need it.');
}

async function ncCaptureElement(el) {
  var v = ncVisible(el);
  if (!v.ok) return { visible: false, w: v.w, h: v.h };
  var warnings = [];
  var clone = el.cloneNode(true);
  await ncProcess(el, clone, warnings, true);
  var hint = (el.getAttribute('aria-label') || '') + ' ' + (el.getAttribute('data-placeholder') || '') + ' ' +
    (el.getAttribute('placeholder') || '') + ' ' + (typeof el.className === 'string' ? el.className : '');
  return {
    visible: true, w: v.w, h: v.h,
    title: /title/i.test(hint),
    html: clone.innerHTML,
    text: el.innerText || el.textContent || '',
    warnings: warnings
  };
}

async function ncCaptureSelection(docs) {
  docs = docs || [document];
  var sel = null, text = '';
  for (var d = 0; d < docs.length; d++) {
    var s = docs[d].getSelection ? docs[d].getSelection() : null;
    if (s && s.rangeCount > 0 && !s.isCollapsed && s.toString().trim()) { sel = s; text = s.toString(); break; }
  }
  if (!sel) return null;
  var div = document.createElement('div');
  for (var i = 0; i < sel.rangeCount; i++) div.appendChild(sel.getRangeAt(i).cloneContents());
  var warnings = [];
  await ncProcess(div, div, warnings, false);
  return { visible: true, w: 0, h: 0, title: false, html: div.innerHTML, text: text, warnings: warnings, fromSelection: true };
}

function ncAllowed() {
  if (/(^|\.)icloud\.com$/i.test(location.hostname)) return true;
  if (location.protocol === 'about:' || location.protocol === 'blob:' || location.protocol === 'data:') {
    var a = location.ancestorOrigins;
    for (var i = 0; a && i < a.length; i++) if (/^https:\/\/([^\/]*\.)?icloud\.com(:\d+)?$/i.test(a[i])) return true;
  }
  return false;
}

async function ncScanDocument() {
  if (!ncAllowed()) return { skip: true };
  var roots = [document];
  for (var i = 0; i < roots.length && i < 80; i++) {
    var all = roots[i].querySelectorAll('*');
    for (var j = 0; j < all.length; j++) {
      var el = all[j];
      if (el.shadowRoot) roots.push(el.shadowRoot);
      if (el.tagName === 'IFRAME' || el.tagName === 'FRAME') {
        try { var d = el.contentDocument; if (d && d.documentElement) roots.push(d); } catch (e) { }
      }
    }
  }
  var candidates = [];
  roots.forEach(function (root) {
    root.querySelectorAll('[contenteditable],[role="textbox"]').forEach(function (el) {
      var ce = el.getAttribute('contenteditable');
      if (ce !== null && ce.toLowerCase() === 'false') return;
      if (el.closest('[role="toolbar"],[role="search"],form')) return;
      candidates.push(el);
    });
  });
  var top = candidates.filter(function (el) { return !candidates.some(function (o) { return o !== el && o.contains(el); }); });
  var items = [];
  for (var k = 0; k < top.length && k < 30; k++) {
    try {
      var r = await ncCaptureElement(top[k]);
      if (r.visible) items.push(r);
    } catch (e) { }
  }
  var selection = items.length ? null : await ncCaptureSelection(roots.filter(function (r) { return r.nodeType === 9; }));
  return { items: items, selection: selection, roots: roots.length, editable: candidates.length };
}
