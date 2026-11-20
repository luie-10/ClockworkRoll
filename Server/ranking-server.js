// CLOCKWORK ROLL — 무한 모드 랭킹 서버
//
// 실행:   node Server/ranking-server.js                 (기본 포트 8787)
// 환경 변수:
//   PORT                 들을 포트 (기본 8787)
//   HOST                 들을 주소 (기본 0.0.0.0)
//   RANK_SECRET          게임과 같이 쓰는 서명 키 (게임의 Ranking.cs와 같아야 한다)
//   MIN_LOOP_SECONDS     한 회차를 넘기는 데 걸려야 하는 최소 시간 (기본 60초 — 이보다 빠르면 거부)
//   TRUST_PROXY=1        앞에 HTTPS 프록시(Caddy · Cloudflare 등)가 있을 때 X-Forwarded-For로 접속 주소를 읽는다
//   DATA_DIR             기록을 둘 폴더 (기본 Server/data)
//
// ── 위조 막기 ──
// 점수를 아무 숫자로나 올릴 수 없게, 기록은 "판(run)" 단위로만 오른다:
//   1) 판 시작   POST /api/endless/run/start       { id, name, sig }          → { runId, nonce }
//   2) 회차 통과 POST /api/endless/run/checkpoint  { runId, loop, sig }       → { nonce, best, rank }
//        · loop는 반드시 직전 회차 + 1 (건너뛰기 불가)
//        · 직전 회차(또는 시작)로부터 MIN_LOOP_SECONDS 이상 지나야 한다 (실제로 플레이한 속도보다 빨리 오를 수 없다)
//        · sig = HMAC-SHA256(키, "checkpoint|runId|loop|nonce") — nonce는 매번 바뀐다 (같은 요청을 다시 보내도 안 먹힌다)
//   3) 판 끝     POST /api/endless/run/end         { runId, sig }             → { rank, best, total }
// 한 플레이어는 동시에 한 판만 (새 판을 시작하면 이전 판은 닫힌다). 판은 24시간이 지나면 버린다.
// 서명 키는 게임 안에도 들어 있으므로 완벽한 비밀은 아니다 — 그래서 서명 + 회차 순서 + 시간 하한 + 일회용 nonce +
// 접속 주소별 횟수 제한을 겹쳐 둔다. 키를 뽑아낸 사람도 "실제로 플레이하는 속도"보다 빨리는 기록을 올릴 수 없다.
//
// ── 읽기 ──
//   GET /api/health                        → { ok: true }
//   GET /api/endless/top?limit=20&id=...   → { entries: [{ rank, name, loop }], me, total }
//   POST /api/player/name { id, name, sig } → { ok }   (이름만 바꾼다)

const http = require("http");
const https = require("https");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const { URL } = require("url");

const PORT = parseInt(process.env.PORT || "8787", 10);
const HOST = process.env.HOST || "0.0.0.0";
const SECRET = process.env.RANK_SECRET || "4S_zgTNx1Sbt0WH9INUBgqlIfdXnN2WS2ACpe4HzQnU";
const MIN_LOOP_MS = Math.max(0, parseFloat(process.env.MIN_LOOP_SECONDS || "60")) * 1000;
const TRUST_PROXY = process.env.TRUST_PROXY === "1";
const DATA_DIR = process.env.DATA_DIR || path.join(__dirname, "data");
const DATA_FILE = path.join(DATA_DIR, "endless.json");
const RUN_TTL_MS = 24 * 3600 * 1000;
const MAX_LOOP = 9999;
const MAX_NAME = 16;

// ─────────────────────────── 저장소 ───────────────────────────

/** id → { id, name, loop, at } — 플레이어마다 최고 기록 */
let players = new Map();
/** runId → { runId, id, name, startedAt, lastAt, loop, nonce } — 진행 중인 판 (메모리에만) */
const runs = new Map();
/** 플레이어 id → 진행 중인 runId */
const runOf = new Map();

function load() {
  try {
    const raw = JSON.parse(fs.readFileSync(DATA_FILE, "utf8"));
    players = new Map((raw.players || []).map((p) => [p.id, p]));
    console.log(`기록 ${players.size}개를 불러왔다`);
  } catch (e) {
    if (e.code !== "ENOENT") console.warn("기록 파일을 읽지 못했다 — 빈 상태로 시작:", e.message);
  }
}

