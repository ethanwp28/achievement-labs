import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
export const BOT_ROOT = join(here, "..");

function required(cfg, path) {
  const value = path.split(".").reduce((o, k) => (o == null ? o : o[k]), cfg);
  if (value === undefined || value === null || value === "") {
    throw new Error(`config.json is missing "${path}"`);
  }
  return value;
}

export function loadConfig() {
  let raw;
  try {
    raw = readFileSync(join(BOT_ROOT, "config.json"), "utf8");
  } catch {
    throw new Error("config.json not found. Copy config.example.json to config.json and fill it in.");
  }

  const cfg = JSON.parse(raw);

  required(cfg, "leaderboardChannelId");
  required(cfg, "requestReviewChannelId");
  for (const key of ["leaderboardChannelId", "requestReviewChannelId"]) {
    if (!/^\d{17,20}$/.test(String(cfg[key])) || /^0+$/.test(String(cfg[key]))) {
      throw new Error(`config.json needs a real Discord ID for "${key}"`);
    }
  }

  // roleBonus doubles as documentation in the example file, so drop the comment key.
  const roleBonus = { ...(cfg.votes?.roleBonus ?? {}) };
  delete roleBonus._comment;

  return {
    leaderboardChannelId: cfg.leaderboardChannelId,
    requestReviewChannelId: cfg.requestReviewChannelId,
    adminRoleIds: cfg.adminRoleIds ?? [],
    votes: {
      basePoints: cfg.votes?.basePoints ?? 3,
      maxPerGame: cfg.votes?.maxPerGame ?? 2,
      boosterBonus: cfg.votes?.boosterBonus ?? 2,
      roleBonus,
    },
    gates: {
      minAccountAgeDays: cfg.gates?.minAccountAgeDays ?? 30,
      minMemberAgeHours: cfg.gates?.minMemberAgeHours ?? 24,
      requestCooldownHours: cfg.gates?.requestCooldownHours ?? 168,
    },
    leaderboard: {
      topN: cfg.leaderboard?.topN ?? 15,
      updateDebounceMs: cfg.leaderboard?.updateDebounceMs ?? 5000,
    },
  };
}
