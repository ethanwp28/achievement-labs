import { SlashCommandBuilder, MessageFlags } from "discord.js";
import { ballotOf, db, setBallot } from "../store.js";
import { budgetFor, gateFailure, spent } from "../voting.js";
import { scheduleUpdate } from "../leaderboard.js";
import { catalogAutocomplete } from "./_autocomplete.js";

export const data = new SlashCommandBuilder()
  .setName("vote")
  .setDescription("Back a game on the request leaderboard")
  .addStringOption((o) =>
    o.setName("game")
      .setDescription("Start typing a title from the catalog")
      .setRequired(true)
      .setAutocomplete(true))
  .addIntegerOption((o) =>
    o.setName("points")
      .setDescription("How many of your points to spend (default 1)")
      .setMinValue(1)
      .setMaxValue(10));

export const autocomplete = catalogAutocomplete;

export async function execute(interaction, cfg) {
  const blocked = gateFailure(interaction.member, cfg);
  if (blocked) {
    return interaction.reply({ content: blocked, flags: MessageFlags.Ephemeral });
  }

  const gameKey = interaction.options.getString("game");
  const game = db().catalog[gameKey];
  if (!game) {
    return interaction.reply({
      content: "Pick a title from the autocomplete list. To suggest something new, use `/request`.",
      flags: MessageFlags.Ephemeral,
    });
  }
  if (game.status === "shipped") {
    return interaction.reply({
      content: "That game has shipped, so it is no longer open for voting.",
      flags: MessageFlags.Ephemeral,
    });
  }

  const points = interaction.options.getInteger("points") ?? 1;
  const budget = budgetFor(interaction.member, cfg);
  const ballot = { ...ballotOf(interaction.user.id) };
  const already = ballot[game.id] ?? 0;

  if (already + points > cfg.votes.maxPerGame) {
    return interaction.reply({
      content:
        `You can put at most **${cfg.votes.maxPerGame}** points on one title` +
        (already ? ` and you already have ${already} on **${game.name}**.` : "."),
      flags: MessageFlags.Ephemeral,
    });
  }

  const remaining = budget.total - spent(interaction.user.id);
  if (points > remaining) {
    return interaction.reply({
      content:
        `You only have **${remaining}** of ${budget.total} points left. ` +
        "Free some up with `/unvote`, or boost the server for more.",
      flags: MessageFlags.Ephemeral,
    });
  }

  ballot[game.id] = already + points;
  setBallot(interaction.user.id, ballot);
  scheduleUpdate(interaction.client, cfg);

  const left = budget.total - spent(interaction.user.id);
  return interaction.reply({
    content:
      `Put **${points}** point${points === 1 ? "" : "s"} on **${game.name}**` +
      `${game.platform ? ` (${game.platform})` : ""}. You have **${left}** left.`,
    flags: MessageFlags.Ephemeral,
  });
}