let saveTimer = null;
function saveSoon() {
  if (saveTimer) return;
  saveTimer = setTimeout(() => {
    saveTimer = null;
    fs.mkdirSync(DATA_DIR, { recursive: true });
    const tmp = DATA_FILE + ".tmp";
    fs.writeFileSync(tmp, JSON.stringify({ players: [...players.values()] }, null, 1));
    fs.renameSync(tmp, DATA_FILE); // 쓰는 도중 꺼져도 파일이 깨지지 않게
  }, 300);
}

function sorted() {
  return [...players.values()].filter((p) => p.loop > 0).sort((a, b) => b.loop - a.loop || a.at - b.at);
}

function rankOf(id) {
  return sorted().findIndex((p) => p.id === id) + 1; // 0 = 아직 순위 없음
}

// 오래된 판 치우기
setInterval(() => {
  const now = Date.now();
  for (const [runId, r] of runs) {
    if (now - r.startedAt > RUN_TTL_MS) { runs.delete(runId); if (runOf.get(r.id) === runId) runOf.delete(r.id); }
  }
}, 10 * 60 * 1000).unref();

// ─────────────────────────── 검사 ───────────────────────────

function cleanName(name) {
  if (typeof name !== "string") return null;
  const n = name.replace(/[\u0000-\u001f\u007f<>]/g, "").trim().slice(0, MAX_NAME);
  return n.length > 0 ? n : null;
}

function cleanId(id) {
  return typeof id === "string" && /^[A-Za-z0-9-]{8,64}$/.test(id) ? id : null;
}

function sign(message) {
  return crypto.createHmac("sha256", SECRET).update(message, "utf8").digest("hex");
}

function verify(message, sig) {
  if (typeof sig !== "string" || !/^[0-9a-f]{64}$/.test(sig)) return false;
  const a = Buffer.from(sign(message), "hex"), b = Buffer.from(sig, "hex");
  return crypto.timingSafeEqual(a, b);
}

const newToken = () => crypto.randomBytes(16).toString("hex");

// 접속 주소별 횟수 제한 (종류마다 따로)
const hits = new Map();
function limited(ip, kind, max, windowMs) {
  const key = kind + "|" + ip, now = Date.now();
  const list = (hits.get(key) || []).filter((t) => now - t < windowMs);
  list.push(now);
  hits.set(key, list);
  return list.length > max;
}
setInterval(() => hits.clear(), 3600 * 1000).unref();

function clientIp(req) {
  if (TRUST_PROXY) {
    const fwd = req.headers["cf-connecting-ip"] || (req.headers["x-forwarded-for"] || "").split(",")[0].trim();
    if (fwd) return fwd;
  }
  return req.socket.remoteAddress || "?";
}

// ─────────────────────────── HTTP ───────────────────────────

function send(res, status, body) {
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
  });
  res.end(status === 204 ? "" : JSON.stringify(body));
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    req.on("data", (chunk) => {
      chunks.push(chunk);
      size += chunk.length;
      if (size > 4096) { reject(new Error("too large")); req.destroy(); }
    });
    // 바이트로 모았다가 한 번에 UTF-8로 — 한글이 조각 경계에서 깨지지 않게
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    req.on("error", reject);
  });
}

async function json(req) {
  try { return JSON.parse(await readBody(req)); } catch { return null; }
}

