// Normalize one Godot export from the unified project; never read an old project/PCK.
// Keep host-loaded text resources readable (custom GDScript depends on the host game's globals).
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {readPack} from './pck-reader.mjs';
const root=path.resolve(import.meta.dirname,'..');
const file=process.argv[2]; assert(file,'PCK path required');
const original=fs.readFileSync(file), files=readPack(file);
const experimental=process.argv.includes('--include-caocao');
const isExperimental=name=>name.startsWith('Experimental/')||name.includes('weicaocao');
function walk(dir) {
  return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=> e.name==='.godot'||e.name==='src'?[]:e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);
}
let preserved=0;
for(const source of walk(path.join(root,'mod'))) {
  const relative=path.relative(path.join(root,'mod'),source).replaceAll('\\','/');
  if(!experimental&&isExperimental(relative)) continue;
  const body=fs.readFileSync(source);
  const textResource=/\.(gd|gdshader|tscn|tres)$/.test(source) && (source.endsWith('.gd')||source.endsWith('.gdshader')||body.toString('utf8').replace(/^\uFEFF/,'').trimStart().startsWith('['));
  const oldRawTexture=relative.startsWith('WeiDongZhuo/')&&source.endsWith('.png')||relative==='Yjcm/Images/events/qu_yuan_tou_jiang.png';
  if(!textResource&&!oldRawTexture) continue;
  files.set(relative,body); preserved++;
  // The former raw DongZhuo resources are resolved against the host. Keep existing
  // common-resource remaps (and their compiled assets) unchanged.
  if(textResource && /WeiDongZhuo|weidongzhuo|wei_dong_zhuo/.test(relative)) files.delete(relative+'.remap');
  // Orb fallback scenes are parsed by the host game, which has the Spine class and
  // its resources. The standalone mod export cannot compile them, so drop the .remap
  // and its broken .scn and let the host load the original text scene.
  if(textResource && relative.startsWith('scenes/orbs/orb_visuals/')) {
    const remap=files.get(relative+'.remap');
    if(remap) {
      const target=remap.toString('utf8').match(/path="res:\/\/([^"\r\n]+)"/)?.[1];
      if(target?.startsWith('.godot/')) files.delete(target);
    }
    files.delete(relative+'.remap');
  }
}
if(!experimental){
  for(const name of [...files.keys()]) if(isExperimental(name)) {
    if(name.endsWith('.remap')||name.endsWith('.import')){
      const content=files.get(name).toString('utf8');
      for(const match of content.matchAll(/path="res:\/\/([^"\r\n]+)"/g)) if(match[1].startsWith('.godot/')) files.delete(match[1]);
    }
    files.delete(name);
  }
}else{
  const directory=path.join(root,'mod/Experimental/WeiCaoCao/localization');
  for(const table of fs.readdirSync(directory).filter(n=>n.endsWith('.json')).sort()){
    const effective=table.startsWith('wei-')?table.match(/-(cards|powers|enchantments)\.json$/)?.[1]+'.json':table;
    if(effective==='undefined.json')throw new Error('Unknown supplemental Cao Cao table: '+table);
    const key='Yjcm/localization/zhs/'+effective;
    const common=JSON.parse(files.get(key)?.toString('utf8')||'{}');
    Object.assign(common,JSON.parse(fs.readFileSync(path.join(directory,table),'utf8')));
    files.set(key,Buffer.from(JSON.stringify(common,null,2)+'\n'));
  }
}
const base=Number(original.readBigUInt64LE(24));
const header=Buffer.from(original.subarray(0,base));
const chunks=[header], records=[]; let cursor=base;
for(const [name,body] of [...files].sort(([a],[b])=>a.localeCompare(b,'en'))) {
  const encoded=Buffer.from(name), padded=Math.ceil(encoded.length/4)*4;
  const record=Buffer.alloc(4+padded+36); record.writeUInt32LE(padded); encoded.copy(record,4);
  record.writeBigUInt64LE(BigInt(cursor-base),4+padded); record.writeBigUInt64LE(BigInt(body.length),12+padded);
  crypto.createHash('md5').update(body).digest().copy(record,20+padded); records.push(record);
  chunks.push(body); cursor+=body.length;
  const padding=(16-cursor%16)%16; if(padding){chunks.push(Buffer.alloc(padding));cursor+=padding;}
}
header.writeBigUInt64LE(BigInt(cursor),32);
const count=Buffer.alloc(4); count.writeUInt32LE(files.size); chunks.push(count,...records);
fs.writeFileSync(file,Buffer.concat(chunks));
console.log(`UNIFIED_PACK_NORMALIZED entries=${files.size} rawResources=${preserved}`);
