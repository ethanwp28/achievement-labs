import { SlashCommandBuilder, EmbedBuilder, MessageFlags } from "discord.js";
import { ballotOf, db } from "../store.js";
import { budgetFor, spent } from "../voting.js";

export const data = new SlashCommandBuilder()
  .setName("myvotes")
  .setDescription("See where your vote points are and how many you have left");

export async function execute(interaction, cfg) {
  const budget = budgetFor(interaction.member, cfg);
  const ballot = ballotOf(interaction.user.id);
  const used = spent(interaction.user.id);

  const rows = Object.entries(ballot)
    .map(([id, points]) => ({ points, game: db().catalog[id] }))
    .filter((r) => r.game)
    .sort((a, b) => b.points - a.points);

  const embed = new EmbedBuilder()
    .setTitle("Your vote points")
    .setColor(0x5ee6a8)
    .setDescription(
      rows.length
        ? rows.map((r) =>
            `\`${r.points}\` · **${r.game.name}**${r.game.platform ? ` *(${r.game.platform})*` : ""}`,
          ).join("\n")
        : "You haven't voted yet. Use `/vote` to back a title.",
    )
    .addFields(
      { name: "Used", value: `${used} / ${budget.total}`, inline: true },
      { name: "Remaining", value: `${Math.max(0, budget.total - used)}`, inline: true },
      {
        name: "Where your points come from",
        value: budget.lines.map((l) => `+${l.points} — ${l.label}`).join("\n"),
      },
    );

  // Losing a boost can leave a ballot over budget; say so instead of silently
  // dropping votes, and let the member choose what to give up.
  if (used > budget.total) {
    embed.addFields({
      name: "⚠ Over budget",
      value:
        `You are ${used - budget.total} point(s) over, probably because a boost or role ended. ` +
        "Your existing votes still count, but you can't add more until you `/unvote` something.",
    });
  }

  return interaction.reply({ embeds: [embed], flags: MessageFlags.Ephemeral });
}
