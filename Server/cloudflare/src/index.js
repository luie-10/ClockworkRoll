// CLOCKWORK ROLL — 무한 모드 랭킹 서버 (Cloudflare Workers + D1)
// Server/ranking-server.js 와 같은 API · 같은 위조 막기 규칙. 게임(Ranking.cs)은 주소만 바꾸면 된다.
//   GET  /api/health
//   GET  /api/endless/top?limit=20&id=...
//   POST /api/endless/run/start       { id, name, sig }   → { runId, nonce }
//   POST /api/endless/run/checkpoint  { runId, loop, sig } → { nonce, best, rank }
//   POST /api/endless/run/end         { runId, sig }       → { rank, best, total }
//   POST /api/player/name             { id, name, sig }    → { ok }
// 환경: DB (D1), RANK_SECRET (wrangler secret, 없으면 게임에 든 기본 키), MIN_LOOP_SECONDS

const DEFAULT_SECRET = "4S_zgTNx1Sbt0WH9INUBgqlIfdXnN2WS2ACpe4HzQnU";
const RUN_TTL_MS = 24 * 3600 * 1000;
const MAX_LOOP = 9999;
const MAX_NAME = 16;

const HEADERS = {
  "Content-Type": "application/json; charset=utf-8",
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
  "Cache-Control": "no-store",
  "X-Content-Type-Options": "nosniff",
};
const reply = (status, body) => new Response(status === 204 ? null : JSON.stringify(body), { status, headers: HEADERS });

// ─────────────────────────── 검사 ───────────────────────────

function cleanName(name) {
  if (typeof name !== "string") return null;
  const n = name.replace(/[\u0000-\u001f\u007f<>]/g, "").trim().slice(0, MAX_NAME);
  return n.length > 0 ? n : null;
}
const cleanId = (id) => (typeof id === "string" && /^[A-Za-z0-9-]{8,64}$/.test(id) ? id : null);

const enc = new TextEncoder();
let keyCache = null, keySecret = null;
async function hmacKey(secret) {
  if (keyCache && keySecret === secret) return keyCache;
  keySecret = secret;
  keyCache = await crypto.subtle.importKey("raw", enc.encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["verify"]);
  return keyCache;
}

/** 서명 확인 — crypto.subtle.verify는 시간 차이로 새지 않는다 */
async function verify(env, message, sig) {
  if (typeof sig !== "string" || !/^[0-9a-f]{64}$/.test(sig)) return false;
  const bytes = new Uint8Array(32);
  for (let i = 0; i < 32; i++) bytes[i] = parseInt(sig.substr(i * 2, 2), 16);
  return crypto.subtle.verify("HMAC", await hmacKey(env.RANK_SECRET || DEFAULT_SECRET), bytes, enc.encode(message));
}

const newToken = () => [...crypto.getRandomValues(new Uint8Array(16))].map((b) => b.toString(16).padStart(2, "0")).join("");

// 접속 주소별 횟수 제한 — 워커 인스턴스마다 따로 세는 가벼운 장치 (서명 · 순서 · 시간 하한이 본 방어선)
const hits = new Map();
function limited(ip, kind, max, windowMs) {
  const key = kind + "|" + ip, now = Date.now();
  const list = (hits.get(key) || []).filter((t) => now - t < windowMs);
  list.push(now);
  hits.set(key, list);
  if (hits.size > 5000) hits.clear();
  return list.length > max;
}

async function body(req) {
  const text = await req.text();
  if (text.length > 4096) return null;
  try { return JSON.parse(text); } catch { return null; }
}

// ─────────────────────────── 기록 ───────────────────────────

async function rankOf(env, id) {
  const r = await env.DB.prepare(
    `SELECT (SELECT COUNT(*) FROM players o WHERE o.loop > 0 AND (o.loop > p.loop OR (o.loop = p.loop AND o.at < p.at))) + 1 AS rank
     FROM players p WHERE p.id = ? AND p.loop > 0`).bind(id).first();
  return r ? r.rank : 0;
}
async function total(env) {
  return (await env.DB.prepare("SELECT COUNT(*) AS n FROM players WHERE loop > 0").first()).n;
}

// ─────────────────────────── 경로 ───────────────────────────

