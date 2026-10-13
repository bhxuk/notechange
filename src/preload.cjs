const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('notechange',{
 paste:()=>ipcRenderer.invoke('paste'),connect:()=>ipcRenderer.invoke('connect'),capture:()=>ipcRenderer.invoke('capture'),preview:(source,options)=>ipcRenderer.invoke('preview',source,options),save:(kind,source,options)=>ipcRenderer.invoke('save',kind,source,options),email:(source,options)=>ipcRenderer.invoke('email',source,options),copy:(source,options)=>ipcRenderer.invoke('copy',source,options),updates:()=>ipcRenderer.invoke('updates'),installUpdate:()=>ipcRenderer.invoke('install-update'),signOut:()=>ipcRenderer.invoke('sign-out'),onNote:fn=>ipcRenderer.on('note',(_,data)=>fn(data)),onStatus:fn=>ipcRenderer.on('status',(_,data)=>fn(data))
});
