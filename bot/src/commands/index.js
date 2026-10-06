import * as vote from "./vote.js";
import * as unvote from "./unvote.js";
import * as myvotes from "./myvotes.js";
import * as request from "./request.js";
import * as catalog from "./catalog.js";

export const commands = [vote, unvote, myvotes, request, catalog];

export const byName = new Map(commands.map((c) => [c.data.name, c]));