const routes = {
  "GET /api/health": async () => reply(200, { ok: true }),

  "GET /api/endless/top": async (req, env, url) => {
    const limit = Math.min(100, Math.max(1, parseInt(url.searchParams.get("limit") || "20", 10) || 20));
    const id = cleanId(url.searchParams.get("id"));
    const { results } = await env.DB.prepare("SELECT name, loop FROM players WHERE loop > 0 ORDER BY loop DESC, at ASC LIMIT ?").bind(limit).all();
    const entries = results.map((p, i) => ({ rank: i + 1, name: p.name, loop: p.loop }));
    let me = null;
    if (id) {
      const p = await env.DB.prepare("SELECT name, loop FROM players WHERE id = ? AND loop > 0").bind(id).first();
      if (p) me = { rank: await rankOf(env, id), name: p.name, loop: p.loop };
    }
    return reply(200, { entries, me, total: await total(env) });
  },

  "POST /api/endless/run/start": async (req, env, url, ip) => {
    if (limited(ip, "start", 20, 3600 * 1000)) return reply(429, { error: "too many runs" });
    const b = await body(req);
    const id = b && cleanId(b.id), name = b && cleanName(b.name);
    if (!id || !name) return reply(400, { error: "bad request" });
    if (!(await verify(env, `start|${id}|${name}`, b.sig))) return reply(403, { error: "bad signature" });

    const runId = newToken(), nonce = newToken(), now = Date.now();
    await env.DB.batch([
      env.DB.prepare("DELETE FROM runs WHERE id = ?").bind(id), // 한 사람은 한 판만
      env.DB.prepare("INSERT INTO runs (run_id, id, name, started_at, last_at, loop, nonce) VALUES (?, ?, ?, ?, ?, 0, ?)").bind(runId, id, name, now, now, nonce),
    ]);
    return reply(200, { runId, nonce });
  },

  "POST /api/endless/run/checkpoint": async (req, env, url, ip) => {
    if (limited(ip, "checkpoint", 60, 3600 * 1000)) return reply(429, { error: "too many requests" });
    const b = await body(req);
    const run = b && typeof b.runId === "string"
      ? await env.DB.prepare("SELECT * FROM runs WHERE run_id = ? AND started_at > ?").bind(b.runId, Date.now() - RUN_TTL_MS).first()
      : null;
    if (!run) return reply(404, { error: "no such run" });
    const loop = Number(b.loop);
    if (!Number.isInteger(loop) || loop !== run.loop + 1 || loop > MAX_LOOP) return reply(409, { error: "loop out of order" });
    if (!(await verify(env, `checkpoint|${run.run_id}|${loop}|${run.nonce}`, b.sig))) return reply(403, { error: "bad signature" });
    const now = Date.now();
    if (now - run.last_at < (parseFloat(env.MIN_LOOP_SECONDS ?? "60") || 0) * 1000) {
      console.warn(`[거부] ${run.name} — ${loop}회차가 너무 빠르다 (${((now - run.last_at) / 1000).toFixed(1)}초)`);
      return reply(409, { error: "too fast" });
    }

    // nonce · 회차가 그대로일 때만 넘긴다 — 같은 요청이 동시에 두 번 와도 한 번만 먹힌다
    const nonce = newToken();
    const upd = await env.DB.prepare("UPDATE runs SET loop = ?, last_at = ?, nonce = ? WHERE run_id = ? AND nonce = ? AND loop = ?")
      .bind(loop, now, nonce, run.run_id, run.nonce, run.loop).run();
    if (!upd.meta || upd.meta.changes !== 1) return reply(403, { error: "stale nonce" });

    await env.DB.prepare(
      `INSERT INTO players (id, name, loop, at) VALUES (?, ?, ?, ?)
       ON CONFLICT(id) DO UPDATE SET name = excluded.name,
         at = CASE WHEN excluded.loop > players.loop THEN excluded.at ELSE players.at END,
         loop = MAX(players.loop, excluded.loop)`).bind(run.id, run.name, loop, now).run();
    const best = (await env.DB.prepare("SELECT loop FROM players WHERE id = ?").bind(run.id).first()).loop;
    return reply(200, { nonce, best, rank: await rankOf(env, run.id) });
  },

  "POST /api/endless/run/end": async (req, env) => {
    const b = await body(req);
    const run = b && typeof b.runId === "string" ? await env.DB.prepare("SELECT * FROM runs WHERE run_id = ?").bind(b.runId).first() : null;
    if (!run) return reply(404, { error: "no such run" });
    if (!(await verify(env, `end|${run.run_id}|${run.nonce}`, b.sig))) return reply(403, { error: "bad signature" });
    await env.DB.prepare("DELETE FROM runs WHERE run_id = ?").bind(run.run_id).run();
    const p = await env.DB.prepare("SELECT loop FROM players WHERE id = ?").bind(run.id).first();
    return reply(200, { rank: await rankOf(env, run.id), best: p ? p.loop : 0, total: await total(env) });
  },

  "POST /api/player/name": async (req, env, url, ip) => {
    if (limited(ip, "name", 20, 3600 * 1000)) return reply(429, { error: "too many requests" });
    const b = await body(req);
    const id = b && cleanId(b.id), name = b && cleanName(b.name);
    if (!id || !name) return reply(400, { error: "bad request" });
    if (!(await verify(env, `name|${id}|${name}`, b.sig))) return reply(403, { error: "bad signature" });
    await env.DB.batch([
      env.DB.prepare("UPDATE players SET name = ? WHERE id = ?").bind(name, id),
      env.DB.prepare("UPDATE runs SET name = ? WHERE id = ?").bind(name, id),
    ]);
    return reply(200, { ok: true });
  },
};

export default {
  async fetch(req, env) {
    if (req.method === "OPTIONS") return reply(204, {});
    const url = new URL(req.url);
    const route = routes[`${req.method} ${url.pathname}`];
    if (!route) return reply(404, { error: "not found" });
    const ip = req.headers.get("cf-connecting-ip") || "?";
    if (limited(ip, "any", 300, 60 * 1000)) return reply(429, { error: "slow down" });
    try {
      return await route(req, env, url, ip);
    } catch (e) {
      console.error(e);
      return reply(500, { error: "server error" });
    }
  },

  // 24시간 지난 판 치우기
  async scheduled(event, env) {
    await env.DB.prepare("DELETE FROM runs WHERE started_at < ?").bind(Date.now() - RUN_TTL_MS).run();
  },
};
