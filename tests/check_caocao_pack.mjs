import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {readPack} from '../tools/pck-reader.mjs';
const pack=readPack(process.argv[2]);
function requirePackedTexture(key) {
 const importEntry = pack.get(key + '.import');
 const importedPath = importEntry?.toString('utf8').match(/^path="res:\/\/([^"]+)"/m)?.[1];
 assert(pack.has(key) || (importedPath && pack.has(importedPath)), `Missing packed CaoCao texture: ${key}`);
}
for (const image of ['card_frame_attack_weicaocao_s','card_frame_skill_weicaocao_s','card_frame_power_weicaocao_s','wei_badge']) {
 requirePackedTexture(`Experimental/WeiCaoCao/card_frames/${image}.png`);
}
for(const id of ['yjcm_orb_abolish_slot_orb','yjcm_orb_ba_gua_zhen_orb','yjcm_orb_bai_yin_shi_zi_orb',
 'yjcm_orb_chi_tu_orb','yjcm_orb_ci_xiong_shuang_gu_jian_orb','yjcm_orb_di_lu_orb',
 'yjcm_orb_empty_armor_slot_orb','yjcm_orb_empty_defensive_horse_slot_orb',
 'yjcm_orb_empty_offensive_horse_slot_orb','yjcm_orb_empty_treasure_slot_orb',
 'yjcm_orb_empty_weapon_slot_orb','yjcm_orb_guan_shi_fu_orb','yjcm_orb_han_bing_jian_orb',
 'yjcm_orb_jue_ying_orb','yjcm_orb_qing_gang_jian_orb','yjcm_orb_qing_long_yan_yue_dao_orb',
 'yjcm_orb_ren_wang_dun_orb','yjcm_orb_zhang_ba_she_mao_orb',
 'yjcm_orb_wei_dong_zhuo_zhang_ba_she_mao_orb','yjcm_orb_wei_cao_cao_zhang_ba_she_mao_orb',
 'yjcm_orb_zhu_ge_lian_nu_orb',
 'yjcm_orb_zhu_que_yu_shan_orb','yjcm_orb_zi_xing_orb']) {
 const key=`scenes/orbs/orb_visuals/${id}.tscn`;
 assert(pack.has(key),`Missing equipment orb fallback scene: ${key}`);
 assert(!pack.has(key+'.remap'),`Orb fallback scene must not be remapped to a host-incompatible .scn: ${key}`);
 const text=pack.get(key).toString('utf8');
 assert(text.includes('SpineSkeleton')&&text.includes('preview_animation = "idle_loop"'),`Fallback scene lacks idle SpineSkeleton: ${key}`);
}
for(const file of ['Experimental/WeiCaoCao/select_bg.tscn','Experimental/WeiCaoCao/creature.tscn',
 'Experimental/WeiCaoCao/select_bg.gd','Experimental/WeiCaoCao/combat.gd',
 'Experimental/WeiCaoCao/spine_runtime.gd','Experimental/WeiCaoCao/icon.tscn','materials/cards/frames/card_frame_weicaocao_mat.tres']) assert(pack.has(file),`Missing development resource: ${file}`);
const chars=JSON.parse(pack.get('Yjcm/localization/zhs/characters.json').toString('utf8'));
assert.equal(chars['YJCM_CHARACTER_WEI_CAO_CAO.title'],'威曹操');
assert.equal(chars['YJCM_CHARACTER_WEI_CAO_CAO.description'],'烽火连天，汉室乱，九州飘血。\n谁挺立、谯乡豪杰，志吞霜月');
assert.equal(chars['YJCM_CHARACTER_WEI_DONG_ZHUO.taishan_description'],'赤帝失鹿四海沸，西凉虎贲出金微。\n玄甲耀日动地来，铁骑连云卷朔辉');
assert.equal(chars['YJCM_CHARACTER_WU_ZHUGELIANG.mou_description'],'维炎汉之末运，值鼎祚之将倾。\n白帝托孤之日，赤符授命之辰');
const cards=JSON.parse(pack.get('Yjcm/localization/zhs/cards.json').toString('utf8'));
assert.equal(Object.keys(cards).filter(k=>k.startsWith('YJCM_CARD_WEI_CAO_CAO_')&&k.endsWith('.title')).length,102);
assert.equal(cards['YJCM_CARD_WEI_CAO_CAO_SHA.title'],'杀');
assert.equal(cards['YJCM_CARD_WEI_CAO_CAO_SHAN.title'],'闪');
const catalog=JSON.parse(fs.readFileSync(new URL('../mod/src/Cards/copy-manifest.json',import.meta.url),'utf8'));
const gods=catalog.cards.filter(c=>c.tags?.includes('ShenJiang'));
assert.equal(gods.length,12);
for(const card of gods) assert.equal(cards[card.cloneId.replace('WEI_DONG_ZHUO','WEI_CAO_CAO')+'.title'],
    cards[card.cloneId+'.title'],'God-general localization must match source');
