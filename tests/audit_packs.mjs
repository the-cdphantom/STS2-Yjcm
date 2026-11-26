import fs from 'node:fs';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
function readPack(file) {
  const data = fs.readFileSync(file);
  assert.equal(data.readUInt32LE(0), 0x43504447);
  assert.equal(data.readUInt32LE(4), 3);
  const base = Number(data.readBigUInt64LE(24));
  let cursor = Number(data.readBigUInt64LE(32));
  const count = data.readUInt32LE(cursor); cursor += 4;
  const files = new Map();
  for (let i = 0; i < count; i++) {
    const len = data.readUInt32LE(cursor); cursor += 4;
    const name = data.subarray(cursor, cursor + len).toString('utf8').replaceAll('\0', ''); cursor += len;
    const start = Number(data.readBigUInt64LE(cursor)) + base;
    const size = Number(data.readBigUInt64LE(cursor + 8));
    const body = data.subarray(start, start + size);
    assert(crypto.createHash('md5').update(body).digest().equals(data.subarray(cursor + 16, cursor + 32)), name);
    cursor += 36;
    assert(!files.has(name), 'Duplicate path: ' + name);
    files.set(name, body);
  }
  return files;
}
const purePath = path.join(root, 'artifacts/migration/base.pck');
const hasLegacyBaseline = fs.existsSync(purePath);
const pure = hasLegacyBaseline ? readPack(purePath) : new Map();
const test = readPack(path.join(root, 'artifacts/dist/yjcm/Yjcm.pck'));
// The user's B is the middle card in the ABC comparison. Its exported files
// are in v2_meiwu_qiongku; directory display names differ from the final labels.
const dongZhuoBHashes={"card_frame_attack_weidongzhuo_s.png":"9e9d39fc4d9f494e500bd81495e271e2c8b80ad115657de32c38e4f3e2645291","card_frame_power_weidongzhuo_s.png":"3c1d5dc9c8305591f58b28753ed60ba718d7f7de86d95a4b1226961a68715b51","card_frame_skill_weidongzhuo_s.png":"2e27e15f1a267be86e1bfb255348e78691f263bf2a3d1f4d8c19c5c96907bd2c","qun_badge.png":"8268b9f45010226630a283b5c21dfadbf6ec9be67dde990fb4e9ce9bce23866e"};
for (const [file,hash] of Object.entries(dongZhuoBHashes)) {
  const relative='Yjcm/Images/CardFrames/WesternDesertB/'+file;
  assert(test.has(relative)||test.has(relative+'.import'),'Missing approved DongZhuo B artwork: '+relative);
  assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'mod',relative))).digest('hex'),hash,'DongZhuo frame differs from approved B: '+file);
}
for (const name of ['Experimental/WeiCaoCao/select_bg.tscn', 'Experimental/WeiCaoCao/creature.tscn'])
  assert(test.has(name), `Release is missing Cao Cao resource: ${name}`);
