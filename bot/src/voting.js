import { ballotOf } from "./store.js";

const DAY_MS = 86_400_000;
const HOUR_MS = 3_600_000;

/**
 * How many vote points a member gets. This is the only place boosts and
 * Nitro-linked roles matter: they widen the ballot, they do not multiply it.
 */
export function budgetFor(member, cfg) {
  const lines = [{ label: "Base", points: cfg.votes.basePoints }];
  let total = cfg.votes.basePoints;

  if (member.premiumSince && cfg.votes.boosterBonus > 0) {
    total += cfg.votes.boosterBonus;
    lines.push({ label: "Server booster", points: cfg.votes.boosterBonus });
  }

  for (const [roleId, bonus] of Object.entries(cfg.votes.roleBonus)) {
    if (!member.roles.cache.has(roleId)) continue;
    const points = Number(bonus) || 0;
    if (points <= 0) continue;
    total += points;
    lines.push({ label: member.guild.roles.cache.get(roleId)?.name ?? "Role bonus", points });
  }

  return { total, lines };
}

export function spent(userId) {
  return Object.values(ballotOf(userId)).reduce((a, b) => a + b, 0);
}

/**
 * Spam gates. Both are about account provenance, not behaviour, so they can be
 * checked before any state is touched.
 */
export function gateFailure(member, cfg) {
  const accountAgeMs = Date.now() - member.user.createdTimestamp;
  if (accountAgeMs < cfg.gates.minAccountAgeDays * DAY_MS) {
    return `Your Discord account needs to be at least ${cfg.gates.minAccountAgeDays} days old to vote.`;
  }

  // joinedTimestamp can be null for uncached members on very old guilds.
  if (member.joinedTimestamp !== null) {
    const memberAgeMs = Date.now() - member.joinedTimestamp;
    if (memberAgeMs < cfg.gates.minMemberAgeHours * HOUR_MS) {
      const hours = Math.ceil((cfg.gates.minMemberAgeHours * HOUR_MS - memberAgeMs) / HOUR_MS);
      return `New members can vote after ${cfg.gates.minMemberAgeHours}h in the server. Try again in ${hours}h.`;
    }
  }

  return null;
}

export function isAdmin(member, cfg) {
  if (member.permissions.has("ManageGuild")) return true;
  return cfg.adminRoleIds.some((id) => member.roles.cache.has(id));
}
