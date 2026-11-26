export const SIMA_YI_ENCOUNTER_ID =
  "ENCOUNTER.YJCM_ENCOUNTER_MOU_SIMA_YI_BOSS_ENCOUNTER";
export const SIMA_YI_MONSTER_ID =
  "MONSTER.YJCM_MONSTER_MOU_SIMA_YI";
const LEGACY_SIMA_YI_IDS = new Set([
  "ENCOUNTER.WU_ZHUGELIANG_ENCOUNTER_MOU_SIMA_YI_BOSS_ENCOUNTER",
  "MONSTER.WU_ZHUGELIANG_MONSTER_MOU_SIMA_YI",
]);

export const CHARACTER_FILTERS = Object.freeze({
  all: "全部角色",
  wu_zhugeliang: "武诸葛亮",
  wei_dong_zhuo: "威董卓",
  wei_caocao: "威曹操",
});

function isSimaYiId(value) {
  return value === SIMA_YI_ENCOUNTER_ID
    || value === SIMA_YI_MONSTER_ID
    || LEGACY_SIMA_YI_IDS.has(value);
}

function parseJson(value) {
  if (typeof value !== "string") return value;
  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}

function asObject(value) {
  const parsed = parseJson(value);
  return parsed && typeof parsed === "object" ? parsed : {};
}

function asArray(value) {
  const parsed = parseJson(value);
  return Array.isArray(parsed) ? parsed : [];
}

function finiteNumber(...values) {
  for (const value of values) {
    const number = Number(value);
    if (Number.isFinite(number)) return number;
  }
  return null;
}

function ratio(numerator, denominator) {
  return denominator > 0 ? numerator / denominator : null;
}

function average(values) {
  return values.length
    ? values.reduce((sum, value) => sum + value, 0) / values.length
    : null;
}

function median(values) {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2
    ? sorted[middle]
    : (sorted[middle - 1] + sorted[middle]) / 2;
}

function increment(map, key, amount = 1) {
  const label = key == null || key === "" ? "未知" : String(key);
  map.set(label, (map.get(label) ?? 0) + amount);
}

function sortedEntries(map, limit = Infinity) {
  return [...map.entries()]
    .map(([name, count]) => ({ name, count }))
    .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name))
    .slice(0, limit);
}

function containsSimaYi(value) {
  if (isSimaYiId(value)) return true;
  if (Array.isArray(value)) return value.some(containsSimaYi);
  if (!value || typeof value !== "object") return false;
  return Object.values(value).some(containsSimaYi);
}

export function extractRunHistory(event) {
  const properties = asObject(event?.properties);
  const payload = asObject(properties.payload);
  const applicantPayload = asObject(
    payload.applicant_payload ?? payload.applicantPayload,
  );
  const candidates = [
    applicantPayload.run_history,
    applicantPayload.runHistory,
    payload.run_history,
    payload.runHistory,
    properties.run_history,
    properties.runHistory,
  ];
  for (const candidate of candidates) {
    const parsed = parseJson(candidate);
    if (parsed && typeof parsed === "object") return parsed;
  }
  return {};
}

function getCardId(choice) {
  const card = asObject(choice?.card);
  return (
    card.id ??
    card.model_id ??
    choice?.card_id ??
    choice?.cardId ??
    choice?.model_id ??
    null
  );
}

// STS2 的 run-history 将卡牌奖励记录放在各地图节点的 player_stats 中，
// 不是固定放在 run_history 根节点。递归收集两种命名形式，兼容不同版本。
function collectCardChoices(value, result = []) {
  if (!value || typeof value !== "object") return result;
  if (Array.isArray(value)) {
    for (const item of value) collectCardChoices(item, result);
    return result;
  }
  for (const [key, child] of Object.entries(value)) {
    if (key === "card_choices" || key === "cardChoices") {
      for (const choice of asArray(child)) result.push(choice);
      continue;
    }
    collectCardChoices(child, result);
  }
  return result;
}

function getEventDay(event) {
  const source = event?.timestamp ?? event?.created_at ?? event?.properties?.occurred_at_utc;
  const date = new Date(source);
  return Number.isNaN(date.getTime()) ? "未知日期" : date.toISOString().slice(0, 10);
}

function getDistinctId(event) {
  const properties = asObject(event?.properties);
  return properties.distinct_id ?? properties.anonymous_install_id ?? event?.distinct_id ?? null;
}

function getModelId(value) {
  if (typeof value === "string") return value;
  if (!value || typeof value !== "object") return null;
  return value.id ?? value.model_id ?? value.modelId ?? value.encounter_id ?? value.encounterId ?? null;
}

function getRunCharacterIds(properties, run) {
  const candidates = [
    properties.run_character_ids,
    properties.runCharacterIds,
    run.character_ids,
    run.characterIds,
  ];
  for (const candidate of candidates) {
    const values = asArray(candidate);
    if (values.length) return values.map(getModelId).filter(Boolean);
  }
  return [
    properties.run_character_id,
    properties.runCharacterId,
    properties.character_id,
    properties.characterId,
    run.character_id,
    run.characterId,
    run.character,
  ].map(getModelId).filter(Boolean);
}

function characterFamily(value) {
  const id = String(value ?? "").toUpperCase();
  if (id.includes("WEI_DONG_ZHUO")) return "wei_dong_zhuo";
  if (id.includes("WEI_CAO_CAO")) return "wei_caocao";
  if (id.includes("WU_ZHUGELIANG") || id === "CHARACTER.YJCM") return "wu_zhugeliang";
  return "other";
}