for (const folder of ['WeiDongZhuo','MouZhugeliang']) {
  const retired=`Spine/${folder}/Combat/character_attack.skel`;
  for (const base of ['assets/loose','artifacts/dist/yjcm'])
    assert(!fs.existsSync(path.join(root,base,retired)),`Retired character attack remains: ${retired}`);
  const relative=`Spine/${folder}/Combat/character.skel`;
  const source=path.join(root,'assets/loose',relative),delivered=path.join(root,'artifacts/dist/yjcm',relative);
  assert(fs.existsSync(source)&&fs.existsSync(delivered),`Original character skeleton missing: ${relative}`);
  assert(fs.readFileSync(source).equals(fs.readFileSync(delivered)),`Original character skeleton delivery mismatch: ${relative}`);
}
// Old projects embedded copies of Spine files which are now supplied only as loose assets.
// No runtime res://Spine references exist; validate loose files separately in check_delivery.
const obsoleteLooseEntries = new Set();
for (const [name, data] of pure) {
  if (!name.startsWith('Spine/') && !name.startsWith('Voice/')) continue;
  obsoleteLooseEntries.add(name);
  if (name.endsWith('.import')) {
    for (const match of data.toString('utf8').matchAll(/res:\/\/([^"\r\n]+\.ctex)/g)) obsoleteLooseEntries.add(match[1]);
  }
}
for (const name of obsoleteLooseEntries) pure.delete(name);
// This index describes this project's resource set, so adding the DongZhuo assets changes it.
pure.delete('.godot/uid_cache.bin');
pure.delete('WuZhugeliang.json'); // Outdated 1.1.22 manifest; authoritative manifest is mod_manifest.json.
pure.delete('project.binary'); // Config/name changed to the unified project; settings are validated at source level.
// C# is compiled into the two DLLs; the old export also carried unneeded source files.
for (const name of pure.keys()) if (/\.(?:cs|csproj|sln|pdb)$/.test(name)) pure.delete(name);
for (const [packName, files] of [...(hasLegacyBaseline ? [['pure', pure]] : []), ['test', test]]) {
  assert(files.has('Yjcm/localization/zhs/cards.json'), `Missing Yjcm localization root in ${packName} pack`);
  const hitPointAtlas = 'Yjcm/Images/ui/lie_cang_atlas.webp';
  assert(files.has(hitPointAtlas + '.import'), `Missing LieCang UI atlas import in ${packName} pack`);
  const atlasImport = files.get(hitPointAtlas + '.import').toString('utf8');
  const texturePath = atlasImport.match(/path="res:\/\/([^"\r\n]+\.ctex)"/)?.[1];
  assert(texturePath && files.has(texturePath), `Missing LieCang UI atlas texture in ${packName} pack`);
  assert(![...files.keys()].some(name => name.startsWith('WuZhugeliang/')),
    `Legacy resource root remains in ${packName} pack`);
  for (const [name, data] of files) {
    if (!/\.(?:cs|json|tres|tscn|gd|cfg)$/i.test(name)) continue;
    assert(!data.toString('utf8').includes('YJCM_CHARACTER_YJCM'),
      `Legacy model id remains in ${packName} pack: ${name}`);
  }
}
const modifiedTables = new Set(['cards', 'orbs', 'characters', 'relics', 'powers', 'events'].map(n => 'Yjcm/localization/zhs/' + n + '.json'));
modifiedTables.add('Yjcm/localization/zhs/ancients.json');
for (const [name, data] of pure) {
  assert(!/weidongzhuo/i.test(name), 'Feature resource leaked into pure pack: ' + name);
  assert(test.has(name), 'Missing previous asset: ' + name);
  if (modifiedTables.has(name) || (name.startsWith('Yjcm/localization/') && name.endsWith('.json'))) {
    const original = JSON.parse(data.toString('utf8').replace(/^\uFEFF/, ''));
    const expanded = JSON.parse(test.get(name).toString('utf8'));
    assert(!Object.keys(original).some(k => k.includes('YJCM_CHARACTER_YJCM')), 'Legacy localization id in pure pack');
    assert(!Object.keys(expanded).some(k => k.includes('YJCM_CHARACTER_YJCM')), 'Legacy localization id in test pack');
    assert(!Object.keys(original).some(k => k.includes('WEI_DONG_ZHUO')), 'Feature localization in pure pack');
    for (const [k, v] of Object.entries(original)) assert.deepEqual(expanded[k], v, 'Changed previous localization: ' + k);
  } else assert(data.equals(test.get(name)), 'Changed original asset: ' + name);
}
const cards = JSON.parse(test.get('Yjcm/localization/zhs/cards.json').toString('utf8'));
assert.equal(cards['sunqian_burning_link.hoverTitle'], '※模组联动');
for (const [id, argument] of [
  ['YJCM_CARD_YI_ZHEN_QING_MENG', 'DreamFireText'],
  ['YJCM_CARD_ZHU_QUE_YU_SHAN', 'FanFireText'],
  ['YJCM_CARD_WEI_DONG_ZHUO_ZHU_QUE_YU_SHAN', 'FanFireText']
]) {
  assert(cards[`${id}.description`].includes(`{${argument}}`), `Sunqian effect placeholder missing: ${id}`);
  assert.equal(cards[`${id}.fireAttackInlineText`], '[gold]火攻[/gold]');
  assert.equal(cards[`${id}.burningInlineText`], '[purple]灼烧[/purple]');
}
assert(JSON.parse(test.get('Yjcm/localization/zhs/orbs.json').toString('utf8'))[
  'YJCM_ORB_ZHU_QUE_YU_SHAN_ORB.description'].includes('{FanFireText}'), 'Fan orb dynamic text missing');
// Godot exports imported PNGs through .import/ctex, scenes/materials through .remap.
function hasRuntimeResource(name) {
  if (test.has(name)) return true;
  const remapName = test.has(name + '.import') ? name + '.import' : name + '.remap';
  if (!test.has(remapName)) return false;
  const target = test.get(remapName).toString('utf8').match(/path="res:\/\/([^"\r\n]+)"/)?.[1];
  return !!target && test.has(target);
}
assert(hasRuntimeResource('WeiDongZhuo/scenes/taishan_select_bg.tscn'), 'TaiShan select scene missing');
assert(hasRuntimeResource('WeiDongZhuo/scenes/icon.tscn'), 'Dong Zhuo top-bar portrait scene missing');
assert(hasRuntimeResource('WeiDongZhuo/images/avatar.png'), 'Dong Zhuo character avatar missing');
assert(test.has('WeiDongZhuo/scripts/taishan_select_bg.gd'), 'TaiShan select layout missing');
const skinRoot='Spine/WeiDongZhuoTaiShan';
const taiShan=JSON.parse(fs.readFileSync(path.join(root,'assets/loose',skinRoot,'skin.json'),'utf8'));
assert.equal(taiShan.name,'泰山崩石');
assert.equal(taiShan.version,'4.2.43');
for(const asset of taiShan.assets) for(const file of [`${asset.base}.skel`,`${asset.base}.atlas`,...asset.pages]) {
  const relative=path.join(skinRoot,asset.folder,file);
  assert(fs.readFileSync(path.join(root,'assets/loose',relative)).equals(fs.readFileSync(path.join(root,'artifacts/dist/yjcm',relative))),`TaiShan resource mismatch: ${relative}`);
}
// Wu-only collaboration cards: imported art, localized attribution/power and loose WAV.
for (const [portrait, voice] of [
  ['sun_qian_shui_huo_wu_di', 'shui_huo_wu_di'],
  ['xin_san_guo_shui_huo_jian_rong', 'shui_huo_jian_rong']
]) {
  const resource = `Yjcm/Images/Cards/${portrait}.png`;
  assert(hasRuntimeResource(resource), `Collaboration portrait missing: ${resource}`);
  const wav = fs.readFileSync(path.join(root, `assets/loose/Voice/Bank/Assets/${voice}.wav`));
  assert.equal(wav.toString('ascii', 0, 4), 'RIFF', 'Invalid collaboration audio');
  assert.equal(wav.toString('ascii', 8, 12), 'WAVE', 'Invalid collaboration audio');
  assert(wav.equals(fs.readFileSync(path.join(root, `artifacts/dist/yjcm/Voice/Bank/Assets/${voice}.wav`))), 'Collaboration WAV delivery mismatch');
}
assert.equal(cards['YJCM_HOVER_COLLABORATION.title'], '联动');
assert.equal(cards['YJCM_HOVER_COLLABORATION_SUN_QIAN.description'], '本牌来自于新三国孙乾mod');
assert.equal(cards['YJCM_HOVER_COLLABORATION_XIN_SAN_GUO.description'], '本牌来自于新三国首播版mod');
const collaborationPowers = JSON.parse(test.get('Yjcm/localization/zhs/powers.json').toString('utf8'));
assert.equal(collaborationPowers['YJCM_POWER_SHUI_HUO_JIAN_RONG_POWER.title'], '水火兼容');
// CardKeyword 自带卡面文本；正文再单独写一次会在游戏内重复显示。
const standaloneKeyword = /(?:^|[。\n])\s*(?:保留|消耗|固有|虚无|狡黠|奇巧)(?:。|\n|$)/u;
for (const [packName, files] of [...(hasLegacyBaseline ? [['pure', pure]] : []), ['test', test]]) {
  const cardText = JSON.parse(files.get('Yjcm/localization/zhs/cards.json').toString('utf8').replace(/^\uFEFF/u, ''));
  for (const [key, value] of Object.entries(cardText)) {
    if (key.endsWith('.description') && typeof value === 'string')
      assert(!standaloneKeyword.test(value), `Duplicate automatic keyword in ${packName} pack: ${key}`);
  }
}
for (const id of ['YJCM_CARD_LE_BU_SI_SHU', 'YJCM_CARD_WEI_DONG_ZHUO_LE_BU_SI_SHU'])
  assert(!cards[id + '.description'].includes('消耗'), 'Stale LeBu description');
for (const id of [
  'YJCM_CARD_WEI_ZHANG_LIAO_PO_RONG',
  'YJCM_CARD_XING_YUAN_SHAO_ZONG_SHI', 'YJCM_CARD_WEI_ZHANG_LIAO_YU_XI',
  'YJCM_CARD_WANG_YUE_SHI_FENG', 'YJCM_CARD_MOU_JIA_XU_FU_MOU_YANG',
  'YJCM_CARD_MOU_JIA_XU_FU_MOU_YIN'
]) {
  assert(cards[id + '.description'].includes('{IfUpgraded:show:'), `Upgrade-aware text missing: ${id}`);
  assert(!cards[id + '.description'].includes('。升级后'), `Legacy upgrade prose remains: ${id}`);
}
assert(cards['YJCM_CARD_WEI_MA_CHAO_JI_ZHAN.description'].includes('{ComboDamage}')
  && cards['YJCM_CARD_WEI_MA_CHAO_JI_ZHAN.description'].includes('{StrengthLoss}')
  && cards['YJCM_CARD_WEI_MA_CHAO_JI_ZHAN.description'].includes('{RepeatCount}'),
  'JiZhan dynamic upgrade values missing');
assert(!cards['YJCM_CARD_MOU_ZHANG_XIU_FU_XI.description'].includes('摸'), 'MouZhangXiu draw text remains');
assert(cards['YJCM_CARD_WANG_YUE_SHI_FENG.description'].includes('永久免费')
  && !cards['YJCM_CARD_WANG_YUE_SHI_FENG.description'].includes('消耗'),
  'ShiFeng generated Duel permanent-free wording missing');
for (const id of ['YJCM_CARD_WEI_DONG_ZHUO_JU_CHUI', 'YJCM_CARD_WEI_DONG_ZHUO_JIE_JU_CHUI']) {
  const description = cards[id + '.description'];
  assert(description.includes('造成{Damage}点伤害/获得{Block}点'), `JuChui branch order missing: ${id}`);
  assert(description.includes('使敌人失去{ComboLoss}点生命/回复{ComboHeal}点血量'),
    `JuChui combo wording missing: ${id}`);
  assert(!description.includes('无来源、无视格挡且不受任何buff影响'),
    `JuChui special-damage implementation prose leaked into card text: ${id}`);
}
assert(cards['YJCM_CARD_MOU_XU_YOU_MO_YOU.description'].includes('弃置手牌中所有该类型的牌'),
  'MoYou discard-all-of-type wording missing');
assert(cards['YJCM_CARD_MOU_XU_YOU_MO_YOU.description'].includes('下一张牌免费'), 'MoYou next-card-free wording missing');
assert(!cards['YJCM_CARD_MOU_XU_YOU_MO_YOU.description'].includes('技能牌免费'), 'Stale MoYou skill-only wording remains');
assert(cards['YJCM_CARD_WEI_LV_BU_BA_GUAN.description'].includes('弃牌数×{ComboDamage}'),
  'BaGuan single-hit total-damage wording missing');
assert(!hasLegacyBaseline || pure.size >= 600, `Required base resource set unexpectedly small: ${pure.size}`);
assert(test.has('WeiDongZhuo/images/guang_yong.png'), 'GuangYong icon missing');
assert(test.has('scenes/rest_site/characters/yjcm_character_wei_dong_zhuo_rest_site.tscn'),
  'WeiDongZhuo native rest-site scene fallback missing');
assert(!pure.has('Yjcm/Images/events/qu_yuan_tou_jiang.png'),
  'QuYuanTouJiang feature art leaked into pure pack');
assert(test.has('Yjcm/Images/events/qu_yuan_tou_jiang.png'),
  'QuYuanTouJiang event art missing');
assert(test.has('Yjcm/Images/events/qu_yuan_tou_jiang.png.import'),
  'QuYuanTouJiang event art import missing');
const quYuanImport = test.get('Yjcm/Images/events/qu_yuan_tou_jiang.png.import').toString('utf8');
const quYuanTexture = quYuanImport.match(/path="res:\/\/(\.godot\/imported\/qu_yuan_tou_jiang[^"]+\.ctex)"/u)?.[1];
assert(quYuanTexture, 'QuYuanTouJiang imported texture path missing');
assert(test.has(quYuanTexture), 'QuYuanTouJiang imported texture missing from development pack');
assert(test.has('WeiDongZhuo/images/guang_yong.png.import'), 'GuangYong import remap missing');
assert(test.has('WeiDongZhuo/images/xiao_yan.png'), 'XiaoYan/JiaoWang shared card art missing');
const guangYongImport = test.get('WeiDongZhuo/images/guang_yong.png.import').toString('utf8');
const guangYongTexture = guangYongImport.match(/path="res:\/\/(\.godot\/imported\/guang_yong[^"]+\.ctex)"/u)?.[1];
assert(guangYongTexture, 'GuangYong imported texture path missing');
assert(test.has(guangYongTexture), 'GuangYong imported texture missing from development pack');
const resourcePackRoot = path.join(root, 'mod');
const guangYongSource = fs.readFileSync(path.join(resourcePackRoot, 'WeiDongZhuo/images/guang_yong.png'));
const guangYongState = fs.readFileSync(path.join(resourcePackRoot, guangYongTexture.replace(/\.ctex$/u, '.md5')), 'utf8');
const guangYongSourceMd5 = crypto.createHash('md5').update(guangYongSource).digest('hex');
assert(guangYongState.includes(`source_md5="${guangYongSourceMd5}"`), 'GuangYong imported texture is stale');
assert(test.get(guangYongTexture).equals(fs.readFileSync(path.join(resourcePackRoot, guangYongTexture))),
  'GuangYong imported texture differs from the reimported resource');
