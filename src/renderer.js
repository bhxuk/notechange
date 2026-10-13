const $=id=>document.getElementById(id);const api=window.notechange;let source=null,revision=0;const outputIDs=['word','email','copy','txt','md','eml'];
const options=()=>({titleMode:$('titleMode').value,customTitle:$('customTitle').value,largeText:$('largeText').checked});
function status(text){$('status').textContent=text;}function enable(value){outputIDs.forEach(id=>$(id).disabled=!value);}
async function call(fn){try{const result=await fn();if(!result.ok)throw Error(result.error);return result.value;}catch(e){status(e.message);return undefined;}}
async function preview(){if(!source)return;const rev=++revision;enable(false);const value=await call(()=>api.preview(source,options()));if(rev!==revision||!value)return;$('preview').innerHTML=value.html;$('preview').classList.toggle('large',$('largeText').checked);$('warnings').textContent=value.warnings.join(' ');$('warnings').hidden=!value.warnings.length;enable(true);status(value.warnings.length?'Please check the highlighted message before saving.':'Your preview is ready. Choose Save Word document or an email option.');}
async function receive(value){if(!value)return;source=value;$('input').value=value.text;await preview();}
$('connect').onclick=()=>call(()=>api.connect());$('paste').onclick=async()=>receive(await call(()=>api.paste()));$('useText').onclick=()=>receive({text:$('input').value});
['titleMode','customTitle','largeText'].forEach(id=>$(id).addEventListener('change',()=>{$('customTitle').hidden=$('titleMode').value!=='custom';preview();}));
for(const [id,kind] of [['word','docx'],['txt','txt'],['md','md'],['eml','eml']])$(id).onclick=async()=>{const path=await call(()=>api.save(kind,source,options()));if(path)status('Saved successfully: '+path);};
$('email').onclick=async()=>{if(await call(()=>api.email(source,options())))status('Your email app is opening. Check the message and add a recipient.');};
$('copy').onclick=async()=>{if(await call(()=>api.copy(source,options())))status('Copied with formatting. Open a new email and paste into the message. Leave the subject empty.');};
$('help').onclick=()=>$('helpDialog').showModal();$('closeHelp').onclick=()=>$('helpDialog').close();$('updates').onclick=()=>call(()=>api.updates());$('install').onclick=()=>call(()=>api.installUpdate());$('signOut').onclick=async()=>{if(confirm('Sign out of Apple on this computer?')){if(await call(()=>api.signOut()))status('Signed out. Your original Apple Notes have not been changed.');}};
api.onNote(receive);api.onStatus(text=>{status(text);if(text.includes('Your update is ready'))$('install').hidden=false;});
