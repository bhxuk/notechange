const {app,BrowserWindow,WebContentsView,ipcMain,clipboard,dialog,shell,session}=require('electron');
const path=require('node:path');const fs=require('node:fs/promises');const {pathToFileURL}=require('node:url');
const {normalize,format}=require('./content.cjs');const exportsNote=require('./exports.cjs');const capture=require('./capture.cjs');
let main,appleWindow,appleView,readyUpdate=false;const localURL=pathToFileURL(path.join(__dirname,'index.html')).href;const toolbarURL=pathToFileURL(path.join(__dirname,'apple.html')).href;
function appleURL(url){try{const u=new URL(url);return u.protocol==='https:'&&['icloud.com','apple.com'].some(d=>u.hostname===d||u.hostname.endsWith('.'+d));}catch{return false;}}
function trusted(event){const url=event.senderFrame?.url;if(event.sender!==main?.webContents&&event.sender!==appleWindow?.webContents)throw Error('Untrusted request');if(![localURL,toolbarURL].includes(url))throw Error('Untrusted page');}
function handle(name,fn){ipcMain.handle(name,async(event,...args)=>{try{trusted(event);return {ok:true,value:await fn(...args)};}catch(e){return {ok:false,error:e.message||'Something went wrong. Please try again.'};}});}
function status(text){main?.webContents.send('status',text);}
function safeName(title){return (title||'My note').replace(/[<>:"/\\|?*\x00-\x1f]/g,'_').replace(/[. ]+$/g,'').slice(0,100)||'My note';}
function documentFrom(source,options){return format(normalize(source),options);}
async function selected(){
 if(!appleView)throw Error('Open Apple Notes and select a note first.');
 const frames=appleView.webContents.mainFrame.framesInSubtree;const found=[];
 for(const frame of frames){if(!appleURL(frame.url))continue;try{const value=await frame.executeJavaScript(`(${capture.toString()})()`);if(value&&!value.error&&value.text?.trim())found.push(value);}catch{/* Sign-in frames may be temporarily unavailable. */}}
 if(found.length!==1)throw Error('Open a note in Apple Notes, then try again. You can also copy it and press Paste my note.');
 const value=found[0];normalize(value);main.webContents.send('note',value);main.show();main.focus();return value;
}
function connect(){
 if(appleWindow){appleWindow.show();appleWindow.focus();return;}
 appleWindow=new BrowserWindow({width:1100,height:850,title:'Apple Notes — NoteChange',webPreferences:{preload:path.join(__dirname,'preload.cjs'),contextIsolation:true,nodeIntegration:false,sandbox:true}});
 appleWindow.loadFile(path.join(__dirname,'apple.html'));
 appleView=new WebContentsView({webPreferences:{partition:'persist:apple-notes',contextIsolation:true,nodeIntegration:false,sandbox:true}});appleWindow.contentView.addChildView(appleView);
 function resize(){const [w,h]=appleWindow.getContentSize();appleView.setBounds({x:0,y:112,width:w,height:Math.max(0,h-112)});}resize();appleWindow.on('resize',resize);
 appleView.webContents.setWindowOpenHandler(({url})=>{if(appleURL(url)){appleView.webContents.loadURL(url);}return {action:'deny'};});
 appleView.webContents.on('will-navigate',(event,url)=>{if(!appleURL(url))event.preventDefault();});appleView.webContents.on('will-redirect',(event,url)=>{if(!appleURL(url))event.preventDefault();});
 appleView.webContents.session.setPermissionRequestHandler((wc,permission,callback)=>callback(false));
 appleView.webContents.loadURL('https://www.icloud.com/notes');appleWindow.on('closed',()=>{appleView?.webContents.close();appleView=null;appleWindow=null;});
}
handle('connect',connect);handle('capture',selected);
handle('paste',()=>{const value={text:clipboard.readText(),html:clipboard.readHTML()};normalize(value);return value;});
handle('preview',(source,options)=>{const doc=documentFrom(source,options);return {html:exportsNote.toHTML(doc),text:exportsNote.toText(doc),title:doc.title,warnings:doc.warnings};});
async function save(kind,source,options){
 if(!['docx','txt','md','eml'].includes(kind))throw Error('Choose Word, text, Markdown or email.');const doc=documentFrom(source,options);
 const warnings=[...doc.warnings];if(kind==='txt'&&doc.blocks.some(b=>b.runs?.some(r=>r.image)))warnings.push('Text files include picture descriptions, not pictures.');if(kind==='md')warnings.push(...exportsNote.toMarkdown(doc).warnings);
 if(warnings.length){const result=await dialog.showMessageBox(main,{type:'warning',buttons:['Back to preview','Save anyway'],defaultId:0,cancelId:0,message:'Please check before saving',detail:warnings.join('\n')});if(result.response!==1)return null;}
 const result=await dialog.showSaveDialog(main,{title:'Save your note',defaultPath:path.join(app.getPath('documents'),safeName(doc.title)+'.'+kind),filters:[{name:kind==='docx'?'Word document':kind==='eml'?'Email draft':'Note',extensions:[kind]}]});if(result.canceled)return null;
 const data=kind==='docx'?await exportsNote.word(doc):kind==='eml'?exportsNote.email(doc):kind==='md'?exportsNote.toMarkdown(doc).text:exportsNote.toText(doc);await fs.writeFile(result.filePath,data);return result.filePath;
}
handle('save',save);
handle('copy',(source,options)=>{const doc=documentFrom(source,options);clipboard.write({text:exportsNote.toText(doc),html:exportsNote.toHTML(doc)});return true;});
handle('email',async(source,options)=>{
 const doc=documentFrom(source,options);if(doc.warnings.length)throw Error('This note has transfer warnings. Please check the preview and use Save email draft to continue.');
 if(exportsNote.toText(doc).length>1600)throw Error('This note is too long for a reliable email link. Use Copy for email, then paste into a new email, or save an email draft.');
 await shell.openExternal('mailto:?body='+encodeURIComponent(exportsNote.toText(doc)));return true;
});
handle('sign-out',async()=>{if(appleWindow)appleWindow.close();await session.fromPartition('persist:apple-notes').clearStorageData();await session.fromPartition('persist:apple-notes').clearCache();return true;});
let updater;handle('updates',async()=>{if(!app.isPackaged){status('Updates are available in the installed Windows version.');return;}await updater.checkForUpdates();});handle('install-update',()=>{if(!readyUpdate)throw Error('No downloaded update is ready.');updater.quitAndInstall();});
app.whenReady().then(()=>{
 main=new BrowserWindow({width:1260,height:880,minWidth:900,minHeight:680,title:'NoteChange',backgroundColor:'#f5f4ef',webPreferences:{preload:path.join(__dirname,'preload.cjs'),contextIsolation:true,nodeIntegration:false,sandbox:true}});main.setMenuBarVisibility(false);main.loadFile(path.join(__dirname,'index.html'));main.webContents.setWindowOpenHandler(()=>({action:'deny'}));main.webContents.on('will-navigate',e=>e.preventDefault());
 session.defaultSession.setPermissionRequestHandler((wc,permission,callback)=>callback(false));
 if(app.isPackaged){updater=require('electron-updater').autoUpdater;updater.autoDownload=true;updater.autoInstallOnAppQuit=false;updater.on('update-available',()=>status('An update is downloading. You can keep using NoteChange.'));updater.on('update-not-available',()=>status('NoteChange is up to date.'));updater.on('update-downloaded',()=>{readyUpdate=true;status('Your update is ready. Press Install update when you are finished.');});updater.on('error',()=>status('Could not check for updates. You can keep using this version.'));updater.checkForUpdates().catch(()=>{});}
});
app.on('window-all-closed',()=>app.quit());
