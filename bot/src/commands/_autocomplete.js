import { listCatalog } from "../store.js";

/** Shared catalog autocomplete: matches on title or platform, 25-choice cap. */
export async function catalogAutocomplete(interaction) {
  const query = interaction.options.getFocused().toLowerCase();
  const choices = listCatalog()
    .filter((g) =>
      !query ||
      g.name.toLowerCase().includes(query) ||
      g.platform.toLowerCase().includes(query))
    .slice(0, 25)
    .map((g) => ({
      name: `${g.name}${g.platform ? ` (${g.platform})` : ""}`.slice(0, 100),
      value: g.id,
    }));
  return interaction.respond(choices);
}
