const assert=require('node:assert/strict');const {normalize,format,collection}=require('../src/content.cjs');const {word}=require('../src/exports.cjs');
module.exports=async({main,pdf})=>{
 const wc=main.webContents;
 const result=await wc.executeJavaScript(`(async()=>{if(!window.notechange)throw Error('Preload unavailable');const info=await window.notechange.info();if(!info.ok)throw Error(info.error);document.getElementById('demoRecipe').click();for(let i=0;i<60&&document.getElementById('word').disabled;i++)await new Promise(r=>setTimeout(r,50));return {version:info.value.version,title:document.getElementById('preview').innerText,wordEnabled:!document.getElementById('word').disabled,layout:document.getElementById('layoutLabel').innerText};})()`);
 assert.equal(result.wordEnabled,true);assert.match(result.title,/Lemon drizzle cake/);assert.match(result.title,/200 g butter/);assert.equal(result.layout,'Recipe layout');
 const chapter=await wc.executeJavaScript(`(async()=>{document.getElementById('layout').value='chapter';document.getElementById('layout').dispatchEvent(new Event('change'));for(let i=0;i<60&&document.getElementById('word').disabled;i++)await new Promise(r=>setTimeout(r,50));return document.getElementById('layoutLabel').innerText;})()`);assert.equal(chapter,'Chapter layout');
 const set=collection([{text:'Chapter One\nOriginal first chapter.'},{text:'Chapter Two\nOriginal second chapter.'}],{layout:'chapter'});const docx=await word(set);assert.equal(docx.subarray(0,2).toString(),'PK');const output=await pdf(set);assert.equal(output.subarray(0,5).toString(),'%PDF-');
 console.log('Windows Electron smoke passed: preload, IPC, recipe preview, chapter layout, Word and two-note PDF.');
};