const routes = {
  "GET /api/health": async () => [200, { ok: true }],

  "GET /api/endless/top": async (req, url) => {
    const limit = Math.min(100, Math.max(1, parseInt(url.searchParams.get("limit") || "20", 10) || 20));
    const id = cleanId(url.searchParams.get("id"));
    const all = sorted();
    const entries = all.slice(0, limit).map((p, i) => ({ rank: i + 1, name: p.name, loop: p.loop }));
    const idx = id ? all.findIndex((p) => p.id === id) : -1;
    const me = idx >= 0 ? { rank: idx + 1, name: all[idx].name, loop: all[idx].loop } : null;
    return [200, { entries, me, total: all.length }];
  },

  "POST /api/endless/run/start": async (req, url, ip) => {
    if (limited(ip, "start", 20, 3600 * 1000)) return [429, { error: "too many runs" }];
    const b = await json(req);
    const id = b && cleanId(b.id), name = b && cleanName(b.name);
    if (!id || !name) return [400, { error: "bad request" }];
    if (!verify(`start|${id}|${name}`, b.sig)) return [403, { error: "bad signature" }];

    const old = runOf.get(id);
    if (old) runs.delete(old); // 한 사람은 한 판만
    const run = { runId: newToken(), id, name, startedAt: Date.now(), lastAt: Date.now(), loop: 0, nonce: newToken() };
    runs.set(run.runId, run);
    runOf.set(id, run.runId);
    return [200, { runId: run.runId, nonce: run.nonce }];
  },

  "POST /api/endless/run/checkpoint": async (req, url, ip) => {
    if (limited(ip, "checkpoint", 60, 3600 * 1000)) return [429, { error: "too many requests" }];
    const b = await json(req);
    const run = b && typeof b.runId === "string" ? runs.get(b.runId) : null;
    if (!run) return [404, { error: "no such run" }];
    const loop = Number(b.loop);
    if (!Number.isInteger(loop) || loop !== run.loop + 1 || loop > MAX_LOOP) return [409, { error: "loop out of order" }];
    if (!verify(`checkpoint|${run.runId}|${loop}|${run.nonce}`, b.sig)) return [403, { error: "bad signature" }];
    const now = Date.now();
    if (now - run.lastAt < MIN_LOOP_MS) {
      console.warn(`[거부] ${run.name} — ${loop}회차가 너무 빠르다 (${((now - run.lastAt) / 1000).toFixed(1)}초)`);
      return [409, { error: "too fast" }];
    }

    run.loop = loop;
    run.lastAt = now;
    run.nonce = newToken();
    const prev = players.get(run.id);
    if (!prev || loop > prev.loop) players.set(run.id, { id: run.id, name: run.name, loop, at: now });
    else prev.name = run.name;
    saveSoon();
    console.log(`[회차] ${run.name} — ${loop}회차 통과`);
    return [200, { nonce: run.nonce, best: players.get(run.id).loop, rank: rankOf(run.id) }];
  },

  "POST /api/endless/run/end": async (req) => {
    const b = await json(req);
    const run = b && typeof b.runId === "string" ? runs.get(b.runId) : null;
    if (!run) return [404, { error: "no such run" }];
    if (!verify(`end|${run.runId}|${run.nonce}`, b.sig)) return [403, { error: "bad signature" }];
    runs.delete(run.runId);
    if (runOf.get(run.id) === run.runId) runOf.delete(run.id);
    const best = players.has(run.id) ? players.get(run.id).loop : 0;
    return [200, { rank: rankOf(run.id), best, total: sorted().length }];
  },

  "POST /api/player/name": async (req, url, ip) => {
    if (limited(ip, "name", 20, 3600 * 1000)) return [429, { error: "too many requests" }];
    const b = await json(req);
    const id = b && cleanId(b.id), name = b && cleanName(b.name);
    if (!id || !name) return [400, { error: "bad request" }];
    if (!verify(`name|${id}|${name}`, b.sig)) return [403, { error: "bad signature" }];
    const p = players.get(id);
    if (p) { p.name = name; saveSoon(); }
    const runId = runOf.get(id);
    if (runId && runs.has(runId)) runs.get(runId).name = name;
    return [200, { ok: true }];
  },
};

async function handle(req, res) {
  if (req.method === "OPTIONS") return send(res, 204, {});
  const url = new URL(req.url, "http://localhost");
  const route = routes[`${req.method} ${url.pathname}`];
  if (!route) return send(res, 404, { error: "not found" });
  const ip = clientIp(req);
  if (limited(ip, "any", 300, 60 * 1000)) return send(res, 429, { error: "slow down" });
  try {
    const [status, body] = await route(req, url, ip);
    send(res, status, body);
  } catch (e) {
    console.error(e);
    send(res, 500, { error: "server error" });
  }
}

load();
const server = process.env.TLS_CERT && process.env.TLS_KEY
  ? https.createServer({ cert: fs.readFileSync(process.env.TLS_CERT), key: fs.readFileSync(process.env.TLS_KEY) }, handle)
  : http.createServer(handle);
server.requestTimeout = 10_000;
server.headersTimeout = 10_000;
server.listen(PORT, HOST, () => {
  console.log(`무한 모드 랭킹 서버 — ${server instanceof https.Server ? "https" : "http"}://${HOST === "0.0.0.0" ? "localhost" : HOST}:${PORT}`);
  console.log(`회차 최소 시간 ${MIN_LOOP_MS / 1000}초 · 프록시 신뢰 ${TRUST_PROXY ? "켬" : "끔"}${process.env.RANK_SECRET ? "" : " · 기본 서명 키 사용"}`);
});

module.exports = { sign }; // 테스트용
