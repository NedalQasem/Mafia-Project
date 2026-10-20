// prints which Arabic presentation forms a font's cmap has (Tools/ui-gen/fontdiag.js <font.ttf>)
const fs=require("fs");const b=fs.readFileSync(process.argv[2]);
const u16=o=>b.readUInt16BE(o),u32=o=>b.readUInt32BE(o),i16=o=>b.readInt16BE(o);
const n=u16(4),T={};for(let i=0;i<n;i++){const o=12+i*16;T[b.toString("latin1",o,o+4)]={off:u32(o+8),len:u32(o+12)};}
const base=T.cmap.off,cn=u16(base+2);let sub=null;
for(let i=0;i<cn;i++){const p=u16(base+4+i*8),e=u16(base+6+i*8),off=base+u32(base+8+i*8);if(u16(off)===4&&(p===3&&e===1||p===0)){sub=off;break;}}
const m=new Set();const o=sub,sx=u16(o+6),sg=sx/2,eo=o+14,so=eo+sx+2,dO=so+sx,rO=dO+sx;
for(let s=0;s<sg;s++){const e=u16(eo+s*2),st=u16(so+s*2);for(let c=st;c<=e&&c!==0xFFFF;c++)m.add(c);}
let have=[],miss=[];for(let c=0xFE80;c<=0xFEFC;c++)(m.has(c)?have:miss).push(c.toString(16));
console.log("has",have.length,"missing",miss.length);
console.log("missing:",miss.join(" "));
console.log("arabic letters 0627-064A present:",[...Array(0x24)].map((_,i)=>0x627+i).filter(c=>m.has(c)).length,"of 36");
console.log("tables:",Object.keys(T).join(","));
