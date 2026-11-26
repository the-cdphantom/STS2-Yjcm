import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {pose,slotGeometry} from '../tools/preview/geometry.mjs';
const root=path.resolve(import.meta.dirname,'..');
const require=createRequire(path.join(process.env.WDZ_NODE_MODULES||path.join(root,'node_modules'),'_caocao_test.cjs'));
const spine=require('@esotericsoftware/spine-core');
const dir=path.join(root,'assets/experimental/WeiCaoCao/Spine/WeiCaoCao');
const meta=JSON.parse(fs.readFileSync(path.join(dir,'character.json'),'utf8'));
const atlas=new spine.TextureAtlas(fs.readFileSync(path.join(dir,'Character/character.atlas'),'utf8'));
for(const page of atlas.pages)page.setTexture(new spine.FakeTexture({width:page.width,height:page.height}));
function read(folder){return new spine.SkeletonBinary(new spine.AtlasAttachmentLoader(atlas)).readSkeletonData(fs.readFileSync(path.join(dir,folder,'character.skel')));}
const combat=read('Combat'),selection=read('Selection');
for(const model of [combat,selection]){
 const shadow=model.findSlot('ren89');
 assert.equal(shadow.attachmentName,'ren89','The hand shadow texture should remain attached');
 assert.equal(shadow.blendMode,spine.BlendMode.Normal,'The hand shadow must respect alpha in Godot');
}
assert.equal(meta.combatRotationDegrees,24.14);
assert.equal(combat.bones[0].rotation,Math.fround(24.14));
assert.equal(selection.bones[0].rotation,0,'Combat correction must not rotate the lobby');
const removed=new Set(meta.foregroundCombatSlots);
assert.equal(removed.size,17);
let poses=0;
for(const animation of combat.animations){
 const skeleton=new spine.Skeleton(combat);
 for(let frame=0;frame<=120;frame++){
  pose(spine,skeleton,animation,animation.duration*frame/120);
  for(const slot of skeleton.slots){
   if(removed.has(slot.data.name))assert.equal(slot.getAttachment(),null,`Foreground accessory returned in ${animation.name}: ${slot.data.name}`);
   const g=slotGeometry(spine,slot);
   if(g)assert(Number.isFinite(g.bounds.minX)&&Number.isFinite(g.bounds.maxY));
  }
  poses++;
 }
}
const lobby=new spine.Skeleton(selection);
pose(spine,lobby,selection.findAnimation('play'),3);
assert(lobby.findSlot('ren17').getAttachment(),'Lobby derivative lost the original artwork');
const layout=fs.readFileSync(path.join(root,'mod/Experimental/WeiCaoCao/combat.gd'),'utf8');
const height=Number(layout.match(/const BODY_HEIGHT = ([\d.]+)/)[1]);
const width=Number(layout.match(/const BODY_WIDTH = ([\d.]+)/)[1]);
const b=meta.assets.Combat.focus;
const scale=Math.min(height/b.height,width/b.width);
assert(scale*b.height<=height+0.01&&scale*b.width<=width+0.01);
assert(scale*b.height>=400,'Body must fill the corrected combat size');
console.log(`CAOCAO_COMBAT_OK poses=${poses} removedAccessories=17 rotation=24.14deg target=${width}x${height} lobbyPreserved=true`);
