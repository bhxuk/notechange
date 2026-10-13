const path=require('node:path');
function appleURL(value){try{const u=new URL(value);return u.protocol==='https:'&&['icloud.com','apple.com'].some(d=>u.hostname===d||u.hostname.endsWith('.'+d));}catch{return false;}}
function notesURL(value){try{const u=new URL(value);return u.protocol==='https:'&&(u.hostname==='icloud.com'||u.hostname.endsWith('.icloud.com'))&&(u.pathname.includes('notes')||u.hostname.startsWith('notes.'));}catch{return false;}}
function safeName(value){let name=String(value||'My note').replace(/[<>:"/\\|?*\x00-\x1f]/g,'_').replace(/[. ]+$/g,'').slice(0,100)||'My note';if(/^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(name))name='_'+name;return name;}
function validateProject(value){if(!value||value.type!=='notechange-project'||value.version!==1||!Array.isArray(value.notes)||value.notes.length>100)throw Error('This is not a supported NoteChange project.');return value.notes;}
module.exports={appleURL,notesURL,safeName,validateProject};
