import test from "node:test";
import assert from "node:assert/strict";
import { buildDashboardData, SIMA_YI_ENCOUNTER_ID } from "../analytics.mjs";

function runEvent({ picked = true, killedBy = null, duration = 600, victory = false, character = "CHARACTER.WU_ZHUGELIANG" } = {}) {
  return {
    event: "run_history.completed",
    timestamp: "2026-08-23T12:00:00Z",
    properties: {
      distinct_id: "install-a",
      game_version: "0.111.0",
      run_time_seconds: duration,
      run_floor_reached: 30,
      run_character_ids: [character],
      run_ascension: 3,
      is_victory: victory,
      payload: {
        applicant_payload: {
          run_history: {
            killed_by_encounter: killedBy,
            rooms: [{ model_id: SIMA_YI_ENCOUNTER_ID, room_type: "boss" }],
            card_choices: [
              { card: { id: "CARD.WU_ZHUGELIANG_TEST" }, was_picked: picked },
              { card: { id: "CARD.NEUTRAL_SKIP" }, was_picked: false },
            ],
          },
        },
      },
    },
  };
}

test("calculates card pick rate, boss outcomes and run durations", () => {
  const result = buildDashboardData({
    runEvents: [runEvent({ picked: true, duration: 600 }), runEvent({ picked: false, killedBy: SIMA_YI_ENCOUNTER_ID, duration: 1200 })],
    sessionEvents: [{ timestamp: "2026-08-23", properties: { distinct_id: "install-a" } }],
    exceptionEvents: [],
  });
  assert.equal(result.runs.total, 2);
  assert.equal(result.runs.averageDurationSeconds, 900);
  assert.equal(result.runs.medianDurationSeconds, 900);
  assert.equal(result.simaYi.encounters, 2);
  assert.equal(result.simaYi.kills, 1);
  assert.equal(result.simaYi.deaths, 1);
  assert.equal(result.simaYi.killRate, 0.5);
  const card = result.cards.find((entry) => entry.id === "CARD.WU_ZHUGELIANG_TEST");
  assert.deepEqual(card, { id: "CARD.WU_ZHUGELIANG_TEST", offered: 2, picked: 1, pickRate: 0.5 });
});

test("separates Wu Zhugeliang and Wei Dong Zhuo runs", () => {
  const wu = runEvent({ character: "CHARACTER.YJCM_CHARACTER_WU_ZHUGELIANG" });
  const dongZhuo = runEvent({
    character: "CHARACTER.YJCM_CHARACTER_WEI_DONG_ZHUO",
    duration: 1200,
  });
  dongZhuo.properties.payload.applicant_payload.run_history.card_choices = [
    { card: { id: "CARD.YJCM_CARD_WEI_DONG_ZHUO_TEST" }, was_picked: true },
  ];

  const wuOnly = buildDashboardData({ runEvents: [wu, dongZhuo], characterFilter: "wu_zhugeliang" });
  const dongZhuoOnly = buildDashboardData({ runEvents: [wu, dongZhuo], characterFilter: "wei_dong_zhuo" });

  assert.equal(wuOnly.runs.total, 1);
  assert.equal(wuOnly.runs.averageDurationSeconds, 600);
  assert.equal(wuOnly.cards.some((card) => card.id.includes("WEI_DONG_ZHUO")), false);
  assert.equal(dongZhuoOnly.runs.total, 1);
  assert.equal(dongZhuoOnly.runs.averageDurationSeconds, 1200);
  assert.equal(dongZhuoOnly.cards[0].id, "CARD.YJCM_CARD_WEI_DONG_ZHUO_TEST");
  assert.equal(dongZhuoOnly.characterLabel, "威董卓");
});

test("separates Wei Cao Cao runs and keeps co-op teams visible", () => {
  const caoCao = runEvent({
    character: "CHARACTER.YJCM_CHARACTER_WEI_CAO_CAO",
    duration: 900,
  });
  caoCao.properties.payload.applicant_payload.run_history.card_choices = [
    { card: { id: "CARD.YJCM_CARD_WEI_CAO_CAO_TEST" }, was_picked: true },
  ];
  const only = buildDashboardData({ runEvents: [caoCao], characterFilter: "wei_caocao" });
  assert.equal(only.runs.total, 1);
  assert.equal(only.runs.averageDurationSeconds, 900);
  assert.equal(only.characterLabel, "威曹操");
  assert.equal(only.cards[0].id, "CARD.YJCM_CARD_WEI_CAO_CAO_TEST");

  const mixed = runEvent({
    character: "CHARACTER.YJCM_CHARACTER_WU_ZHUGELIANG",
  });
  mixed.properties.run_character_ids = [
    "CHARACTER.YJCM_CHARACTER_WU_ZHUGELIANG",
    "CHARACTER.YJCM_CHARACTER_WEI_CAO_CAO",
  ];
  assert.equal(buildDashboardData({ runEvents: [mixed], characterFilter: "wei_caocao" }).runs.total, 1);
  assert.equal(buildDashboardData({ runEvents: [mixed], characterFilter: "wu_zhugeliang" }).runs.total, 1);
});