for (const name of [
  'ju_chui', 'jie_ju_chui', 'hao_shou', 'qu_yuan_li_sao', 'li_sao_power',
  'mo_shou', 'ba_guan', 'ji_zhan', 'po_rong', 'wei_lin', 'zong_shi',
  'zhui_tao', 'yu_xi', 'xiao_xi', 'xiong_huo', 'sha_jue', 'duan_ti',
  'xiao_yan', 'fu_xi', 'wang_yue', 'zuo_jun', 'xian_shuai', 'shu_guo',
  'xiong_suan', 'nei_fa', 'dou_wei', 'fu_mou_yang', 'fu_mou_yin',
  'qu_xian', 'su_shen', 'ru_shi', 'lian_jie', 'jiang_xian', 'huo_shao_luo_yang', 'zu_yin', 'piao_ping'
]) {
  assert(test.has(`WeiDongZhuo/images/${name}.png`), `Card art missing: ${name}`);
  assert(test.has(`WeiDongZhuo/images/${name}.png.import`), `Card art import missing: ${name}`);
}
for (const name of [
  'duan_ti_buff', 'qu_xian_buff', 'mo_you_buff', 'mo_shou_buff',
  'wang_yue_buff', 'xiong_huo_buff', 'yu_xi_buff', 'zhui_tao_buff',
  'lian_jie_buff', 'jiang_xian_buff'
]) {
  assert(test.has(`WeiDongZhuo/images/${name}.png`), `Power icon missing: ${name}`);
  assert(test.has(`WeiDongZhuo/images/${name}.png.import`), `Power icon import missing: ${name}`);
}
for (const key of [
  'WEI_DONG_ZHUO_JU_CHUI', 'WEI_DONG_ZHUO_JIE_JU_CHUI', 'WEI_DONG_ZHUO_HAO_SHOU',
  'WEI_DONG_ZHUO_QU_YUAN_LI_SAO', 'YUAN_YIN_MO_SHOU', 'WEI_LV_BU_BA_GUAN',
  'WEI_MA_CHAO_JI_ZHAN', 'WEI_ZHANG_LIAO_PO_RONG', 'XING_DONG_ZHUO_WEI_LIN',
  'XING_YUAN_SHAO_ZONG_SHI', 'WEI_GONG_SUN_ZAN_ZHUI_TAO', 'WEI_ZHANG_LIAO_YU_XI',
  'NIU_FU_XIAO_XI', 'WU_PU_DUAN_TI', 'XU_RONG_XIONG_HUO', 'XU_RONG_SHA_JUE',
  'XING_YUAN_SHAO_XIAO_YAN', 'XING_YUAN_SHAO_JIAO_WANG', 'MOU_ZHANG_XIU_FU_XI',
  'WANG_YUE_JIAN_DAO', 'WANG_YUE_SHI_FENG', 'MOU_XU_YOU_MO_YOU',
  'MOU_JU_SHOU_ZUO_JUN', 'DING_YUAN_XIAN_SHUAI', 'MA_TIE_QU_XIAN',
  'ZHANG_HUAN_SHU_GUO', 'LI_JUE_GUO_SI_XIONG_SUAN',
  'YUAN_TAN_YUAN_SHANG_YUAN_XI_NEI_FA', 'MOU_DONG_CHENG_DOU_WEI',
  'MOU_JIA_XU_FU_MOU_YANG', 'MOU_JIA_XU_FU_MOU_YIN', 'MOU_JIA_XU_SU_SHEN',
  'MOU_JIA_XU_RU_SHI', 'WU_HUANG_FU_SONG_LIAN_JIE', 'WU_HUANG_FU_SONG_JIANG_XIAN',
  'DONG_ZHUO_HUO_SHAO_LUO_YANG', 'ZHU_GE_JING_ZU_YIN', 'QIN_YI_LU_PIAO_PING'
])
  assert(cards[`YJCM_CARD_${key}.title`], `Card localization missing: ${key}`);
