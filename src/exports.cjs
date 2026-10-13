const {imageSize}=require('image-size');
const { Document, Packer, Paragraph, TextRun, ImageRun, ExternalHyperlink, Table, TableRow, TableCell, HeadingLevel, WidthType }=require('docx');
const {toHTML,toText,toMarkdown}=require('./content.cjs');
function textRuns(runs,size,font='Calibri'){return runs.map(r=>{
 if(r.image){const match=r.image.match(/^data:image\/(png|jpeg);base64,(.+)$/s);const data=Buffer.from(match[2],'base64');const {width,height}=imageSize(data);const scale=Math.min(1,480/width,640/height);return new ImageRun({data,type:match[1]==='jpeg'?'jpg':'png',transformation:{width:Math.round(width*scale),height:Math.round(height*scale)},altText:{title:r.alt,description:r.alt,name:r.alt}});}
 const parts=String(r.text||'').split('\n');const children=parts.map((text,i)=>new TextRun({text,font,break:i?1:0,bold:r.bold,italics:r.italics,underline:r.underline?{}:undefined,strike:r.strike,size:size*2}));return r.link?new ExternalHyperlink({link:r.link,children}):children;
 }).flat();}
async function word(input){
 const docs=input.documents||[input];const children=[];for(const [index,doc] of docs.entries()){if(index)children.push(new Paragraph({text:'',pageBreakBefore:true}));if(doc.title)children.push(new Paragraph({text:doc.title,heading:HeadingLevel.TITLE,spacing:{after:240},alignment:doc.layout==='chapter'?'center':'left'}));
 for(const b of doc.blocks){if(b.type==='table'){children.push(new Table({width:{size:100,type:WidthType.PERCENTAGE},rows:b.rows.map(row=>new TableRow({children:row.map(c=>new TableCell({children:[new Paragraph({children:textRuns(c.runs,doc.size,doc.font)})]}))}))}));}else children.push(new Paragraph({children:textRuns(b.runs,doc.size,doc.font),heading:b.type==='heading'?HeadingLevel['HEADING_'+b.level]:undefined,bullet:b.type==='list'&&!b.ordered?{level:b.level}:undefined,numbering:b.type==='list'&&b.ordered?{reference:'ordered',level:b.level,instance:index*100+(b.group||0)}:undefined,spacing:{after:doc.layout==='recipe'?120:180,line:doc.layout==='chapter'?360:300},indent:doc.layout==='chapter'&&b.type==='paragraph'?{firstLine:300}:undefined}));}
 }
 return Packer.toBuffer(new Document({styles:{default:{document:{run:{font:docs[0].font||'Calibri',size:docs[0].size*2}}}},numbering:{config:[{reference:'ordered',levels:Array.from({length:9},(_,i)=>({level:i,format:'decimal',text:`%${i+1}.`,alignment:'left',style:{paragraph:{indent:{left:360*(i+1),hanging:260}}}}))}]},sections:[{children}]}));
}
function email(doc){
 const alternative='notechange-alternative',related='notechange-related';const images=[];
 const html=`<!doctype html><html><body>${toHTML(doc).replace(/src="(data:image\/(png|jpeg);base64,([^\"]+))"/g,(_,data,type,base64)=>{const cid=`picture-${images.length}@notechange.local`;images.push({cid,type,base64});return `src="cid:${cid}"`;})}</body></html>`;
 const wrap=s=>s.match(/.{1,76}/g)?.join('\r\n')||'';const b64=s=>wrap(Buffer.from(s,'utf8').toString('base64'));
 const lines=['X-Unsent: 1','Subject: ','MIME-Version: 1.0',`Content-Type: multipart/related; boundary="${related}"`,'',`--${related}`,`Content-Type: multipart/alternative; boundary="${alternative}"`,'',`--${alternative}`,'Content-Type: text/plain; charset=UTF-8','Content-Transfer-Encoding: base64','',b64(toText(doc)),`--${alternative}`,'Content-Type: text/html; charset=UTF-8','Content-Transfer-Encoding: base64','',b64(html),`--${alternative}--`];
 for(const image of images)lines.push(`--${related}`,`Content-Type: image/${image.type}`,`Content-ID: <${image.cid}>`,'Content-Disposition: inline','Content-Transfer-Encoding: base64','',wrap(image.base64));
 lines.push(`--${related}--`,'');return lines.join('\r\n');
}
module.exports={word,email,toText,toMarkdown,toHTML};