test("keeps co-op runs visible for every selected team character", () => {
  const event = runEvent();
  event.properties.run_character_ids = [
    "CHARACTER.YJCM_CHARACTER_WU_ZHUGELIANG",
    "CHARACTER.YJCM_CHARACTER_WEI_DONG_ZHUO",
  ];
  assert.equal(buildDashboardData({ runEvents: [event], characterFilter: "wu_zhugeliang" }).runs.total, 1);
  assert.equal(buildDashboardData({ runEvents: [event], characterFilter: "wei_dong_zhuo" }).runs.total, 1);
});

test("keeps legacy Sima Yi telemetry after the YJCM id migration", () => {
  const legacyId = "ENCOUNTER.WU_ZHUGELIANG_ENCOUNTER_MOU_SIMA_YI_BOSS_ENCOUNTER";
  const current = runEvent({ victory: true });
  const legacy = runEvent({ killedBy: legacyId });
  legacy.properties.payload.applicant_payload.run_history.rooms = [{ model_id: legacyId, room_type: "boss" }];
  const result = buildDashboardData({ runEvents: [current, legacy] });
  assert.equal(result.simaYi.encounters, 2);
  assert.equal(result.simaYi.kills, 1);
  assert.equal(result.simaYi.deaths, 1);
});

test("accepts JSON encoded PostHog payloads", () => {
  const event = runEvent();
  event.properties.payload = JSON.stringify(event.properties.payload);
  const result = buildDashboardData({ runEvents: [event] });
  assert.equal(result.simaYi.kills, 1);
  assert.equal(result.cards.find((entry) => entry.id === "CARD.WU_ZHUGELIANG_TEST").picked, 1);
});

test("infers deaths from the current run-history schema", () => {
  const event = runEvent({ picked: false, victory: false });
  const run = event.properties.payload.applicant_payload.run_history;
  delete run.rooms;
  run.map_point_history = [[
    { rooms: [{ model_id: SIMA_YI_ENCOUNTER_ID, room_type: "boss", turns_taken: 3 }] },
  ]];

  const result = buildDashboardData({ runEvents: [event] });
  assert.equal(result.simaYi.encounters, 1);
  assert.equal(result.simaYi.kills, 0);
  assert.equal(result.simaYi.deaths, 1);
  assert.equal(result.simaYi.deathRate, 1);
});

test("does not count a later death as a Sima Yi death", () => {
  const event = runEvent({ picked: false, victory: false });
  const run = event.properties.payload.applicant_payload.run_history;
  delete run.rooms;
  run.map_point_history = [[
    { rooms: [{ model_id: SIMA_YI_ENCOUNTER_ID, room_type: "boss" }] },
    { rooms: [{ model_id: "ENCOUNTER.QUEEN_BOSS", room_type: "boss" }] },
  ]];

  const result = buildDashboardData({ runEvents: [event] });
  assert.equal(result.simaYi.encounters, 1);
  assert.equal(result.simaYi.kills, 1);
  assert.equal(result.simaYi.deaths, 0);
});

test("reads card choices nested in map point player stats", () => {
  const event = runEvent();
  event.properties.payload.applicant_payload.run_history = {
    map_point_history: [
      [
        {
          player_stats: [
            {
              card_choices: [
                { card: { id: "CARD.NESTED" }, was_picked: true },
                { card: { id: "CARD.NESTED" }, was_picked: false },
              ],
            },
          ],
        },
      ],
    ],
  };
  const result = buildDashboardData({ runEvents: [event] });
  assert.deepEqual(result.cards.find((entry) => entry.id === "CARD.NESTED"), {
    id: "CARD.NESTED",
    offered: 2,
    picked: 1,
    pickRate: 0.5,
  });
});

test("attaches localized card names when provided", () => {
  const result = buildDashboardData({
    runEvents: [runEvent()],
    cardNames: { "CARD.WU_ZHUGELIANG_CARD_HUO_GONG": "火攻" },
  });
  const card = result.cards.find((entry) => entry.id === "CARD.WU_ZHUGELIANG_TEST");
  assert.equal(card.name, null);
  const named = buildDashboardData({
    runEvents: [
      {
        ...runEvent(),
        properties: {
          ...runEvent().properties,
          payload: {
            applicant_payload: {
              run_history: {
                card_choices: [{ card: { id: "CARD.WU_ZHUGELIANG_CARD_HUO_GONG" }, was_picked: true }],
              },
            },
          },
        },
      },
    ],
    cardNames: { "CARD.WU_ZHUGELIANG_CARD_HUO_GONG": "火攻" },
  });
  assert.equal(named.cards[0].name, "火攻");
});
