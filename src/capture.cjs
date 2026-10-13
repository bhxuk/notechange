// Runs in an Apple page only, after the user presses Use this note.
// Read-only: never click, type into, or change Apple Notes.
module.exports = async function capture(){
 const visible=el=>{const r=el.getBoundingClientRect();return r.width>80&&r.height>40&&getComputedStyle(el).visibility!=='hidden';};
 const candidates=[...document.querySelectorAll('[contenteditable]:not([contenteditable="false"]), [role="textbox"][aria-multiline="true"]')].filter(visible).filter(el=>!el.closest('[role="toolbar"]'));
 const top=candidates.filter(el=>!candidates.some(other=>other!==el&&other.contains(el)));
 if(top.length!==1)return {error:'Open a note first. If it cannot be read, copy the note and use Paste my note.'};
 const source=top[0];const clone=source.cloneNode(true);const warnings=[];
 const imgs=[...clone.querySelectorAll('img')];
 for(let i=0;i<imgs.length;i++){
  const img=imgs[i];const src=source.querySelectorAll('img')[i]?.src;
  try{if(!src||(!src.startsWith('blob:')&&!src.startsWith('data:')&&new URL(src,location.href).origin!==location.origin))throw Error('Unsupported picture');const response=await fetch(src,{signal:AbortSignal.timeout(8000)});const blob=await response.blob();if(blob.size>3*1024*1024||!['image/png','image/jpeg'].includes(blob.type))throw Error('Picture size or format');const value=await new Promise((resolve,reject)=>{const r=new FileReader();r.onload=()=>resolve(r.result);r.onerror=reject;r.readAsDataURL(blob);});img.setAttribute('src',value);}catch{img.removeAttribute('src');warnings.push('A picture could not be transferred. Check the preview before saving.');}
 }
 if(source.querySelector('video,audio,canvas,object,embed')||source.querySelector('a[download]'))warnings.push('This note may contain an attachment that cannot be transferred. Save the attachment from Apple Notes separately.');
 return {html:clone.innerHTML,text:source.innerText,warnings};
};
