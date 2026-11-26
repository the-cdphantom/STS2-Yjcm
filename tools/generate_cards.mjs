// Read-only generator: emit an apply_patch patch, or verify already-generated files.
// All generated artifacts stay below Cards and CardPools/WeiDongZhuoCardPool.cs.
// Run from any directory: node generate_cards.mjs --patch | <apply_patch tool>
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { selectVariant, conditionalLines } from './variant-source.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const workspace = path.resolve(here, '..');
const sourceRoot = path.join(workspace, 'mod');
const variantIndex = process.argv.indexOf('--variant');
const variant = variantIndex < 0 ? 'stable' : process.argv[variantIndex + 1];
assert(['stable', 'beta'].includes(variant), 'Use --variant stable or --variant beta');
const stableRoot = workspace;
const outputRoot = sourceRoot;
const outputCards = path.join(outputRoot, 'src/Cards');
const readRaw = p => fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
const read = p => p.endsWith('.cs') ? selectVariant(readRaw(p), 'stable') : readRaw(p);
const hash = p => {
  // Git normalizes text to LF in a clean checkout. Match that on Windows too.
  let data = fs.readFileSync(p, 'utf8').replace(/\r\n/g, '\n');
  // Generated clone localization is an output, not an input. Avoid a self-hash cycle.
  if (/[\\/]Yjcm[\\/]localization[\\/]zhs[\\/](cards|orbs)\.json$/.test(p)) {
    const table = JSON.parse(readRaw(p));
    data = JSON.stringify(Object.fromEntries(Object.entries(table).filter(([key]) => !/^YJCM_(CARD|ORB)_WEI_DONG_ZHUO_/.test(key)).sort(([a],[b]) => a.localeCompare(b,'en'))));
  }
  return crypto.createHash('sha256').update(data).digest('hex');
};
const snake = s => s.replace(/([A-Z]+)([A-Z][a-z])/g, '$1_$2').replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase();
const id = (s, category = 'CARD') => `YJCM_${category}_${snake(s)}`;
const clone = s => `WeiDongZhuo${s}`;

