# Achievement Labs Discord setup

The game voting bot was found in the older `ultimate-achievements-lab/bot/`
workspace and copied into this repository's [`bot/`](../bot/) directory. Its
voting design and commands are documented in [`bot/README.md`](../bot/README.md).

## Channels

Create these text channels in the Achievement Labs server:

| Channel | Access | Purpose |
| --- | --- | --- |
| `#requested-games` | Everyone can read; only the bot can send messages | One pinned leaderboard that the bot edits after votes. Members use `/vote`, `/unvote`, `/myvotes`, and `/request` in the server. |
| `#admin-only` | Staff and bot only | Suggestions from `/request` with Approve and Reject buttons. |
| `#github` | Everyone can read; staff and the GitHub webhook can post | PR openings/merges/closures, commits to `main`, and published releases. |

For `#requested-games`, deny Send Messages, Add Reactions, and Create Public
Threads to `@everyone`, but allow Use Application Commands so members can vote
there. Allow the bot to View Channel, Send Messages,
Embed Links, Read Message History, and Pin Messages (for the board).
The private `#admin-only` review channel also needs View Channel, Send Messages, and Embed
Links for the bot.

Turn on **User Settings → Advanced → Developer Mode**, then right-click the
server and channels to copy their IDs. Put the server ID in `bot/.env` and the
two voting channel IDs in `bot/config.json`; both files are ignored by Git.

## Voting bot

Follow [`bot/README.md`](../bot/README.md) to create the Discord application,
enable Server Members Intent, invite it, install dependencies, register slash
commands, and start the process. The bot needs a persistent disk for
`bot/data/store.json`, which holds the catalog and ballots. Back it up before
moving hosts or redeploying.

Seed a few games with `/catalog add`, then run `/catalog refresh`. Check a vote,
an unvote, a suggestion, and a staff approval before opening the channel to
members. If members vote while the bot is offline, Discord will reject the
commands; the last leaderboard remains visible.

## GitHub updates

The repository uses GitHub's direct webhook integration with Discord. In
`#github`, open **Edit Channel → Integrations → Webhooks** and copy the
**Achievement Labs GitHub** webhook URL. In the GitHub repository, open
**Settings → Webhooks → Add webhook**, append `/github` to the Discord URL,
choose `application/json`, and select the **Pushes**, **Pull requests**, and
**Releases** events. Keep SSL verification and the webhook active. The webhook
ID for this setup is `693309026`.

The URL is a posting credential: anyone holding it can post to `#github`.
Keep it out of source control and public messages. GitHub delivers event data
directly to Discord, so this integration does not depend on GitHub Actions.
GitHub's webhook deliveries page shows each response; Discord should return
`204` for accepted events. Push events cover all branches.
