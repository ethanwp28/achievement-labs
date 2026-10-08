# Game request bot

Runs the `#requested-games` leaderboard for the Discord server. Members spend a small
budget of vote points on titles from a curated catalog; boosters get a bigger
budget. The board is a single embed the bot edits in place.

Why it works out as spam-proof:

- The leaderboard channel is **locked to the bot**, so there is nothing to spam.
- Members cannot add titles. `/request` goes to a private staff channel with
  Approve / Reject buttons, and is rate-limited to one suggestion per week.
- Every member reply is ephemeral, so voting leaves no trace in chat.
- Account-age and time-in-server gates block throwaway accounts before they
  reach the ballot.

## Setup

1. Create the application at <https://discord.com/developers/applications>.
   - **Bot → Privileged Gateway Intents:** enable **Server Members Intent**
     (needed to read boost status and roles). Message Content is *not* needed.
   - **Installation → Scopes:** `bot`, `applications.commands`.
   - **Bot Permissions:** View Channels, Send Messages, Embed Links,
     Read Message History, Pin Messages (for the board).
   - Use `#requested-games` and the private `#admin-only` channel. Allow
     members to Use Application Commands in `#requested-games` while denying
     ordinary messages there. The bot needs access to both channels.
2. Install and configure:

```bash
cd bot
npm ci
cp .env.example .env            # add the token, client id, guild id
cp config.example.json config.json
```

3. Fill in `config.json` with real channel and role IDs. Turn on Developer Mode
   in Discord (Settings → Advanced) to copy IDs from the right-click menu.
4. Register the slash commands, then start it:

```bash
npm run deploy
npm start
```

`npm run deploy` is guild-scoped, so commands appear immediately. Re-run it
whenever a command's name, description, or options change — not for logic
changes.

## Commands

| Command | Who | Does |
|---|---|---|
| `/vote game: points:` | Members | Spend points on a title. Autocompletes over the catalog. |
| `/unvote game:` | Members | Take points back. Autocompletes over your own ballot only. |
| `/myvotes` | Members | Your ballot, your remaining points, and where your points come from. |
| `/request title: platform:` | Members | Suggest a title not in the catalog. Goes to staff review. |
| `/catalog add\|remove\|status\|refresh` | Staff | Manage the catalog and repost the board. |

Statuses are `requested`, `planned`, `in-progress`, `shipped`. Shipped titles
drop off the main board into a "Recently shipped" field.

## Configuration

```jsonc
{
  "leaderboardChannelId": "…",    // the bot-only #requested-games channel
  "requestReviewChannelId": "…",  // private staff channel for /request
  "adminRoleIds": ["…"],          // in addition to anyone with Manage Server

  "votes": {
    "basePoints": 3,              // everyone's ballot size
    "maxPerGame": 2,              // ceiling per title, stops one-title stacking
    "boosterBonus": 2,            // extra points for server boosters
    "roleBonus": { "<roleId>": 2 }// extra points per role, e.g. a Nitro-linked role
  },

  "gates": {
    "minAccountAgeDays": 30,      // Discord account age required to vote
    "minMemberAgeHours": 24,      // time in this server required to vote
    "requestCooldownHours": 168   // one /request per member per week
  },

  "leaderboard": {
    "topN": 15,
    "updateDebounceMs": 5000      // coalesce vote bursts into one edit
  }
}
```

### On the weighting model

Boosts change **how many points you get**, not what each point is worth. A
booster has 5 points to a regular member's 3, but still cannot put more than
`maxPerGame` on any single title. So paying buys breadth of influence, not the
ability to unilaterally decide the top slot — which is the thing that makes
members stop trusting a leaderboard.

If you want paid influence to be stronger, raise `boosterBonus`. If you want it
weaker, lower it to 1 or 0. Do not raise `maxPerGame` much above 2 without
thinking about it; that is the dial that decides whether one person can own the
top spot.

## Data

State lives in `bot/data/store.json` (catalog, ballots, request cooldowns, the
board's message ID). Writes are atomic via a temp file and rename, so a crash
mid-write cannot truncate the catalog. Back this file up; it is the only copy of
the vote data.

`data/` and `config.json` are git-ignored — `config.json` holds server IDs and
`store.json` holds member IDs.

## Hosting

Run this bot on a Docker host with [`compose.yaml`](compose.yaml). It opens no
inbound ports. Enable Docker at boot; the container's `unless-stopped` policy
restarts it after crashes and host reboots unless it is explicitly stopped.
The Compose file sets DNS for this container to avoid depending on the host's
Docker resolver configuration.
Keep `.env`, `config.json`, and `data/store.json` on the host; all three are
outside the image and must survive rebuilds. Only the data directory is
writable inside the container.

To deploy an update, copy the new `package.json`, `package-lock.json`,
`Dockerfile`, `compose.yaml`, `.dockerignore`, and `src/` to the host. Do not
replace the three local state files. From the host's bot directory, run
`docker compose up -d --build`, then check `docker compose logs --tail=50`.
Use `docker compose ps` to check status, and `docker compose restart` to restart
it without rebuilding. Keep the local PC copy stopped after migration.
Before migration, stop the old process so two copies never write separate vote
stores or respond to the same interaction. Back up `data/store.json` before
each migration or restore.