// Lex strings/comments separately. Identifier rewriting never touches paths, keys or dialogue.
const lexical = /\/\/[^\n]*|\/\*[\s\S]*?\*\/|(?:\$@|@\$|@)"(?:""|[^"])*"|\$?"(?:\\[\s\S]|[^"\\])*"|'(?:\\[\s\S]|[^'\\])*'|\b[A-Za-z_]\w*\b/g;
function rewrite(text, aliases) {
  return text.replace(lexical, token => /^[A-Za-z_]\w*$/.test(token) ? (aliases[token] ?? token) : token);
}
function mask(text) {
  return text.replace(lexical, token => /^[A-Za-z_]\w*$/.test(token) ? token : token.replace(/[^\n]/g, ' '));
}
function canonicalCode(text) {
  let last = 0, result = '';
  for (const match of text.matchAll(lexical)) {
    result += text.slice(last, match.index).replace(/\s/g, '');
    if (!match[0].startsWith('//') && !match[0].startsWith('/*')) result += match[0];
    last = match.index + match[0].length;
  }
  return result + text.slice(last).replace(/\s/g, '');
}
function declarations(text) {
  const clean = mask(text);
  const found = [];
  const pattern = /\b(?:(?:public|internal|private|protected|sealed|abstract|static|partial)\s+)*class\s+(\w+)[^{]*\{/g;
  for (const match of clean.matchAll(pattern)) {
    const open = match.index + match[0].length - 1;
    let end = open + 1, depth = 1;
    for (; end < clean.length && depth; end++) {
      if (clean[end] === '{') depth++;
      if (clean[end] === '}') depth--;
    }
    assert.equal(depth, 0, `Unbalanced declaration: ${match[1]}`);
    const signature = match[0];
    found.push({ name: match[1], base: signature.match(/:\s*(\w+)/)?.[1] ?? null,
      abstract: /\babstract\b/.test(signature), body: text.slice(match.index, end) });
  }
  return found;
}

const sourceCards = path.join(sourceRoot, 'src/Cards');
const poolFile = path.join(sourceRoot, 'src/CardPools/WuZhugeliangCardPool.cs');
const originalPool = new Set([...read(poolFile).matchAll(/typeof\((\w+)\)/g)].map(m => m[1]));
const cards = new Map();
const sourceFiles = new Set([poolFile]);
for (const filename of fs.readdirSync(sourceCards).filter(f => f.endsWith('.cs')).sort()) {
  const file = path.join(sourceCards, filename), text = read(file);
  sourceFiles.add(file);
  const namespaceAt = text.indexOf('namespace Yjcm;');
  for (const decl of declarations(text)) {
    decl.file = file;
    decl.imports = text.slice(0, namespaceAt).trim();
    decl.tags = [...(mask(decl.body).match(/CanonicalTags\s*=>\s*\[([^\]]*)\]/)?.[1] ?? '').matchAll(/YjcmCardTags\.(\w+)/g)].map(m => m[1]);
    cards.set(decl.name, decl);
  }
}

const groups = {
  basic: ['Sha', 'Shan', 'Jiu', 'Tao'],
  trick: ['LeBuSiShu', 'WuZhongShengYou', 'HuoGong', 'JueDou', 'TieSuoLianHuan', 'NanManRuQin', 'WanJianQiFa'],
  weapon: ['QingLongYanYueDao', 'GuanShiFu', 'ZhuQueYuShan', 'ZhuGeLianNu', 'HanBingJian', 'ZhangBaSheMao', 'QingGangJian', 'CiXiongShuangGuJian'],
  armor: ['RenWangDun', 'BaGuaZhen', 'BaiYinShiZi'],
  mount: ['DiLu', 'ChiTu', 'JueYing', 'ZiXing'],
  general: [],
  token: ['WuSeSha', 'ShenPangTongFengLiaoYin', 'LieCangOption1', 'LieCangOption2', 'LieCangOption3', 'LieCangOption4', 'LieCangOption5', 'ZhanJueOption1', 'ZhanJueOption2'],
};
for (const c of cards.values()) {
  if (c.tags.includes('WuJiang') && (c.tags.includes('ShenJiang') || !c.tags.some(t => ['Wei', 'Shu', 'Wu'].includes(t)))) {
    assert(c.tags.some(t => ['Qun', 'ShenJiang'].includes(t)), `Review unclassified general ${c.name}`);
    groups.general.push(c.name);
  }
}
const names = Object.values(groups).flat();
assert.equal(new Set(names).size, names.length);
for (const name of names) assert(cards.has(name), `Missing source ${name}`);
const aliases = Object.fromEntries(names.map(name => [name, clone(name)]));
aliases.WuZhugeliangCard = 'WeiDongZhuoCard';
aliases.ZhangBaSheMaoOrb = 'WeiDongZhuoZhangBaSheMaoOrb';

const excludedSpecial = {
  SunQianShuiHuoWuDi: '武诸葛亮专属联动牌，不复制到董卓卡池',
  XinSanGuoShuiHuoJianRong: '武诸葛亮专属联动牌，不复制到董卓卡池',
  ZhiZhe: '武诸葛亮起始专属：智哲，非通用基础牌',
  BeiFa: '武诸葛亮专属：北伐；悬浮提示包含蜀，未声明武将 tag',
  JieDongFeng: '诸葛亮专属：借东风；未声明武将 tag',
  JinCuiXingTai: '武诸葛亮专属：尽瘁形态；未声明武将 tag',
  YiZhenQingMeng: '角色技能语音及星资源联动，未证明是通用锦囊，先列角色专属待定',
  ChenShiQingBei: '陈式专属擎北，漏标武将／蜀 tag',
  MouFaZhengXiXing: '谋法正专属悉性，漏标武将／蜀 tag',
  BeiZhiCard: '背置测试／角色生成牌，非通用杀闪锦囊装备',
  HuangWudieShouXing: '蜀武将黄舞蝶生成 token',
  HuangWudieShaXue: '蜀武将黄舞蝶生成 token',
  PangHongPingZhiYin: '蜀武将庞宏转换阴 token',
  ShiWeiYanZhongAo: '武诸葛亮事件任务牌：仅进入武诸葛亮牌组，不复制到威董卓通用包',
};

const outputs = new Map();
function betaSignatures(content) {
  // Verified against matching wuzhugeliang sources, not copied from their gameplay bodies.
  return content.replaceAll('.FromCard(this)', '.FromCard(this, cardPlay)')
    .replaceAll('AttackContext.CreateAsync(CombatState, choiceContext, this)', 'AttackContext.CreateAsync(CombatState, choiceContext, cardPlay)')
    .replaceAll('CreatureCmd.Damage(choiceContext, enemy, dmg, ValueProp.Move, this)', 'CreatureCmd.Damage(choiceContext, enemy, dmg, ValueProp.Move, this, cardPlay)')
    .replaceAll('ValueProp.Unblockable | ValueProp.Unpowered, this);', 'ValueProp.Unblockable | ValueProp.Unpowered, this, null);')
    .replaceAll('CreatureCmd.LoseBlock(target, target.Block)', 'CreatureCmd.LoseBlock(choiceContext, target, target.Block, null)');
}
function emit(relative, content) {
  assert(relative.startsWith('src/Cards/') || relative === 'src/CardPools/WeiDongZhuoCardPool.cs' || /^Yjcm\/localization\/zhs\/(cards|orbs)\.json$/.test(relative));
  if (relative.endsWith('.cs')) content = conditionalLines(content, betaSignatures);
  outputs.set(relative, content.trimEnd() + '\n');
}
const cardDir = 'src/Cards/';
const generatedBanner = name => `// Generated by generate_cards.mjs from ${path.relative(workspace, cards.get(name).file).replaceAll('\\', '/')}.\n// Independent model; source effects, resource literals and shared powers are preserved.\n`;
const metadata = [];
for (const name of names) {
  const c = cards.get(name);
  let body = rewrite(c.body, aliases);
  if (c.base === 'CardModel') {
    body = body.replace(/public override CardPoolModel Pool\s*=>[^;]+;\s*/g, '');
    body = body.replace('{', '{\n    public override CardPoolModel Pool => ModelDb.CardPool<WeiDongZhuoCardPool>();\n');
  }
  if (name === 'ShenZhangFeiXunShi') {
    // Both original and clone tricks can be acquired from other sources.
    body = body.replace('MultiTargetTypes.Contains(c.GetType())', 'MultiTargetTypes.Contains(WeiDongZhuoCloneMap.CloneType(c.GetType()))');
  }
  if (name === 'ShenJiangWeiPingXiang') {
    // Keep cross-character generation, map only our equivalents and avoid double weighting.
    body = body.replace('ModelDb.AllCards\n', 'ModelDb.AllCards\n            .Select(WeiDongZhuoCloneMap.ForGeneration)\n            .DistinctBy(c => c.Id)\n');
  }
  const imports = c.imports.includes('using MegaCrit.Sts2.Core.Models;') ? c.imports : c.imports + '\nusing MegaCrit.Sts2.Core.Models;';
  const attribute = `[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "${id(clone(name))}")]`;
  emit(`${cardDir}Generated/${clone(name)}.cs`, `${generatedBanner(name)}${imports}\n\nnamespace Yjcm;\n\n${attribute}\n${body}`);
  const refs = [...mask(c.body).matchAll(/(?:CreateCard|FromCard|FromCardWithCardHoverTips)\s*<\s*(\w+)/g)].map(m => m[1]);
  metadata.push({ originalType: name, cloneType: clone(name), originalId: id(name), cloneId: id(clone(name)),
    category: Object.keys(groups).find(g => groups[g].includes(name)), tags: c.tags, originalPoolListed: originalPool.has(name),
    source: path.relative(workspace, c.file).replaceAll('\\', '/'), generatedTypes: refs,
    sourceSha256: hash(c.file), token: /CardRarity\.Token/.test(mask(c.body)) });
}

// Shared equipment is retained except this producer of a typed WuSeSha token.
const orbPath = path.join(sourceRoot, 'src/Orbs/ZhangBaSheMaoOrb.cs');
sourceFiles.add(orbPath);
emit(`${cardDir}Compatibility/WeiDongZhuoZhangBaSheMaoOrb.cs`,
  '// Generated equipment producer: keeps the original slot/effects, creates the clone token.\n' + rewrite(read(orbPath), aliases));

// Handwritten shared card base and compatibility patches remain in src/Cards.

emit('src/CardPools/WeiDongZhuoCardPool.cs', `using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace Yjcm;

// Only independent WeiDongZhuo types. Basic and hidden Token rarities stay unchanged.
public sealed class WeiDongZhuoCardPool : TypeListCardPoolModel
{
    public override string Title => "weidongzhuo";
    public override string EnergyColorName => "yjcm_dongzhuo";
    public override string CardFrameMaterialPath => "card_frame_weidongzhuo";
    public override Color DeckEntryCardColor => new("#B98A31");
    public override Color EnergyOutlineColor => new("#5A3825");
    public override bool IsColorless => false;

    // Native AllCards appends attribute registrations to CardTypes; expose each ID once.
    // Keep this local to the test pool; never alter the original pool's enumeration.
    public override IEnumerable<CardModel> AllCards => base.AllCards.DistinctBy(card => card.Id);

    protected override IEnumerable<Type> CardTypes => new Type[]
    {
${Object.entries(groups).map(([g, list]) => `        // ${g}\n${list.map(n => `        typeof(${clone(n)}),`).join('\n')}`).join('\n')}
    };
}`);

emit(`${cardDir}WeiDongZhuoCloneMap.cs`, `using MegaCrit.Sts2.Core.Models;

