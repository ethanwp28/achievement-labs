import "dotenv/config";
import { REST, Routes } from "discord.js";
import { commands } from "./commands/index.js";

const { DISCORD_TOKEN, DISCORD_CLIENT_ID, DISCORD_GUILD_ID } = process.env;

for (const [key, value] of Object.entries({ DISCORD_TOKEN, DISCORD_CLIENT_ID, DISCORD_GUILD_ID })) {
  if (!value) {
    console.error(`Missing ${key} in .env`);
    process.exit(1);
  }
}

const body = commands.map((c) => c.data.toJSON());
const rest = new REST({ version: "10" }).setToken(DISCORD_TOKEN);

// Guild-scoped: updates appear immediately instead of taking up to an hour.
await rest.put(
  Routes.applicationGuildCommands(DISCORD_CLIENT_ID, DISCORD_GUILD_ID),
  { body },
);

console.log(`Registered ${body.length} commands: ${body.map((c) => "/" + c.name).join(", ")}`);