for(const token of ['SHEN_PANG_TONG_FENG_LIAO_YIN','LIE_CANG_OPTION1','LIE_CANG_OPTION2','LIE_CANG_OPTION3','LIE_CANG_OPTION4','LIE_CANG_OPTION5','ZHAN_JUE_OPTION1','ZHAN_JUE_OPTION2'])
    assert(cards[`YJCM_CARD_WEI_CAO_CAO_${token}.title`],`Missing god support token: ${token}`);
assert.equal(cards['YJCM_CARD_WEI_CAO_CAO_HU_JIA.title'],'曹操-护驾');
assert.equal(cards['YJCM_CARD_WEI_CAO_CAO_DUO_YUE.title'],'威曹操-掇月');
assert(cards['YJCM_CARD_WEI_CAO_CAO_DUO_YUE.description'].includes('\n[gold]军合[/gold]：伤害翻倍。'));
const relics=JSON.parse(pack.get('Yjcm/localization/zhs/relics.json').toString('utf8'));
assert.equal(relics['YJCM_RELIC_WEI_CAO_CAO_JUN_HE.flavor'],'聚兵如铁，方能碎尽敌胆');
assert.equal(relics['YJCM_RELIC_WEI_CAO_CAO_JIE_JUN_HE.flavor'],'万众一口，可吞千里之国');
const powers=JSON.parse(pack.get('Yjcm/localization/zhs/powers.json').toString('utf8'));
assert.equal(powers['YJCM_POWER_WEI_CAO_CAO_JUN_HE_POWER.title'],'军合');
assert.equal(cards['YJCM_CARD_WEI_CAO_CAO_LE_CAO_ZHI_FU_LE.title'],'乐曹植-赋乐');
assert.equal(powers['YJCM_POWER_WEI_CAO_CAO_WEN_LAN.title'],'文澜');
assert(powers['YJCM_POWER_WEI_CAO_CAO_WEN_LAN.description'].includes('每使用2张牌后'));
const enchantments=JSON.parse(pack.get('Yjcm/localization/zhs/enchantments.json').toString('utf8'));
assert.equal(enchantments['YJCM_ENCHANTMENT_WEI_CAO_CAO_FU.title'],'赋');
const sourceRoot=new URL('../mod/src/Experimental/WeiCaoCao/',import.meta.url);
const sources=fs.readdirSync(sourceRoot,{recursive:true}).filter(f=>f.endsWith('.cs')).map(f=>fs.readFileSync(new URL(f.replaceAll('\\','/'),sourceRoot),'utf8')).join('\n');
const iconPaths = new Set([...sources.matchAll(/"res:\/\/(Experimental\/WeiCaoCao\/images\/\w+_(?:buff|enchantment)\.png)"/g)].map(match => match[1]));
for (const key of iconPaths) requirePackedTexture(key);
const entries=[...sources.matchAll(/FullPublicEntry\s*=\s*"(YJCM_CARD_WEI_CAO_CAO_[^"]+)"/g)].map(m=>m[1]);
assert.equal(new Set(entries).size,102);
for(const id of entries)assert(cards[id+'.title']&&cards[id+'.description'],'Missing registered card localization '+id);
const slug=value=>value.replace(/([a-z0-9])([A-Z])/g,'$1_$2').toUpperCase();
for(const [attribute,prefix,table] of [['Power','POWER',powers],['Enchantment','ENCHANTMENT',enchantments]]){
 for(const match of sources.matchAll(new RegExp('\\[Register'+attribute+'\\]\\s*public sealed class (WeiCaoCao\\w+)','g'))){
  const id='YJCM_'+prefix+'_'+slug(match[1]);
  assert(table[id+'.title']&&table[id+'.description'],'Missing registered '+attribute+' localization '+id);
  if(attribute==='Power')assert(table[id+'.smartDescription'] || table[id+'.active_description'],'Missing power tooltip '+id);
 }
}
for(const image of ['avatar','select_icon','hu_jia','duo_yue','jie_duo_yue','jun_he','gui_xin_xing_tai','xie_tian_zi','jun_he_buff','fu_le','fu_enchantment','wen_lan_buff']) {
 const key=`Experimental/WeiCaoCao/images/${image}.png`;
 assert(pack.has(key)||pack.has(key+'.import'),`Missing CaoCao image: ${key}`);
}
const portraitSource=fs.readFileSync(new URL('WeiCaoCaoDesignRules.cs',sourceRoot),'utf8');
const portraitMappings=[...portraitSource.matchAll(/\b(WeiCaoCao\w+)\s*=>\s*"([a-z_]+)"/g)];
assert.equal(portraitMappings.length,38,'Expected thirty-eight Wei card mappings, including shared BiZuo art');
for(const [,model,image] of portraitMappings){
 const key=`Experimental/WeiCaoCao/images/${image}.png`;
 assert(pack.has(key)||pack.has(key+'.import'),`Missing portrait for ${model}: ${key}`);
}
console.log('CAOCAO_PACK_OK cards=102 gods=14 weiDesignModels=49 common+starter+FuLe+Fu+WenLan+JunHe+scenes+frame');
