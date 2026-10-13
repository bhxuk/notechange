const sanitize = require('sanitize-html');
const { parseHTML } = require('linkedom');
const {imageSize}=require('image-size');
const LIMIT = 12 * 1024 * 1024;
const escape = s => String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function plainHTML(text) { return String(text).split(/\r?\n/).map(s=>`<p>${escape(s) || '<br>'}</p>`).join(''); }
function safeHTML(html) {
 return sanitize(html, { allowedTags:['p','div','br','h1','h2','h3','h4','h5','h6','strong','b','em','i','u','s','strike','span','ul','ol','li','a','table','thead','tbody','tr','th','td','blockquote','img','input'], allowedAttributes:{span:['style'],p:['style'],div:['style'],a:['href'],img:['src','alt'],input:['type','checked'],td:['colspan','rowspan'],th:['colspan','rowspan']},allowedStyles:{'*':{'font-weight':[/^(bold|[6-9]00)$/],'font-style':[/^italic$/],'text-decoration':[/^(underline|line-through)$/]}},allowedSchemes:['https','http','mailto'],allowedSchemesByTag:{img:['data']}, allowProtocolRelative:false,transformTags:{input:(tag,attr)=>({tagName:'span',attribs:{},text:attr.type==='checkbox'?(Object.hasOwn(attr,'checked')?'☑ ':'☐ '):''})} });
}
function normalize(payload={}) {
 if(typeof payload.text!=='string' || (payload.html && typeof payload.html!=='string')) throw Error('Please choose or paste a note first.');
 if(payload.text.length+(payload.html||'').length>LIMIT) throw Error('This note is too large. Please use a smaller note (up to 12 MB).');
 const warnings=[...(Array.isArray(payload.warnings)?payload.warnings:[])].filter(x=>typeof x==='string');
 const source=payload.html||plainHTML(payload.text);
 const {document}=parseHTML(`<html><body>${safeHTML(source)}</body></html>`);
 document.querySelectorAll('img').forEach(img=>{if(!/^data:image\/(png|jpeg);base64,[a-z\d+/=\s]+$/i.test(img.getAttribute('src')||'')) { warnings.push('A picture could not be transferred. Copy the note again, including the picture, or save it separately.'); img.replaceWith(document.createTextNode(`[Picture: ${img.getAttribute('alt')||'not transferred'}]`)); }else {try{const bytes=Buffer.from(img.getAttribute('src').split(',')[1],'base64');const size=imageSize(bytes);if(!['png','jpg'].includes(size.type)||!size.width||!size.height)throw Error();}catch{warnings.push('A picture is damaged or unsupported. Please save it separately from Apple Notes.');img.replaceWith(document.createTextNode('[Picture: not transferred]'));}} });
 const blocks=[];let listGroup=0;
 function runs(node,style={}) {
  if(node.nodeType===3)return [{text:node.textContent,...style}];
  if(node.nodeType!==1)return [];
  const tag=node.tagName.toLowerCase();
  if(tag==='br')return [{text:'\n',...style}];
  if(tag==='img')return [{image:node.getAttribute('src'),alt:node.getAttribute('alt')||'Picture'}];
  const next={...style};if(['b','strong'].includes(tag))next.bold=true;if(['em','i'].includes(tag))next.italics=true;if(tag==='u')next.underline=true;if(['s','strike'].includes(tag))next.strike=true;if(tag==='a')next.link=node.getAttribute('href');const css=node.getAttribute('style')||'';if(/font-weight:\s*(bold|[6-9]00)/i.test(css))next.bold=true;if(/font-style:\s*italic/i.test(css))next.italics=true;if(/text-decoration:\s*underline/i.test(css))next.underline=true;if(/text-decoration:\s*line-through/i.test(css))next.strike=true;
  return [...node.childNodes].flatMap(n=>runs(n,next));
 }
 function walk(parent,level=0) {
  for(const node of parent.childNodes){
   if(node.nodeType===3){if(node.textContent.trim())blocks.push({type:'paragraph',runs:runs(node)});continue;}
   if(node.nodeType!==1)continue;
   const tag=node.tagName.toLowerCase();
   if(['ul','ol'].includes(tag)){let number=1;const group=listGroup++;for(const li of node.children){if(li.tagName!=='LI')continue; const copy=li.cloneNode(true);copy.querySelectorAll('ul,ol').forEach(n=>n.remove());blocks.push({type:'list',ordered:tag==='ol',group,number:number++,level,runs:runs(copy)});for(const list of li.children)if(['UL','OL'].includes(list.tagName))walk({childNodes:[list]},Math.min(level+1,8));} }
   else if(tag==='table'){const rows=[...node.querySelectorAll('tr')].map(tr=>[...tr.children].filter(c=>['TD','TH'].includes(c.tagName)).map(c=>({runs:runs(c),header:c.tagName==='TH'})));if(rows.length){blocks.push({type:'table',rows});if(node.querySelector('[colspan],[rowspan]'))warnings.push('Merged table cells are simplified. Please check the table preview.');}}
   else if(tag==='div' && [...node.children].some(c=>['P','DIV','UL','OL','TABLE','H1','H2','H3'].includes(c.tagName)))walk(node,level);
   else blocks.push({type:/^h[1-6]$/.test(tag)?'heading':'paragraph',level:/^h[1-6]$/.test(tag)?Number(tag[1]):0,runs:runs(node)});
  }
 }
 walk(document.body);
 if(!blocks.length || !blocks.some(b=>b.type==='table'||b.runs.some(r=>r.image||r.text?.trim())))throw Error('The note is empty. Select a note or paste its contents.');
 const first=blocks.find(b=>b.type!=='table'&&b.runs.some(r=>r.text?.trim()));
 const title=first?first.runs.map(r=>r.text||'').join(''):'';
 return {html:document.body.innerHTML,blocks,title,warnings:[...new Set(warnings)]};
}
function format(model,options={}) {
 const blocks=structuredClone(model.blocks);let title='';
 if(options.titleMode!=='none'){
  const i=blocks.findIndex(b=>b.type!=='table'&&b.runs.some(r=>r.text?.trim()));
  if(options.titleMode==='custom')title=String(options.customTitle||'');
  else if(i>=0){title=blocks[i].runs.map(r=>r.text||'').join('');const images=blocks[i].runs.filter(r=>r.image);if(images.length)blocks[i]={type:'paragraph',runs:images};else blocks.splice(i,1);}
 }
 const text=[title,...blocks.filter(b=>b.runs).map(b=>b.runs.map(r=>r.text||'').join(''))].join('\n');
 let layout=['document','recipe','chapter'].includes(options.layout)?options.layout:'auto';
 if(layout==='auto')layout=/^\s*(ingredients|method|instructions)\s*:?\s*$/im.test(text)?'recipe':/^\s*(chapter\s+\S+|prologue|epilogue)\b/im.test(text)?'chapter':'document';
 if(layout==='recipe')for(const b of blocks){if(b.type==='paragraph'&&/^(ingredients|method|instructions|directions|preparation|notes|serves|servings)\s*:?$/i.test(b.runs.map(r=>r.text||'').join('').trim())){b.type='heading';b.level=2;}}
 return {title,blocks,warnings:model.warnings||[],size:options.largeText?14:12,layout,font:layout==='chapter'?'Georgia':'Calibri'};
}
function line(b,markdown=false){
 if(b.type==='table')return b.rows.map(row=>row.map(c=>c.runs.map(r=>r.text||r.alt||'').join('')).join('\t')).join('\n');
 const text=b.runs.map(r=>r.image?`[Picture: ${r.alt}]`:markdown?(r.link?`[${r.text}](${r.link})`:r.bold?`**${r.text}**`:r.italics?`*${r.text}*`:r.text):r.text).join('');
 return (b.type==='list'?`${'  '.repeat(b.level)}${b.ordered?`${b.number}.`:'-'} `:markdown&&b.type==='heading'?'#'.repeat(b.level)+' ':'')+text;
}
function toText(doc){return [doc.title,...doc.blocks.map(b=>line(b))].filter((x,i)=>i>0||x!=='').join('\n\n');}
function toMarkdown(doc){const warnings=[];if(doc.blocks.some(b=>b.type==='table'))warnings.push('Tables use tab-separated text in Markdown.');if(doc.blocks.some(b=>b.runs?.some(r=>r.image)))warnings.push('Markdown includes picture descriptions; use Word to keep pictures.');return {text:[doc.title?`# ${doc.title}`:'',...doc.blocks.map(b=>line(b,true))].filter((x,i)=>i>0||x!=='').join('\n\n'),warnings};}
function toHTML(doc){
 function inline(rs){return rs.map(r=>{if(r.image)return `<img src="${escape(r.image)}" alt="${escape(r.alt)}">`;let t=escape(r.text||'').replace(/\n/g,'<br>');if(r.bold)t=`<strong>${t}</strong>`;if(r.italics)t=`<em>${t}</em>`;if(r.underline)t=`<u>${t}</u>`;if(r.strike)t=`<s>${t}</s>`;if(r.link)t=`<a href="${escape(r.link)}">${t}</a>`;return t;}).join('');}
 return (doc.title?`<h1>${escape(doc.title)}</h1>`:'')+doc.blocks.map(b=>b.type==='table'?`<table>${b.rows.map(row=>`<tr>${row.map(c=>`<${c.header?'th':'td'}>${inline(c.runs)}</${c.header?'th':'td'}>`).join('')}</tr>`).join('')}</table>`:b.type==='list'?`<p>${'&nbsp;&nbsp;'.repeat(b.level)}${b.ordered?b.number+'.':'•'} ${inline(b.runs)}</p>`:`<${b.type==='heading'?'h'+b.level:'p'}>${inline(b.runs)}</${b.type==='heading'?'h'+b.level:'p'}>`).join('');
}
function collection(sources,options={}){
 if(!Array.isArray(sources)||!sources.length||sources.length>100)throw Error('Choose between 1 and 100 notes.');
 return {documents:sources.map(source=>format(normalize(source),options))};
}
function collectionHTML(collection){return collection.documents.map((d,i)=>`<article class="document layout-${d.layout}${i?' page-break':''}">${toHTML(d)}</article>`).join('');}
module.exports={normalize,format,toText,toMarkdown,toHTML,escape,collection,collectionHTML};
