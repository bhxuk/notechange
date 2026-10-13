// Read-only adapter, run only on iCloud Notes frames after an explicit capture.
module.exports=async function capture(){
 const visible=el=>{const r=el.getBoundingClientRect(),s=getComputedStyle(el);return r.width>60&&r.height>25&&s.visibility!=='hidden'&&s.display!=='none';};
 const roots=[document];for(let i=0;i<roots.length&&i<30;i++)for(const el of roots[i].querySelectorAll('*'))if(el.shadowRoot)roots.push(el.shadowRoot);
 const candidates=roots.flatMap(root=>[...root.querySelectorAll('[contenteditable]:not([contenteditable="false"]),[role="textbox"][aria-multiline="true"]')]).filter(visible).filter(el=>!el.closest('[role="toolbar"],[role="search"],form'));
 const top=candidates.filter(el=>!candidates.some(other=>other!==el&&other.contains(el)));
 if(!top.length)return {error:'Select and open a note first. If it is locked, unlock it in Apple Notes.'};
 // Some editors split title and body. Accept only an identifiable title/body pair.
 let selected=top;
 if(top.length>1){const title=top.filter(el=>/title/i.test((el.getAttribute('aria-label')||'')+' '+(el.getAttribute('data-placeholder')||'')+' '+el.className));const body=top.filter(el=>!title.includes(el));if(title.length!==1||body.length!==1)return {error:'The selected note could not be identified safely. Copy it and use Paste note.'};selected=[title[0],body[0]];}
 const container=document.createElement('div');const warnings=[];
 for(const source of selected){const clone=source.cloneNode(true);
  const originals=[...source.querySelectorAll('*')],copies=[...clone.querySelectorAll('*')];
  for(let i=0;i<copies.length;i++){const orig=originals[i],copy=copies[i];const css=getComputedStyle(orig);const parts=[];if(Number(css.fontWeight)>=600||css.fontWeight==='bold')parts.push('font-weight:bold');if(css.fontStyle==='italic')parts.push('font-style:italic');if(css.textDecorationLine?.includes('underline'))parts.push('text-decoration:underline');if(parts.length)copy.setAttribute('style',parts.join(';'));
   if(orig.getAttribute('role')==='checkbox'){const marker=document.createTextNode(orig.getAttribute('aria-checked')==='true'?'☑ ':'☐ ');copy.insertBefore(marker,copy.firstChild);}
  }
  const imgs=[...clone.querySelectorAll('img')];
  for(let i=0;i<imgs.length;i++){const img=imgs[i],src=source.querySelectorAll('img')[i]?.src;try{const u=new URL(src,location.href);if(!src||!['blob:','data:'].includes(u.protocol)&&!(u.protocol==='https:'&&(u.hostname===location.hostname||u.hostname.endsWith('.icloud.com')||u.hostname.endsWith('.icloud-content.com'))))throw Error();const response=await fetch(src,{signal:AbortSignal.timeout(8000)});if(!response.ok)throw Error();const blob=await response.blob();if(blob.size>3*1024*1024||!['image/png','image/jpeg'].includes(blob.type))throw Error();const value=await new Promise((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(reader.result);reader.onerror=reject;reader.readAsDataURL(blob);});img.setAttribute('src',value);}catch{img.removeAttribute('src');warnings.push('A picture could not be transferred. Please check the preview.');}}
  if(source.querySelector('video,audio,canvas,object,embed,a[download]'))warnings.push('An attachment may need to be saved separately in Apple Notes.');
  const section=document.createElement(selected.length>1&&source===selected[0]?'h1':'div');section.innerHTML=clone.innerHTML;container.appendChild(section);
 }
 return {html:container.innerHTML,text:selected.map(el=>el.innerText||el.textContent).join('\n'),warnings};
};