const powers = JSON.parse(test.get('Yjcm/localization/zhs/powers.json').toString('utf8'));
for (const voice of ['zu_yin', 'piao_ping']) {
  const relative = `Voice/WeiDongZhuo/${voice}.wav`;
  const wav = fs.readFileSync(path.join(root, 'assets/loose', relative));
  assert.equal(wav.toString('ascii', 0, 4), 'RIFF');
  assert.equal(wav.toString('ascii', 8, 12), 'WAVE');
  assert(wav.equals(fs.readFileSync(path.join(root, 'artifacts/dist/yjcm', relative))), `New Dongzhuo voice delivery mismatch: ${voice}`);
}
assert.equal(powers['YJCM_POWER_PIAO_PING.title'], '漂萍');
assert(powers['YJCM_POWER_PIAO_PING.active_description'].includes('{DiscardNext:cond:弃|摸}{Count}'));
assert.equal(cards['YJCM_CARD_YUAN_TAN_YUAN_SHANG_YUAN_XI_NEI_FA.description'],
  '摸3张牌，然后弃置1张。\n弃置攻击牌：本回合不能使用技能牌，接下来的若干张攻击牌免费，张数等于弃牌后手中的技能牌数量；弃置技能牌则相反。',
  'Packaged NeiFa must use the approved limited-use description without X');
