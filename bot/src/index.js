import "dotenv/config";
import { Client, Events, GatewayIntentBits, MessageFlags } from "discord.js";
import { loadConfig } from "./config.js";
import { byName } from "./commands/index.js";
import { addGame, db, save } from "./store.js";
import { isAdmin } from "./voting.js";
import { scheduleUpdate, updateNow } from "./leaderboard.js";

let cfg;
try {
  cfg = loadConfig();
} catch (err) {
  console.error(err.message);
  process.exit(1);
}

if (!process.env.DISCORD_TOKEN || !process.env.DISCORD_GUILD_ID) {
  console.error("Missing DISCORD_TOKEN or DISCORD_GUILD_ID in .env");
  process.exit(1);
}

// GuildMembers is needed to read boost status and roles; enable the Server
// Members privileged intent on the bot's application page.
const client = new Client({
  intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMembers],
});

client.once(Events.ClientReady, async (c) => {
  console.log(`Logged in as ${c.user.tag}`);
  try {
    await updateNow(c, cfg);
    console.log("Leaderboard is live.");
  } catch (err) {
    console.error("Could not render the leaderboard on startup:", err.message);
  }
});

client.on(Events.InteractionCreate, async (interaction) => {
  try {
    if (interaction.isAutocomplete()) {
      const handler = byName.get(interaction.commandName);
      if (handler?.autocomplete) await handler.autocomplete(interaction, cfg);
      return;
    }

    if (interaction.isChatInputCommand()) {
      const handler = byName.get(interaction.commandName);
      if (!handler) return;
      if (!interaction.inGuild()) {
        await interaction.reply({
          content: "Use this in the server, not in DMs.",
          flags: MessageFlags.Ephemeral,
        });
        return;
      }
      if (interaction.guildId !== process.env.DISCORD_GUILD_ID) {
        await interaction.reply({ content: "This bot is configured for a different server.", flags: MessageFlags.Ephemeral });
        return;
      }
      await handler.execute(interaction, cfg);
      return;
    }

    if (interaction.isButton() && interaction.guildId === process.env.DISCORD_GUILD_ID && interaction.customId.startsWith("req:")) {
      await handleReviewButton(interaction);
    }
  } catch (err) {
    console.error(`[${interaction.commandName ?? interaction.customId}]`, err);
    const body = { content: "Something went wrong. Staff have been shown the error.", flags: MessageFlags.Ephemeral };
    if (interaction.isRepliable()) {
      await (interaction.deferred || interaction.replied
        ? interaction.followUp(body)
        : interaction.reply(body)).catch(() => {});
    }
  }
});

/** Approve/reject flow for suggestions raised with /request. */
async function handleReviewButton(interaction) {
  if (!isAdmin(interaction.member, cfg)) {
    return interaction.reply({ content: "Staff only.", flags: MessageFlags.Ephemeral });
  }

  const [, action, token] = interaction.customId.split(":");
  const pending = db().pendingRequests[token];

  if (!pending) {
    await interaction.update({ components: [] });
    return interaction.followUp({
      content: "That suggestion is no longer pending.",
      flags: MessageFlags.Ephemeral,
    });
  }

  delete db().pendingRequests[token];
  save();

  if (action === "reject") {
    await interaction.update({
      content: `❌ Rejected by <@${interaction.user.id}>.`,
      components: [],
    });
    return;
  }

  const { game, created } = addGame({ name: pending.title, platform: pending.platform });
  scheduleUpdate(interaction.client, cfg);

  await interaction.update({
    content: created
      ? `✅ Added to the catalog by <@${interaction.user.id}>.`
      : `✅ Already in the catalog (approved by <@${interaction.user.id}>).`,
    components: [],
  });

  // Courtesy ping to the suggester; DMs may be closed, which is fine.
  try {
    const user = await interaction.client.users.fetch(pending.userId);
    await user.send(`Your suggestion **${game.name}** is now on the request leaderboard — go \`/vote\` for it.`);
  } catch { /* DMs closed */ }
}

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => {
    console.log("Shutting down.");
    client.destroy();
    process.exit(0);
  });
}

client.login(process.env.DISCORD_TOKEN);
