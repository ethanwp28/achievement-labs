import {
  SlashCommandBuilder, EmbedBuilder, ActionRowBuilder, ButtonBuilder,
  ButtonStyle, MessageFlags,
} from "discord.js";
import { randomBytes } from "node:crypto";
import { db, save } from "../store.js";
import { gateFailure } from "../voting.js";

const HOUR_MS = 3_600_000;

export const data = new SlashCommandBuilder()
  .setName("request")
  .setDescription("Suggest a game that isn't in the catalog yet (goes to staff review)")
  .addStringOption((o) =>
    o.setName("title").setDescription("Exact game title").setRequired(true).setMaxLength(100))
  .addStringOption((o) =>
    o.setName("platform")
      .setDescription("Platform, e.g. PC or Xbox One / Xbox Series")
      .setMaxLength(60));

export async function execute(interaction, cfg) {
  const blocked = gateFailure(interaction.member, cfg);
  if (blocked) {
    return interaction.reply({ content: blocked, flags: MessageFlags.Ephemeral });
  }

  const cooldownMs = cfg.gates.requestCooldownHours * HOUR_MS;
  const last = db().lastRequestAt[interaction.user.id] ?? 0;
  const elapsed = Date.now() - last;
  if (elapsed < cooldownMs) {
    const hours = Math.ceil((cooldownMs - elapsed) / HOUR_MS);
    return interaction.reply({
      content:
        `You can suggest one new title every ${cfg.gates.requestCooldownHours}h. ` +
        `Try again in ${hours}h — in the meantime, \`/vote\` for something already on the board.`,
      flags: MessageFlags.Ephemeral,
    });
  }

  const title = interaction.options.getString("title").trim();
  const platform = (interaction.options.getString("platform") ?? "").trim();

  const review = await interaction.client.channels.fetch(cfg.requestReviewChannelId);
  if (!review?.isTextBased()) {
    return interaction.reply({
      content: "Request review channel is misconfigured — ping a staff member.",
      flags: MessageFlags.Ephemeral,
    });
  }

  const embed = new EmbedBuilder()
    .setTitle("New game suggestion")
    .setColor(0xf5b942)
    .addFields(
      { name: "Title", value: title },
      { name: "Platform", value: platform || "—", inline: true },
      { name: "From", value: `<@${interaction.user.id}>`, inline: true },
    )
    .setTimestamp(new Date());

  // Discord caps custom ids at 100 characters, so the suggestion is parked in
  // the store and only a short handle travels on the button.
  const token = randomBytes(8).toString("hex");
  db().pendingRequests[token] = { title, platform, userId: interaction.user.id, at: Date.now() };

  const buttons = new ActionRowBuilder().addComponents(
    new ButtonBuilder()
      .setCustomId(`req:approve:${token}`)
      .setLabel("Add to catalog")
      .setStyle(ButtonStyle.Success),
    new ButtonBuilder()
      .setCustomId(`req:reject:${token}`)
      .setLabel("Reject")
      .setStyle(ButtonStyle.Secondary),
  );

  await review.send({ embeds: [embed], components: [buttons] });

  db().lastRequestAt[interaction.user.id] = Date.now();
  save();

  return interaction.reply({
    content:
      `Sent **${title}** to staff for review. If it's approved it'll appear on the leaderboard ` +
      "and you can `/vote` for it.",
    flags: MessageFlags.Ephemeral,
  });
}