namespace Yjcm;

/// <summary>Explicit type pairs; never infer resource paths or IDs by stripping a prefix.</summary>
public static class WeiDongZhuoCloneMap
{
    private static readonly IReadOnlyDictionary<Type, Type> OriginalsToClones = new Dictionary<Type, Type>
    {
${names.map(n => `        [typeof(${n})] = typeof(${clone(n)}),`).join('\n')}
    };
    private static readonly IReadOnlyDictionary<Type, Type> ClonesToOriginals =
        OriginalsToClones.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static Type CloneType(Type type) => OriginalsToClones.TryGetValue(type, out var clone) ? clone : type;
    public static Type OriginalType(Type type) => ClonesToOriginals.TryGetValue(type, out var original) ? original : type;
    public static bool IsClone(Type type) => ClonesToOriginals.ContainsKey(type);

    // Called only with canonical entries from ModelDb.AllCards, before combat copies are created.
    public static CardModel ForGeneration(CardModel canonical)
    {
        var type = CloneType(canonical.GetType());
        return type == canonical.GetType() ? canonical : ModelDb.GetById<CardModel>(ModelDb.GetId(type));
    }

    public static string OriginalPortraitPath(Type cloneType)
    {
        if (!ClonesToOriginals.TryGetValue(cloneType, out var originalType))
            throw new ArgumentException("Unregistered clone type", nameof(cloneType));
        var original = ModelDb.GetById<CardModel>(ModelDb.GetId(originalType));
        return original is WuZhugeliangCard card ? card.CustomPortraitPath : original.PortraitPath;
    }
}`);

const locSources = ['cards', 'orbs'].map(table => path.join(sourceRoot, `Yjcm/localization/zhs/${table}.json`));
locSources.forEach(p => sourceFiles.add(p));
const cardLoc = JSON.parse(read(locSources[0]));
const orbLoc = JSON.parse(read(locSources[1]));
function copyLocalization(table, pairs) {
  const result = {};
  for (const [from, to] of pairs) {
    for (const suffix of ['.title', '.description']) assert(from + suffix in table, `Missing localization: ${from + suffix}`);
    for (const [key, value] of Object.entries(table)) if (key.startsWith(from + '.')) result[to + key.slice(from.length)] = value;
  }
  return result;
}
const copiedCardsLoc = copyLocalization(cardLoc, names.map(n => [id(n), id(clone(n))]));
const copiedOrbsLoc = copyLocalization(orbLoc, [[id('ZhangBaSheMaoOrb', 'ORB'), id('WeiDongZhuoZhangBaSheMaoOrb', 'ORB')]]);
const manualCardsLocFile = path.join(stableRoot, 'tools/card-inputs/new-cards.json');
const manualCardsLoc = JSON.parse(read(manualCardsLocFile));
for (const [key, value] of Object.entries(manualCardsLoc)) {
  assert(!(key in copiedCardsLoc), `Manual localization duplicates generated clone key: ${key}`);
  copiedCardsLoc[key] = value;
}
sourceFiles.add(manualCardsLocFile);
// The runtime localization is already unified. These outputs are generation audit data.
emit(`${cardDir}localization/zhs/cards.json`, JSON.stringify(copiedCardsLoc, null, 2));
emit(`${cardDir}localization/zhs/orbs.json`, JSON.stringify(copiedOrbsLoc, null, 2));
emit('Yjcm/localization/zhs/cards.json', JSON.stringify({...cardLoc, ...copiedCardsLoc}, null, 2));
emit('Yjcm/localization/zhs/orbs.json', JSON.stringify({...orbLoc, ...copiedOrbsLoc}, null, 2));

const classification = [];
for (const c of cards.values()) {
  if (!['WuZhugeliangCard', 'CardModel', 'SlotOptionCard'].includes(c.base) || c.abstract) continue;
  let reason;
  if (names.includes(c.name)) reason = '复制';
  else if (c.tags.some(t => ['Wei', 'Shu', 'Wu'].includes(t))) reason = '排除魏蜀吴 tag 武将';
  else reason = excludedSpecial[c.name] ?? (/^(QingBei|SlotOption|HongCe)/.test(c.name) ? '被排除角色专属的选择 token' : null);
  assert(reason, `Unclassified card: ${c.name}`);
  classification.push({ type: c.name, title: cardLoc[id(c.name) + '.title'] ?? c.name, tags: c.tags,
    source: path.relative(workspace, c.file).replaceAll('\\', '/'), inOriginalPoolList: originalPool.has(c.name), reason });
}

// Record external exact-type consumers/producers for review without editing any of them.
const externalReferences = [];
function scan(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name);
    if (entry.isDirectory()) scan(p);
    else if (entry.name.endsWith('.cs')) {
      sourceFiles.add(p);
      const text = read(p), clean = mask(text);
      for (const [index, line] of clean.split('\n').entries()) {
        if (!/(?:\bis\b|typeof|OfType|CreateCard|GetType|ReferenceEquals|Id\.Entry|FromCard)/.test(line)) continue;
        const matches = names.filter(n => new RegExp(`\\b${n}\\b`).test(line));
        if (matches.length) externalReferences.push({ file: path.relative(workspace, p).replaceAll('\\', '/'), line: index + 1, types: matches, code: text.split('\n')[index].trim() });
      }
    }
  }
}
for (const folder of ['Powers', 'Patches', 'Relics', 'Orbs', 'Equipment', 'Extensions']) scan(path.join(sourceRoot, `src/${folder}`));
const snapshot = Object.fromEntries([...sourceFiles].sort().map(p => [path.relative(workspace, p).replaceAll('\\', '/'), hash(p)]));
const manifest = { scope: '群雄+神武将+全部通用基础牌/锦囊/装备；神标签优先，其他魏蜀吴专属及其 tokens 排除',
  variant: 'both', gameApi: ['0.111.0', '0.114.0'],
  mainNamespace: 'Yjcm', frame: 'card_frame_weidongzhuo', energyColor: 'yjcm_dongzhuo',
  primaryColor: '#B98A31', darkColor: '#5A3825', groups, cards: metadata, classification, externalReferences,
  inputHashes: snapshot, additionalModel: 'WeiDongZhuoZhangBaSheMaoOrb',
  localizationCounts: { cards: Object.keys(copiedCardsLoc).length, orbs: Object.keys(copiedOrbsLoc).length },
  buildPerformed: false };
emit(`${cardDir}copy-manifest.json`, JSON.stringify(manifest, null, 2));

emit(`${cardDir}分类清单.md`, `# 威董卓复制卡分类清单