assert.equal(powers['YJCM_POWER_NEI_FA.active_description'],
  '本回合不能使用{BlocksSkills:cond:技能牌|攻击牌}，接下来的{Remaining}张{BlocksSkills:cond:攻击牌|技能牌}免费。',
  'Packaged NeiFa must show its actual remaining free uses');
const events = JSON.parse(test.get('Yjcm/localization/zhs/events.json').toString('utf8'));
const ancients = JSON.parse(test.get('Yjcm/localization/zhs/ancients.json').toString('utf8'));
for (const ancient of ['DARV', 'NEOW', 'NONUPEIPE', 'OROBAS', 'PAEL', 'TANX', 'TEZCATARA', 'VAKUU'])
  assert(ancients[`${ancient}.talk.YJCM_CHARACTER_WEI_DONG_ZHUO.0-0.ancient`], `WeiDongZhuo ancient dialogue missing: ${ancient}`);
assert(ancients['THE_ARCHITECT.talk.YJCM_CHARACTER_WEI_DONG_ZHUO.0-0r.ancient'], 'WeiDongZhuo ancient dialogue missing: THE_ARCHITECT');
assert.equal(powers['YJCM_POWER_LI_SAO.title'], '离骚', 'LiSao power localization missing');
assert.equal(events['YJCM_EVENT_QU_YUAN_TOU_JIANG.title'], '屈原投江',
  'QuYuanTouJiang event localization missing');
