-- CLOCKWORK ROLL 무한 모드 랭킹 (Cloudflare D1)
CREATE TABLE IF NOT EXISTS players (
  id   TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  loop INTEGER NOT NULL DEFAULT 0,
  at   INTEGER NOT NULL            -- 최고 기록을 세운 시각 (같은 회차면 먼저 세운 사람이 위)
);
CREATE INDEX IF NOT EXISTS players_rank ON players (loop DESC, at ASC);

CREATE TABLE IF NOT EXISTS runs (
  run_id     TEXT PRIMARY KEY,
  id         TEXT NOT NULL,
  name       TEXT NOT NULL,
  started_at INTEGER NOT NULL,
  last_at    INTEGER NOT NULL,
  loop       INTEGER NOT NULL DEFAULT 0,
  nonce      TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS runs_player ON runs (id);
CREATE INDEX IF NOT EXISTS runs_started ON runs (started_at);
