import { readFileSync, writeFileSync, renameSync, mkdirSync, existsSync } from "node:fs";
import { join } from "node:path";
import { randomUUID } from "node:crypto";
import { BOT_ROOT } from "./config.js";

const DATA_DIR = join(BOT_ROOT, "data");
const DATA_FILE = join(DATA_DIR, "store.json");

const EMPTY = {
  catalog: {},              // gameId -> { id, name, platform, status, addedAt }
  votes: {},                // userId  -> { gameId: points }
  lastRequestAt: {},        // userId  -> epoch ms
  pendingRequests: {},      // token   -> { title, platform, userId, at }
  leaderboardMessageId: null,
};

let state = null;

function load() {
  if (!existsSync(DATA_FILE)) return structuredClone(EMPTY);
  try {
    return { ...structuredClone(EMPTY), ...JSON.parse(readFileSync(DATA_FILE, "utf8")) };
  } catch (err) {
    // Refuse to silently start from scratch and lose the catalog.
    throw new Error(`data/store.json is unreadable (${err.message}). Fix or remove it before starting.`);
  }
}

export function db() {
  if (state === null) state = load();
  return state;
}

/**
 * Persist immediately. Written to a temp file and renamed into place, so a
 * crash mid-write cannot truncate the catalog. Deliberately synchronous: the
 * file is a few KB and writes are rare, and deferring risks losing a vote if
 * the process is killed before the callback runs.
 */
export function save() {
  mkdirSync(DATA_DIR, { recursive: true });
  const tmp = `${DATA_FILE}.${randomUUID()}.tmp`;
  writeFileSync(tmp, JSON.stringify(db(), null, 2), "utf8");
  renameSync(tmp, DATA_FILE);
}

/** Stable, human-readable catalog key so duplicates collapse on their own. */
export function gameId(name, platform = "") {
  return `${name}|${platform}`
    .toLowerCase()
    .normalize("NFKD")
    .replace(/[^a-z0-9|]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 90);
}

export function listCatalog() {
  return Object.values(db().catalog).sort((a, b) => a.name.localeCompare(b.name, "en"));
}

export function addGame({ name, platform = "", status = "requested" }) {
  const id = gameId(name, platform);
  const existing = db().catalog[id];
  if (existing) return { game: existing, created: false };

  const game = { id, name, platform, status, addedAt: Date.now() };
  db().catalog[id] = game;
  save();
  return { game, created: true };
}

export function removeGame(id) {
  if (!db().catalog[id]) return false;
  delete db().catalog[id];
  // Drop orphaned ballots so scores stay consistent.
  for (const ballot of Object.values(db().votes)) delete ballot[id];
  save();
  return true;
}

export function ballotOf(userId) {
  return db().votes[userId] ?? {};
}

export function setBallot(userId, ballot) {
  const cleaned = Object.fromEntries(Object.entries(ballot).filter(([, n]) => n > 0));
  if (Object.keys(cleaned).length === 0) delete db().votes[userId];
  else db().votes[userId] = cleaned;
  save();
}

/**
 * Totals per game. Vote weight lives in how many points a member gets to
 * allocate (see voting.js), not in a multiplier here, so the leaderboard number
 * is always just "points spent on this title".
 */
export function tally() {
  const scores = new Map();
  for (const ballot of Object.values(db().votes)) {
    for (const [id, points] of Object.entries(ballot)) {
      const row = scores.get(id) ?? { id, score: 0, voters: 0 };
      row.score += points;
      row.voters += 1;
      scores.set(id, row);
    }
  }
  return scores;
}
