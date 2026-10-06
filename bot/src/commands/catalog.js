import { SlashCommandBuilder, InteractionContextType, MessageFlags } from "discord.js";
import { addGame, db, listCatalog, removeGame, save } from "../store.js";
import { isAdmin } from "../voting.js";
import { scheduleUpdate, updateNow } from "../leaderboard.js";
import { catalogAutocomplete } from "./_autocomplete.js";

const STATUSES = ["requested", "planned", "in-progress", "shipped"];

export const data = new SlashCommandBuilder()
  .setName("catalog")
  .setDescription("Staff: manage the request catalog")
  .setContexts(InteractionContextType.Guild)
  .addSubcommand((s) =>
    s.setName("add")
      .setDescription("Add a title members can vote for")
      .addStringOption((o) =>
        o.setName("title").setDescription("Game title").setRequired(true).setMaxLength(100))
      .addStringOption((o) =>
        o.setName("platform").setDescription("Platform").setMaxLength(60)))
  .addSubcommand((s) =>
    s.setName("remove")
      .setDescription("Remove a title and everyone's votes on it")
      .addStringOption((o) =>
        o.setName("game").setDescription("Title").setRequired(true).setAutocomplete(true)))
  .addSubcommand((s) =>
    s.setName("status")
      .setDescription("Change a title's status")
      .addStringOption((o) =>
        o.setName("game").setDescription("Title").setRequired(true).setAutocomplete(true))
      .addStringOption((o) =>
        o.setName("status")
          .setDescription("New status")
          .setRequired(true)
          .addChoices(...STATUSES.map((s) => ({ name: s, value: s })))))
  .addSubcommand((s) =>
    s.setName("refresh").setDescription("Repost or re-render the leaderboard message"));

export const autocomplete = catalogAutocomplete;

export async function execute(interaction, cfg) {
  if (!isAdmin(interaction.member, cfg)) {
    return interaction.reply({ content: "Staff only.", flags: MessageFlags.Ephemeral });
  }

  const sub = interaction.options.getSubcommand();

  if (sub === "add") {
    const title = interaction.options.getString("title").trim();
    const platform = (interaction.options.getString("platform") ?? "").trim();
    const { game, created } = addGame({ name: title, platform });
    scheduleUpdate(interaction.client, cfg);
    return interaction.reply({
      content: created
        ? `Added **${game.name}**${platform ? ` (${platform})` : ""} to the catalog.`
        : `**${game.name}** is already in the catalog.`,
      flags: MessageFlags.Ephemeral,
    });
  }

  if (sub === "remove") {
    const id = interaction.options.getString("game");
    const name = db().catalog[id]?.name;
    const ok = removeGame(id);
    scheduleUpdate(interaction.client, cfg);
    return interaction.reply({
      content: ok ? `Removed **${name}** and its votes.` : "No such title.",
      flags: MessageFlags.Ephemeral,
    });
  }

  if (sub === "status") {
    const id = interaction.options.getString("game");
    const status = interaction.options.getString("status");
    const game = db().catalog[id];
    if (!game) {
      return interaction.reply({ content: "No such title.", flags: MessageFlags.Ephemeral });
    }
    game.status = status;
    save();
    scheduleUpdate(interaction.client, cfg);
    return interaction.reply({
      content: `**${game.name}** is now \`${status}\`.`,
      flags: MessageFlags.Ephemeral,
    });
  }

  // refresh
  await interaction.deferReply({ flags: MessageFlags.Ephemeral });
  await updateNow(interaction.client, cfg);
  return interaction.editReply(`Leaderboard refreshed (${listCatalog().length} titles in catalog).`);
}
