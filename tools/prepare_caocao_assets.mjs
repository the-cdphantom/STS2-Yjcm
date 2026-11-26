// Derive runtime copies from the supplied assets. Original files are never changed.
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {createRequire} from 'node:module';
import {merge,rect,pose,slotGeometry} from './preview/geometry.mjs';
const root=path.resolve(import.meta.dirname,'..'),workspace=process.argv[2]||path.dirname(root);
const require=createRequire(path.join(process.env.WDZ_NODE_MODULES||path.join(root,'node_modules'),'_caocao.cjs'));
const spine=require('@esotericsoftware/spine-core');
const converter=path.join(workspace,'_spine_conversion_tools/SpineSkeletonDataConverter.exe');
const source=path.join(workspace,'素材/图片处理后/威曹操/人物');
const out=path.join(root,'assets/experimental/WeiCaoCao/Spine/WeiCaoCao');
const reports=path.join(root,'artifacts/reports/weicaocao');fs.mkdirSync(reports,{recursive:true});
function convert(input,target){const p=spawnSync(converter,[input,target,'-v','4.2.43'],{encoding:'utf8',windowsHide:true});assert.equal(p.status,0,p.stderr||p.stdout);}
const json={};
for(const [folder,number] of [['Character',1],['Background',2]]){
 const original=`zhujiemian_weicaocao${number}`,target=path.join(out,folder);fs.mkdirSync(target,{recursive:true});
 const file=path.join(reports,folder+'.json');convert(path.join(source,original+'.skel'),file);
 json[folder]=JSON.parse(fs.readFileSync(file,'utf8'));
 const base=folder==='Character'?'character':'background';
 convert(file,path.join(target,base+'.skel'));
 const atlasText=fs.readFileSync(path.join(source,original+'.atlas'),'utf8');fs.writeFileSync(path.join(target,base+'.atlas'),atlasText);
 const atlas=new spine.TextureAtlas(atlasText);
 for(const page of atlas.pages){assert(!page.name.includes('..')&&!path.isAbsolute(page.name));fs.copyFileSync(path.join(source,page.name),path.join(target,page.name));}
}
const selection=structuredClone(json.Character),bones=new Map(selection.bones.map(b=>[b.name,b]));
function body(name){for(let b=bones.get(name);b;b=bones.get(b.parent))if(b.name==='suofang')return true;return false;}
function hideSlots(model,names){
 for(const slot of model.slots)if(names.includes(slot.name))delete slot.attachment;
 for(const skin of model.skins)for(const name of names)delete skin.attachments[name];
 for(const animation of Object.values(model.animations))for(const name of names){
  if(animation.slots)delete animation.slots[name];
  for(const skins of [animation.attachments,animation.deform])
   for(const slots of Object.values(skins||{}))delete slots[name];
 }
}
const hiddenSelection=selection.slots.filter(s=>!body(s.bone)).map(s=>s.name);
hideSlots(selection,hiddenSelection);
// Godot's multiply blend renders the transparent edges of this hand shadow black.
// Normal alpha blending preserves the shadow and the hand artwork beneath it.
const handShadow=selection.slots.find(slot=>slot.name==='ren89');
assert.equal(handShadow?.blend,'multiply','Unexpected source hand-shadow blend mode');
delete handShadow.blend;
// Lobby framing is already calibrated. Keep that derivative separate from combat.
const selectionFile=path.join(reports,'Selection.json');fs.writeFileSync(selectionFile,JSON.stringify(selection));
fs.mkdirSync(path.join(out,'Selection'),{recursive:true});convert(selectionFile,path.join(out,'Selection/character.skel'));
const combat=structuredClone(selection);
// ren1..ren17 are independent foreground weapons, shield and their hanging ribbons.
// Remove attachments, switch timelines and deforms so neither clip can restore them.
const foregroundSlots=Array.from({length:17},(_,i)=>`ren${i+1}`);
hideSlots(combat,foregroundSlots);
const combatRotationDegrees=24.14;
combat.bones.find(b=>b.name==='root').rotation=combatRotationDegrees;
const hidden=[...hiddenSelection,...foregroundSlots];
const combatFile=path.join(reports,'Combat.json');fs.writeFileSync(combatFile,JSON.stringify(combat));
fs.mkdirSync(path.join(out,'Combat'),{recursive:true});convert(combatFile,path.join(out,'Combat/character.skel'));
const report={version:'4.2.43',animation:'play',idleAnimation:'play',clickAnimation:'play2',referenceAnimation:'play2',combatRotationDegrees,foregroundCombatSlots:foregroundSlots,hiddenCombatSlots:hidden,hiddenSelectionSlots:hiddenSelection,assets:{}};
for(const folder of ['Background','Character','Selection','Combat']){
 const base=folder==='Background'?'background':'character';
 const atlas=new spine.TextureAtlas(fs.readFileSync(path.join(out,['Combat','Selection'].includes(folder)?'Character':folder,base+'.atlas'),'utf8'));
 for(const page of atlas.pages)page.setTexture(new spine.FakeTexture({width:page.width,height:page.height}));
 const data=new spine.SkeletonBinary(new spine.AtlasAttachmentLoader(atlas)).readSkeletonData(fs.readFileSync(path.join(out,folder,base+'.skel')));
 assert.equal(data.version,'4.2.43');
 assert(data.findAnimation('play')&&data.findAnimation('play2'),'Missing idle/click animation');
 // Keep the established reference bounds stable when changing the played clip.
 const animation=data.findAnimation(report.referenceAnimation);
 const skeleton=new spine.Skeleton(data);let bounds=null,focus=null;
 for(let i=0;i<=60;i++){pose(spine,skeleton,animation,animation.duration*i/60);
  for(const slot of skeleton.slots){const g=slotGeometry(spine,slot);if(!g||g.alpha<=0.001)continue;
   bounds=merge(bounds,g.bounds);
   // Fit the actual backdrop plane, excluding protruding foreground columns.
   if(folder==='Background'?slot.data.name==='bj48':/^ren/.test(slot.data.name))focus=merge(focus,g.bounds);
  }
 }
 assert(focus,'No visible composition');report.assets[folder]={base,animations:data.animations.map(a=>({name:a.name,duration:a.duration})),bounds:rect(bounds),focus:rect(focus),pages:atlas.pages.map(p=>p.name)};
}
fs.writeFileSync(path.join(out,'character.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report,null,2));
