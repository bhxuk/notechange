const {test}=require('node:test');const assert=require('node:assert/strict');const {normalize,format,toHTML,toText,toMarkdown}=require('../src/content.cjs');const {word,email}=require('../src/exports.cjs');
test('first line becomes title without rewriting punctuation or body',()=>{const m=normalize({text:'Mum’s shopping\nMilk & bread\nDon’t forget 0.5 kg!'});const d=format(m,{});assert.equal(d.title,'Mum’s shopping');assert.equal(toText(d),'Mum’s shopping\n\nMilk & bread\n\nDon’t forget 0.5 kg!');});
test('custom title does not consume original text',()=>{const d=format(normalize({text:'First line\nSecond line'}),{titleMode:'custom',customTitle:'New heading'});assert.equal(d.blocks.length,2);assert.match(toText(d),/First line/);});
test('no-title keeps heading and original body',()=>{const d=format(normalize({text:'My title\nMy body'}),{titleMode:'none'});assert.equal(d.title,'');assert.equal(d.blocks.length,2);});
test('marks, checklists, Unicode, hyperlinks and tables survive',()=>{const d=format(normalize({text:'Title\nDetails',html:'<h1>Title</h1><p><strong>Exact words</strong> <em>café</em> <a href="https://example.com">link</a></p><ul><li><input type="checkbox" checked>Done</li></ul><table><tr><th>Item</th><th>Qty</th></tr><tr><td>Milk</td><td>2</td></tr></table>'}),{});const h=toHTML(d);assert.match(h,/<strong>Exact words<\/strong>/);assert.match(h,/café/);assert.match(h,/☑ Done/);assert.match(h,/<td>2<\/td>/);});
test('active and remote content cannot run or load in preview',()=>{const m=normalize({text:'hello',html:'<p>Hello<script>alert(1)</script><a href="javascript:alert(1)">click</a><img src="https://evil.example/tracker" onerror="alert(2)"></p>'});const h=toHTML(format(m,{}));assert.doesNotMatch(h,/javascript:|<script|onerror|src="https/);assert.equal(m.warnings.length,1);});
test('real Word package is generated',async()=>{const buffer=await word(format(normalize({text:'Title\nBody'}),{}));assert.equal(buffer.subarray(0,2).toString(),'PK');assert.ok(buffer.length>1000);});
test('email draft keeps an empty subject and rich HTML body',()=>{const doc=format(normalize({text:'Title\nBody'}),{});const value=email(doc);assert.match(value,/Subject: \r\n/);assert.match(value,/X-Unsent: 1/);assert.match(value,/text\/html/);const part=value.split('Content-Type: text/html; charset=UTF-8')[1].split('\r\n\r\n')[1].split('\r\n--')[0];assert.match(Buffer.from(part.replace(/\s/g,''),'base64').toString('utf8'),/<h1>Title<\/h1><p>Body<\/p>/);});
test('empty and oversized notes fail clearly',()=>{assert.throws(()=>normalize({text:''}),/empty/);assert.throws(()=>normalize({text:'a'.repeat(13*1024*1024)}),/large/);});
test('blank lines are retained as paragraphs',()=>{const d=format(normalize({text:'Title\n\nLast line'}),{});assert.equal(d.blocks.length,2);assert.match(toHTML(d),/<p><br><\/p>/);});
test('markdown explicitly warns when tables are flattened',()=>{const doc=format(normalize({text:'Title',html:'<h1>Title</h1><table><tr><td>A</td><td>B</td></tr></table>'}),{});assert.equal(toMarkdown(doc).warnings.length,1);assert.match(toMarkdown(doc).text,/A\tB/);});

test('embedded pictures are retained in Word and email MIME',async()=>{
 const src='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aH1cAAAAASUVORK5CYII=';
 const d=format(normalize({text:'Title',html:`<h1>Title</h1><p><img src="${src}" alt="My picture"></p>`}),{});
 assert.equal(d.warnings.length,0);assert.ok((await word(d)).length>1000);const e=email(d);assert.match(e,/Content-ID: <picture-0@notechange.local>/);assert.match(e,/Content-Type: image\/png/);
});
test('malformed image data warns instead of silently breaking Word',()=>{
 const m=normalize({text:'Title',html:'<h1>Title</h1><p><img src="data:image/png;base64,AAAA" alt="bad"></p>'});assert.equal(m.warnings.length,1);assert.doesNotMatch(toHTML(format(m,{})),/<img/);
});