assert(events['YJCM_EVENT_QU_YUAN_TOU_JIANG.pages.INITIAL.options.APPROACH.description'].includes('屈原-离骚'),
  'QuYuanTouJiang reward wording missing');
assert.equal(powers['YJCM_POWER_MO_SHOU_PLUS.description'], powers['YJCM_POWER_MO_SHOU.description'],
  'Legacy MoShou+ save compatibility still has the removed extra play');
assert.equal(powers['YJCM_POWER_MO_YOU_FREE_NEXT.description'], '下{Amount}张牌免费。',
  'MoYou stacked-free-card power localization missing');
assert(powers['YJCM_POWER_LIAN_JIE.description'].includes('每回合限一次'),
  'LianJie power localization missing');
assert(powers['YJCM_POWER_JIANG_XIAN.description'].includes('造成伤害后'),
  'JiangXian power localization missing');
assert(powers['YJCM_POWER_SHI_FENG.description'].includes('永久免费')
  && !powers['YJCM_POWER_SHI_FENG.description'].includes('消耗'),
  'ShiFeng power permanent-free wording missing');
assert(powers['YJCM_POWER_SHI_FENG_PLUS.description'].includes('永久免费')
  && !powers['YJCM_POWER_SHI_FENG_PLUS.description'].includes('消耗'),
  'ShiFeng+ power permanent-free wording missing');
