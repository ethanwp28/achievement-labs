import { EmbedBuilder } from "discord.js";
import { db, listCatalog, save, tally } from "./store.js";

const MEDALS = ["🥇", "🥈", "🥉"];

const STATUS_LABEL = {
  requested: "",
  planned: "· planned",
  "in-progress": "· in progress",
  shipped: "· shipped ✅",
};

export function rankings() {
  const scores = tally();
  return listCatalog()
    .map((game) => ({ ...game, ...(scores.get(game.id) ?? { score: 0, voters: 0 }) }))
    .sort((a, b) => b.score - a.score || a.name.localeCompare(b.name, "en"));
}

function buildEmbed(cfg) {
  const all = rankings();
  const rows = all.filter((g) => g.status !== "shipped");
  const top = rows.slice(0, cfg.leaderboard.topN);

  const lines = top.map((g, i) => {
    const rank = MEDALS[i] ?? `\`${String(i + 1).padStart(2, " ")}.\``;
    const platform = g.platform ? ` *(${g.platform})*` : "";
    const status = STATUS_LABEL[g.status] ?? "";
    const voters = g.voters === 1 ? "1 voter" : `${g.voters} voters`;
    return `${rank} **${g.name}**${platform} ${status}\n \`${g.score}\` pts · ${voters}`;
  });

  const embed = new EmbedBuilder()
    .setTitle("🎮 Requested games")
    .setColor(0x5ee6a8)
    .setDescription(
      lines.length
        ? lines.join("\n")
        : "Nothing on the board yet. Staff add titles with `/catalog add`.",
    )
    .setFooter({
      text:
        `/vote to back a title · ${cfg.votes.basePoints} points each` +
        (cfg.votes.boosterBonus > 0 ? ` · +${cfg.votes.boosterBonus} for boosters` : ""),
    })
    .setTimestamp(new Date());

  const shipped = all.filter((g) => g.status === "shipped").slice(0, 10);
  if (shipped.length) {
    embed.addFields({
      name: "Recently shipped",
      value: shipped.map((g) => `✅ ${g.name}`).join("\n"),
    });
  }

  if (rows.length > top.length) {
    embed.addFields({
      name: "​",
      value: `…and ${rows.length - top.length} more. Use \`/vote\` to search the full catalog.`,
    });
  }

  return embed;
}

/**
 * Posts the board if it is missing, otherwise edits the existing message. Only
 * ever one message, so the channel can stay locked to everyone but the bot.
 */
async function publish(client, cfg) {
  const channel = await client.channels.fetch(cfg.leaderboardChannelId);
  if (!channel?.isTextBased()) {
    throw new Error(`leaderboardChannelId ${cfg.leaderboardChannelId} is not a text channel`);
  }

  const embed = buildEmbed(cfg);
  const existingId = db().leaderboardMessageId;

  if (existingId) {
    try {
      const message = await channel.messages.fetch(existingId);
      await message.edit({ embeds: [embed] });
      return message;
    } catch {
      // Deleted or in another channel now; fall through and repost.
    }
  }

  const message = await channel.send({ embeds: [embed] });
  db().leaderboardMessageId = message.id;
  save();
  try {
    await message.pin();
  } catch {
    // Pinning is cosmetic; missing ManageMessages should not break the board.
  }
  return message;
}

let pending = null;

/** Coalesces bursts of votes into one edit so we stay well inside rate limits. */
export function scheduleUpdate(client, cfg) {
  if (pending) clearTimeout(pending);
  pending = setTimeout(() => {
    pending = null;
    publish(client, cfg).catch((err) => console.error("[leaderboard] update failed:", err));
  }, cfg.leaderboard.updateDebounceMs);
}

export async function updateNow(client, cfg) {
  if (pending) {
    clearTimeout(pending);
    pending = null;
  }
  return publish(client, cfg);
}