export function runMatchesCharacter(event, characterFilter = "all") {
  if (!(characterFilter in CHARACTER_FILTERS)) return false;
  if (characterFilter === "all") return true;
  const properties = asObject(event?.properties);
  const run = extractRunHistory(event);
  return getRunCharacterIds(properties, run).some(
    (characterId) => characterFamily(characterId) === characterFilter,
  );
}

// 新版 STS2 的跑局历史不再提供 killed_by_encounter，
// 只能根据未胜利且最后访问的房间是司马懿来还原被击杀场次。
function getLastVisitedRoomId(run) {
  const history = asArray(run?.map_point_history);
  let lastRoomId = null;
  for (const layer of history) {
    for (const point of asArray(layer)) {
      for (const room of asArray(point?.rooms)) {
        const roomId = getModelId(room?.model_id ?? room?.modelId ?? room);
        if (roomId) lastRoomId = roomId;
      }
    }
  }
  return lastRoomId;
}

export function buildDashboardData({ runEvents = [], sessionEvents = [], exceptionEvents = [], cardNames, characterFilter = "all" }) {
  if (!(characterFilter in CHARACTER_FILTERS)) {
    throw new Error(`不支持的角色筛选：${characterFilter}`);
  }
  const filteredRunEvents = runEvents.filter((event) => runMatchesCharacter(event, characterFilter));
  const cards = new Map();
  const durations = [];
  const floors = [];
  const versions = new Map();
  const characters = new Map();
  const ascensions = new Map();
  const deathEncounters = new Map();
  const trend = new Map();
  const installs = new Set();
  let victories = 0;
  let simaEncounters = 0;
  let simaKills = 0;
  let simaDeaths = 0;

  const dayBucket = (day) => {
    if (!trend.has(day)) {
      trend.set(day, { day, runs: 0, sessions: 0, exceptions: 0, simaEncounters: 0 });
    }
    return trend.get(day);
  };

  for (const event of filteredRunEvents) {
    const properties = asObject(event?.properties);
    const run = extractRunHistory(event);
    const day = getEventDay(event);
    dayBucket(day).runs += 1;

    const distinctId = getDistinctId(event);
    if (distinctId) installs.add(String(distinctId));

    const duration = finiteNumber(properties.run_time_seconds, run.run_time_seconds, run.run_time);
    if (duration != null && duration >= 0) durations.push(duration);

    const floor = finiteNumber(properties.run_floor_reached, run.floor_reached, run.floor);
    if (floor != null && floor >= 0) floors.push(floor);

    const victory = properties.is_victory === true || run.is_victory === true || run.victory === true;
    if (victory) victories += 1;

    increment(versions, properties.game_version ?? run.game_version);
    for (const character of getRunCharacterIds(properties, run)) {
      increment(characters, character);
    }
    increment(ascensions, properties.run_ascension ?? run.ascension);

    const killedBy = getModelId(
      run.killed_by_encounter ?? run.killedByEncounter
        ?? properties.killed_by_encounter ?? properties.killedByEncounter,
    );
    if (killedBy) increment(deathEncounters, killedBy);
    const lastVisitedRoomId = getLastVisitedRoomId(run);
    const simaDeath = isSimaYiId(killedBy)
      || (!victory && isSimaYiId(lastVisitedRoomId));
    const simaEncounter = simaDeath || containsSimaYi(run.rooms ?? run.visited_rooms ?? run);
    if (simaEncounter) {
      simaEncounters += 1;
      dayBucket(day).simaEncounters += 1;
      if (simaDeath) simaDeaths += 1;
      else simaKills += 1;
    }

    for (const choice of collectCardChoices(run)) {
      const cardId = getCardId(choice);
      if (!cardId) continue;
      const key = String(cardId);
      const current = cards.get(key) ?? { id: key, offered: 0, picked: 0 };
      if (cardNames) current.name = cardNames[key] ?? null;
      current.offered += 1;
      if (choice.was_picked === true || choice.wasPicked === true) current.picked += 1;
      cards.set(key, current);
    }
  }

  for (const event of sessionEvents) {
    dayBucket(getEventDay(event)).sessions += 1;
    const distinctId = getDistinctId(event);
    if (distinctId) installs.add(String(distinctId));
  }
  for (const event of exceptionEvents) {
    dayBucket(getEventDay(event)).exceptions += 1;
  }

  return {
    generatedAt: new Date().toISOString(),
    characterFilter,
    characterLabel: CHARACTER_FILTERS[characterFilter],
    runs: {
      total: filteredRunEvents.length,
      victories,
      winRate: ratio(victories, filteredRunEvents.length),
      averageDurationSeconds: average(durations),
      medianDurationSeconds: median(durations),
      averageFloorReached: average(floors),
    },
    simaYi: {
      encounters: simaEncounters,
      kills: simaKills,
      deaths: simaDeaths,
      killRate: ratio(simaKills, simaEncounters),
      deathRate: ratio(simaDeaths, simaEncounters),
    },
    usage: {
      uniqueInstalls: installs.size,
      sessions: sessionEvents.length,
      exceptions: exceptionEvents.length,
      exceptionsPerSession: ratio(exceptionEvents.length, sessionEvents.length),
    },
    cards: [...cards.values()]
      .map((card) => ({ ...card, pickRate: ratio(card.picked, card.offered) }))
      .sort((a, b) => b.offered - a.offered || a.id.localeCompare(b.id)),
    trend: [...trend.values()].sort((a, b) => a.day.localeCompare(b.day)),
    breakdowns: {
      versions: sortedEntries(versions),
      characters: sortedEntries(characters),
      ascensions: sortedEntries(ascensions),
      deathEncounters: sortedEntries(deathEncounters, 12),
    },
  };
}
