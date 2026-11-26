// Render the actual converted Spine data. These are development previews, not game screenshots.
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {startServer} from './preview/server.mjs';
const root=path.resolve(import.meta.dirname,'..');
const requestedAnimation=process.argv[2]||'';
const combatLayout=fs.readFileSync(path.join(root,'mod/Experimental/WeiCaoCao/combat.gd'),'utf8');
const bodyHeight=Number(combatLayout.match(/const BODY_HEIGHT = ([\d.]+)/)?.[1]);
const bodyWidth=Number(combatLayout.match(/const BODY_WIDTH = ([\d.]+)/)?.[1]);
const facingX=Number(combatLayout.match(/const FACING_X = (-?[\d.]+)/)?.[1]);
const layout=fs.readFileSync(path.join(root,'mod/Experimental/WeiCaoCao/select_bg.gd'),'utf8');
const zoom=Number(layout.match(/const PORTRAIT_ZOOM = ([\d.]+)/)?.[1]);
const vector=name=>layout.match(new RegExp(`const ${name} = Vector2\\(([\\d.]+), ([\\d.]+)\\)`))?.slice(1).map(Number);
const previousFace=vector('PREVIOUS_FACE'),referenceFace=vector('REFERENCE_FACE');
assert(Number.isFinite(zoom)&&previousFace?.length===2&&referenceFace?.length===2,'Missing portrait calibration');
const require=createRequire(path.join(process.env.WDZ_NODE_MODULES||path.join(root,'node_modules'),'_caocao_preview.cjs'));
const {chromium}=require('playwright-core');
const server=await startServer(0),origin=`http://127.0.0.1:${server.address().port}`;
const browser=await chromium.launch({channel:'chrome',headless:true,args:['--enable-webgl','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
try{
 const page=await browser.newPage({viewport:{width:1280,height:720}}),errors=[];
 page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(origin+'/tools/preview/README.md');
 await page.setContent(`<style>body{margin:0}canvas{display:block}</style><canvas width="1280" height="720"></canvas><script src="${origin}/vendor/spine-webgl.js"></script>`);
 const frames=await page.evaluate(async ({origin,zoom,previousFace,referenceFace,requestedAnimation,bodyHeight,bodyWidth,facingX})=>{
  const sp=window.spine,canvas=document.querySelector('canvas');
  const context=new sp.ManagedWebGLRenderingContext(canvas,{alpha:true,preserveDrawingBuffer:true,antialias:true});
  const renderer=new sp.SceneRenderer(canvas,context),manager=new sp.AssetManager(context),gl=context.gl;
  const base=origin+'/assets/experimental/WeiCaoCao/Spine/WeiCaoCao';
  const manifest=await(await fetch(base+'/character.json')).json(),models={};
  for(const [folder,asset] of Object.entries(manifest.assets)){
   const skel=`${base}/${folder}/${asset.base}.skel`,atlas=`${base}/${['Combat','Selection'].includes(folder)?'Character':folder}/${asset.base}.atlas`;
   manager.loadBinary(skel);manager.loadTextureAtlas(atlas);await manager.loadAll();
   const data=new sp.SkeletonBinary(new sp.AtlasAttachmentLoader(manager.require(atlas))).readSkeletonData(manager.require(skel));
   models[folder]={data,skeleton:new sp.Skeleton(data),asset};
  }
  function draw(folder,time,scale,x,y,flipX=1){
   const {data,skeleton}=models[folder];skeleton.setToSetupPose();
   data.findAnimation(requestedAnimation||manifest.idleAnimation).apply(skeleton,-1,time,true,[],1,sp.MixBlend.replace,sp.MixDirection.mixIn);
   skeleton.scaleX=scale*flipX;skeleton.scaleY=scale;skeleton.x=x;skeleton.y=720-y;
   skeleton.updateWorldTransform(sp.Physics.none);renderer.drawSkeleton(skeleton,false);
  }
  renderer.camera.position.set(640,360,0);renderer.camera.viewportWidth=1280;renderer.camera.viewportHeight=720;renderer.camera.update();
  const output=[];
  for(const t of [0,3,6,9]){
   gl.viewport(0,0,1280,720);gl.clearColor(.04,.04,.055,1);gl.clear(gl.COLOR_BUFFER_BIT);renderer.begin();
   const b=manifest.assets.Background.focus,c=manifest.assets.Character.focus;
   const bg=Math.max(1280/b.width,720/b.height)*1.025;
   draw('Background',t,bg,640-b.centerX*bg,360+b.centerY*bg);
   const fit=Math.min(720*.9/c.height,1280*.6/c.width);
   draw('Selection',t,fit*zoom,(1280*.52-c.centerX*fit)*zoom+1280*(referenceFace[0]-previousFace[0]*zoom),
    (720*.9+c.minY*fit)*zoom+720*(referenceFace[1]-previousFace[1]*zoom));
   renderer.end();output.push({name:`select-${t}.png`,url:canvas.toDataURL(),error:gl.getError()});
  }
  gl.clearColor(.10,.14,.11,1);gl.clear(gl.COLOR_BUFFER_BIT);renderer.begin();
  const c=manifest.assets.Combat.focus,fit=Math.min(bodyHeight/c.height,bodyWidth/c.width);
  draw('Combat',3,fit,360-c.centerX*fit*facingX,580+c.minY*fit,facingX);renderer.end();
  output.push({name:'combat.png',url:canvas.toDataURL(),error:gl.getError()});
  return output;
 },{origin,zoom,previousFace,referenceFace,requestedAnimation,bodyHeight,bodyWidth,facingX});
 assert.equal(errors.length,0,JSON.stringify(errors));assert(frames.every(f=>f.error===0));
 for(const frame of frames)fs.writeFileSync(path.join(root,'artifacts/reports/weicaocao',frame.name),Buffer.from(frame.url.split(',')[1],'base64'));
 console.log(`CAOCAO_RENDER_OK frames=${frames.length} glErrors=0`);
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
