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

The [GitHub Actions workflow](../.github/workflows/discord-github-updates.yml)
posts to a Discord incoming webhook through
[`tools/post-discord-github-update.mjs`](../tools/post-discord-github-update.mjs).
It announces:

- PR opened, reopened, marked ready for review, merged, or closed;
- each push to the default `main` branch, with up to five commit summaries;
- a published release or pre-release.

In `#github`, open **Edit Channel → Integrations → Webhooks → New
Webhook**, select that channel, and copy its URL. In the GitHub repository open
**Settings → Secrets and variables → Actions → New repository secret**. Name it
`DISCORD_GITHUB_WEBHOOK_URL` and paste the URL. Treat the URL like a password:
anyone holding it can post to the channel. Do not put it in a file or commit.

The workflow must be on the repository's default branch before it can run. It
reads PR metadata but never checks out a contributor's PR code, so fork PRs
cannot execute code with the Discord webhook secret. It requests only
`contents: read` GitHub permissions. It does not announce PR comments or
reviews, and commit posts are limited to `main` to keep the channel readable.

After setup, use a harmless test PR or commit to verify delivery. A missing or
invalid secret makes the job fail without printing the webhook URL.
