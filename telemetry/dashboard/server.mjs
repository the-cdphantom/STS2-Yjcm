import { spawn } from "node:child_process";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, extname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildDashboardData, CHARACTER_FILTERS } from "./analytics.mjs";
import { createQueryClient, readVerifiedEvents } from "./posthog.mjs";
import { createSnapshotCache } from "./snapshot-cache.mjs";

const dashboardDir = dirname(fileURLToPath(import.meta.url));
const publicDir = join(dashboardDir, "public");
const configPath = join(dashboardDir, "config.local.json");
const hostName = "127.0.0.1";
const port = Number(process.env.WUZHUGELIANG_DASHBOARD_PORT || 4173);
const mimeTypes = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".svg": "image/svg+xml",
};

async function loadConfig() {
  let config;
  try {
    config = JSON.parse(await readFile(configPath, "utf8"));
  } catch {
    throw new Error("尚未配置仪表盘。请双击遥测仪表盘启动器，或运行 node dashboard/setup.mjs");
  }
  const url = new URL(config.host);
  if (url.protocol !== "https:") throw new Error("配置中的 PostHog 地址必须使用 HTTPS");
  if (!/^\d+$/.test(String(config.projectId))) throw new Error("配置中的 Project ID 无效");
  if (!config.personalApiKey) throw new Error("配置中缺少 Personal API key");
  return { ...config, host: url.origin };
}

async function loadCardNames() {
  const candidates = [
    join(dashboardDir, "..", "..", "mod", "Yjcm", "localization", "zhs", "cards.json"),
    ...["cards", "wei-design-cards", "wei-simple-cards", "wei-cycle-cards",
      "wei-cao-zhang-cards", "wei-gui-xin-cards", "wei-reinforcement-cards"].map(name =>
      join(dashboardDir, "..", "..", "mod", "Experimental", "WeiCaoCao", "localization", name + ".json")),
  ];
  const names = {};
  for (const file of candidates) {
    try {
      const table = JSON.parse(await readFile(file, "utf8"));
      for (const [key, value] of Object.entries(table)) {
        if (key.endsWith(".title") && typeof value === "string") {
          names[`CARD.${key.slice(0, -6)}`] = value;
        }
      }
    } catch {
      // Some development localization files may be absent in a source checkout.
    }
  }
  return names;
}

const getDashboard = createSnapshotCache(async (cacheKey) => {
  const [rawDays, characterFilter] = String(cacheKey).split("|");
  const days = Number(rawDays);
  const config = await loadConfig();
  const cardNames = await loadCardNames();
  const { events, verification } = await readVerifiedEvents(createQueryClient(config), { days });
  return {
    ...buildDashboardData({
      runEvents: events["run_history.completed"],
      sessionEvents: events.session_start,
      exceptionEvents: events.exception,
      cardNames,
      characterFilter,
    }),
    rangeDays: days,
    verification,
    warnings: [],
  };
});

function sendJson(response, status, body) {
  response.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
  });
  response.end(JSON.stringify(body));
}

async function serveStatic(pathname, response) {
  const files = {
    "/": "index.html",
    "/index.html": "index.html",
    "/app.js": "app.js",
    "/styles.css": "styles.css",
  };
  const file = files[pathname];
  if (!file) return false;
  const content = await readFile(join(publicDir, file));
  response.writeHead(200, {
    "Content-Type": mimeTypes[extname(file)] ?? "application/octet-stream",
    "Cache-Control": "no-cache",
    "X-Content-Type-Options": "nosniff",
    "Content-Security-Policy": "default-src 'self'; style-src 'self'; script-src 'self'; connect-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'",
  });
  response.end(content);
  return true;
}

const server = createServer(async (request, response) => {
  try {
    const requestUrl = new URL(request.url, `http://${hostName}:${port}`);
    if (request.method !== "GET") {
      sendJson(response, 405, { error: "只允许 GET 请求" });
      return;
    }
    if (requestUrl.pathname === "/health") {
      response.writeHead(200, { "Content-Type": "text/plain", "Cache-Control": "no-store" });
      response.end("yjcm-telemetry-dashboard\n");
      return;
    }
    if (requestUrl.pathname === "/api/dashboard") {
      const rawDays = Number(requestUrl.searchParams.get("days") ?? 30);
      const days = [0, 7, 30, 90, 365].includes(rawDays) ? rawDays : 30;
      const requestedCharacter = requestUrl.searchParams.get("character") ?? "all";
      const character = requestedCharacter in CHARACTER_FILTERS ? requestedCharacter : "all";
      sendJson(response, 200, await getDashboard(`${days}|${character}`));
      return;
    }
    if (await serveStatic(requestUrl.pathname, response)) return;
    sendJson(response, 404, { error: "未找到" });
  } catch (error) {
    sendJson(response, 500, { error: error.message });
  }
});

server.on("error", (error) => {
  console.error(`无法启动仪表盘（${hostName}:${port}）：${error.code === "EADDRINUSE" ? "端口已被其他程序占用" : error.message}`);
  process.exitCode = 1;
});

server.listen(port, hostName, () => {
  const localUrl = `http://${hostName}:${port}`;
  console.log(`一将成名遥测仪表盘已启动：${localUrl}`);
  console.log("按 Ctrl+C 停止；关闭网页不会停止本地服务。\n");
  if (process.argv.includes("--open") && process.platform === "win32") {
    const child = spawn(process.env.ComSpec || "cmd.exe", ["/d", "/c", `start "" ${localUrl}`], {
      windowsVerbatimArguments: true,
      detached: true,
      stdio: "ignore",
    });
    const showManualUrl = () => console.error(`浏览器未能自动打开，请手动访问 ${localUrl}`);
    child.on("error", showManualUrl);
    child.on("exit", (code) => { if (code !== 0) showManualUrl(); });
    child.unref();
  }
});