assert(test.size >= pure.size, `Feature pack smaller than base: ${test.size} < ${pure.size}`);
for (const resource of [
  'Yjcm/scenes/shang_fang_gu_event_background.tscn',
  'Yjcm/scenes/shang_fang_gu_event_background.gd',
  'Yjcm/Images/events/shang_fang_gu_zhi_yu.png',
  'Yjcm/Images/Relics/qi_xing_deng.png',
  'Yjcm/Images/Relics/Big/qi_xing_deng.png'
]) assert(hasRuntimeResource(resource), `ShangFangGu runtime art missing: ${resource}`);
assert.equal(events['YJCM_EVENT_SHANG_FANG_GU_ZHI_YU.title'], '上方谷之雨');
assert.equal(events['YJCM_EVENT_SHANG_FANG_GU_ZHI_YU.pages.INITIAL.description'],
  '独木难支益州地，上方雨落万事空\n先帝大业化清梦，只叹天意助冢虎');
const relicText = JSON.parse(test.get('Yjcm/localization/zhs/relics.json').toString('utf8'));
assert.equal(relicText['YJCM_RELIC_QI_XING_DENG.title'], '七星灯');
assert.equal(relicText['YJCM_RELIC_QI_XING_DENG.flavor'], '魏延将军，走慢点！');
console.log(`PACK_AUDIT_OK mod=Yjcm entries=${test.size} legacyBaseline=${hasLegacyBaseline ? pure.size : 'not-present'}; runtime resources and localizations checked`);
