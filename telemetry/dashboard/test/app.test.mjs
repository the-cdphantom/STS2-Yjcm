import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createContext, runInContext } from "node:vm";

const source = await readFile(new URL("../public/app.js", import.meta.url), "utf8");
function harness() {
  const elements = new Map();
  const requests = [];
  const rendered = [];
  const node = (selector) => {
    if (!elements.has(selector)) elements.set(selector, { value: selector === "#character" ? "all" : "30", textContent: "", className: "", addEventListener() {} });
    return elements.get(selector);
  };
  const context = createContext({
    document: { querySelector: node },
    fetch: (url) => new Promise(resolve => requests.push({ url, resolve })),
    record: data => rendered.push(data),
  });
  runInContext(source, context);
  // Focus these tests on refresh/error/range behavior, independent of chart DOM.
  runInContext("render = data => { state.data = data; record(data); };", context);
  const respond = async (index, body, ok = true) => {
    requests[index].resolve({ ok, status: ok ? 200 : 500, json: async () => body });
    await new Promise(resolve => setImmediate(resolve));
  };
  return { node, rendered, requests, respond, load: () => runInContext("load()", context) };
}
const complete = days => ({ rangeDays: days, characterFilter: "all", characterLabel: "全部角色", verification: { complete: true }, generatedAt: "2026-09-02T12:00:00Z", runs: { total: 132 }, warnings: [] });

test("page never renders an unverified partial response", async () => {
  const h = harness();
  await h.respond(0, { rangeDays: 30, runs: { total: 5 } });
  assert.equal(h.rendered.length, 0);
  assert.match(h.node("#status").textContent, /尚无完整数据/);
});

test("refresh failure preserves displayed data and labels its original range", async () => {
  const h = harness();
  await h.respond(0, complete(30));
  h.node("#range").value = "7";
  const pending = h.load();
  await h.respond(1, { error: "timeout" }, false);
  await pending;
  assert.equal(h.rendered.length, 1);
  assert.match(h.node("#status").textContent, /最近 30 天/);
  assert.match(h.node("#status").textContent, /仍显示上次/);
});

test("slow old range requests cannot replace the latest selected range", async () => {
  const h = harness();
  h.node("#range").value = "0";
  const pending = h.load();
  await h.respond(1, complete(0));
  await pending;
  await h.respond(0, complete(30));
  assert.deepEqual(h.rendered.map(data => data.rangeDays), [0]);
  assert.match(h.node("#status").textContent, /全部数据/);
});

test("stale complete snapshots have a visible warning", async () => {
  const h = harness();
  await h.respond(0, { ...complete(30), stale: true, warnings: ["刷新失败"] });
  assert.equal(h.rendered.length, 1);
  assert.equal(h.node("#status").className, "status error");
  assert.match(h.node("#status").textContent, /上次完整数据/);
});

test("selected character is sent to the local dashboard API", async () => {
  const h = harness();
  h.node("#character").value = "wei_dong_zhuo";
  const pending = h.load();
  assert.match(h.requests[1].url, /character=wei_dong_zhuo/);
  await h.respond(1, { ...complete(30), characterFilter: "wei_dong_zhuo", characterLabel: "威董卓" });
  await pending;
  assert.match(h.node("#status").textContent, /威董卓/);
});

test("Wei Cao Cao is available as a dashboard character filter", async () => {
  const h = harness();
  h.node("#character").value = "wei_caocao";
  const pending = h.load();
  assert.match(h.requests[1].url, /character=wei_caocao/);
  await h.respond(1, { ...complete(30), characterFilter: "wei_caocao", characterLabel: "威曹操" });
  await pending;
  assert.match(h.node("#status").textContent, /威曹操/);
});
