import { SlashCommandBuilder, MessageFlags } from "discord.js";
import { ballotOf, db, setBallot } from "../store.js";
import { budgetFor, spent } from "../voting.js";
import { scheduleUpdate } from "../leaderboard.js";

export const data = new SlashCommandBuilder()
  .setName("unvote")
  .setDescription("Take your points back off a game")
  .addStringOption((o) =>
    o.setName("game")
      .setDescription("One of the games you have voted for")
      .setRequired(true)
      .setAutocomplete(true));

/** Autocompletes over the caller's own ballot, not the whole catalog. */
export async function autocomplete(interaction) {
  const query = interaction.options.getFocused().toLowerCase();
  const choices = Object.keys(ballotOf(interaction.user.id))
    .map((id) => db().catalog[id])
    .filter((g) => g && g.name.toLowerCase().includes(query))
    .slice(0, 25)
    .map((g) => ({
      name: `${g.name}${g.platform ? ` (${g.platform})` : ""}`.slice(0, 100),
      value: g.id,
    }));
  return interaction.respond(choices);
}

export async function execute(interaction, cfg) {
  const gameKey = interaction.options.getString("game");
  const ballot = { ...ballotOf(interaction.user.id) };

  if (!ballot[gameKey]) {
    return interaction.reply({
      content: "You have no points on that title.",
      flags: MessageFlags.Ephemeral,
    });
  }

  const freed = ballot[gameKey];
  delete ballot[gameKey];
  setBallot(interaction.user.id, ballot);
  scheduleUpdate(interaction.client, cfg);

  const name = db().catalog[gameKey]?.name ?? "that title";
  const left = budgetFor(interaction.member, cfg).total - spent(interaction.user.id);
  return interaction.reply({
    content: `Took ${freed} point${freed === 1 ? "" : "s"} off **${name}**. You have **${left}** left.`,
    flags: MessageFlags.Ephemeral,
  });
}