目标 API：0.111.0 stable 与 0.114.0 beta，未编译。依据统一源码 CanonicalTags（不把悬浮提示、效果中的 tag 查询误当作本卡 tag）。神标签优先保留；当前群 tag 武将数量为 ${groups.general.filter(n => cards.get(n).tags.includes('Qun')).length}。

共 ${names.length} 张独立类型：${groups.general.length} 张神将主牌、${names.length - groups.general.length - groups.token.length} 张通用牌、${groups.token.length} 张转换/生成/选择 token。另附 1 个专用丈八蛇矛装备球。

本轮纳入范围按 Cards 的实际类声明确定，并核对原卡池；不会因为原池显式列表漏列雌雄双股剑、神诸葛亮或神马超而漏复制。

| 原类型 | 标题 | CanonicalTags | 原池显式列出 | 决定 |
| --- | --- | --- | --- | --- |
${classification.map(c => `| ${c.type} | ${c.title} | ${c.tags.join(', ') || '无武将势力 tag'} | ${c.inOriginalPoolList ? '是' : '否'} | ${c.reason} |`).join('\n')}

机器可读类型、独立 ID、源码哈希、生成边、外部精确类型依赖见 copy-manifest.json。
`);

function verify() {
  const results = [];
  for (const [rel, expected] of outputs) {
    assert.equal(readRaw(path.join(outputRoot, rel)), expected, `Generated file drift: ${rel}`);
  }
  results.push(`${outputs.size} 个生成文件逐字重现通过`);
  assert.equal(new Set(metadata.map(c => c.cloneId)).size, names.length);
  assert(metadata.every(c => c.cloneId !== c.originalId));
  assert(names.every(n => cards.get(n).tags.includes('ShenJiang') || !cards.get(n).tags.some(t => ['Wei', 'Shu', 'Wu'].includes(t))));
  results.push(`${names.length} 个独立 ID、排除势力、完整注册清单通过`);
  for (const row of metadata) {
    const generated = selectVariant(readRaw(path.join(outputCards, `Generated/${row.cloneType}.cs`)), variant);
    const clean = mask(generated);
    for (const dep of row.generatedTypes) {
      if (aliases[dep]) assert(new RegExp(`\\b${aliases[dep]}\\b`).test(clean), `${row.cloneType} fails to map ${dep}`);
    }
    assert(!/RegisterCard\(typeof\((?:WuZhugeliangCardPool|TokenCardPool)\)/.test(clean));
    const sourceLiterals = [...cards.get(row.originalType).body.matchAll(lexical)].map(m => m[0]).filter(s => s.startsWith('"'));
    // Resource, audio and key literals must all survive even when types are renamed.
    for (const literal of sourceLiterals) assert(generated.includes(literal), `${row.cloneType} changed literal ${literal}`);
    assert.deepEqual(declarations(generated)[0].body.match(/CanonicalTags\s*=>\s*\[[^\]]*\]/)?.[0], cards.get(row.originalType).body.match(/CanonicalTags\s*=>\s*\[[^\]]*\]/)?.[0]);
    for (const suffix of ['.title', '.description']) assert.equal(copiedCardsLoc[row.cloneId + suffix], cardLoc[row.originalId + suffix]);
  }
  results.push('生成/悬浮类型闭包、字符串资源、语音、动态变量键、tags、本地化一致性通过');
  const prior = JSON.parse(read(path.join(outputCards, 'copy-manifest.json')));
  assert.deepEqual(prior.inputHashes, snapshot, 'Original inputs changed since generation');
  results.push(`${Object.keys(snapshot).length} 个输入文件 SHA-256 与生成快照一致`);
  assert(read(path.join(outputCards, 'Compatibility/WeiDongZhuoZhangBaSheMaoOrb.cs')).includes('CreateCard<WeiDongZhuoWuSeSha>'));
  results.push('丈八装备球生成新框无色杀通过');
  for (const name of new Set(metadata.map(c => path.basename(c.source)))) {
    const original = read(path.join(sourceCards, name));
    const beta = selectVariant(readRaw(path.join(sourceCards, name)), 'beta');
    const normalize = s => declarations(s).map(c => canonicalCode(c.body)).join('\n');
    assert.equal(normalize(betaSignatures(original)), normalize(beta), `Beta signature/effect drift: ${name}`);
  }
  results.push('统一源文件两分支的效果与机械 API 适配一致（忽略注释/空白）');
  console.log(JSON.stringify({ status: 'PASS', variant, compiled: false, results }, null, 2));
}

if (process.argv.includes('--check')) verify();
else if (process.argv.includes('--write')) {
  for (const [relative, content] of outputs) {
    const target = path.join(outputRoot, relative);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, content);
  }
  console.log(JSON.stringify({ status: 'WRITTEN', variant, files: outputs.size }, null, 2));
}
else if (process.argv.includes('--patch')) {
  let patch = '*** Begin Patch\n';
  for (const [relative, content] of outputs) {
    const p = path.join(outputRoot, relative).replaceAll('\\', '/');
    if (fs.existsSync(p)) {
      const old = read(p);
      if (old === content) continue;
      patch += `*** Update File: ${p}\n@@\n` + old.trimEnd().split('\n').map(l => '-' + l).join('\n') + '\n';
      patch += content.trimEnd().split('\n').map(l => '+' + l).join('\n') + '\n';
    } else patch += `*** Add File: ${p}\n` + content.trimEnd().split('\n').map(l => '+' + l).join('\n') + '\n';
  }
  console.log(patch + '*** End Patch');
} else console.log(JSON.stringify({ count: names.length, groups, excluded: classification.filter(c => c.reason !== '复制') }, null, 2));
