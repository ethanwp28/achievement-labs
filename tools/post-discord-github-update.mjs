import { readFileSync } from 'node:fs';

const webhook = process.env.DISCORD_WEBHOOK_URL;
if (!webhook) throw new Error('Set the DISCORD_GITHUB_WEBHOOK_URL repository secret.');
const destination = new URL(webhook);
if (destination.protocol !== 'https:' || !['discord.com', 'discordapp.com'].includes(destination.hostname)
    || !/^\/api\/webhooks\/\d+\/[^/]+/.test(destination.pathname)) {
  throw new Error('The Discord webhook URL is invalid.');
}

const eventName = process.env.GITHUB_EVENT_NAME;
const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
const repo = event.repository?.full_name;
if (repo !== 'ethanwp28/achievement-labs') throw new Error('Unexpected repository in event payload.');

const clean = (value, max = 240) => String(value ?? '')
  .replace(/[\r\n\t]+/g, ' ').replace(/\s+/g, ' ').trim().slice(0, max)
  .replace(/([\\`*_~|>\[\]()])/g, '\\$1');
const safeUrl = (value) => {
  const url = new URL(value);
  if (url.protocol !== 'https:' || url.hostname !== 'github.com') throw new Error('Unexpected GitHub URL.');
  return url.href;
};

let embed;
if (eventName === 'pull_request_target') {
  const pr = event.pull_request;
  const action = event.action === 'closed' ? (pr.merged ? 'merged' : 'closed') : event.action.replaceAll('_', ' ');
  embed = {
    title: `Pull request #${pr.number} ${action}`,
    url: safeUrl(pr.html_url),
    description: clean(pr.title),
    color: pr.merged ? 0x8957e5 : 0x2da44e,
    footer: { text: clean(pr.user?.login, 80) },
  };
} else if (eventName === 'push') {
  if (event.ref !== `refs/heads/${event.repository.default_branch}` || event.deleted) process.exit(0);
  const commits = event.commits ?? [];
  const lines = commits.slice(0, 5).map((commit) => {
    if (!/^[a-f0-9]{40}$/.test(commit.id)) throw new Error('Invalid commit ID in push event.');
    return `• [${commit.id.slice(0, 7)}](https://github.com/${repo}/commit/${commit.id}) ${clean(commit.message?.split('\n')[0], 140)}`;
  });
  if (commits.length > 5) lines.push(`• …and ${commits.length - 5} more`);
  embed = {
    title: `${commits.length} commit${commits.length === 1 ? '' : 's'} on ${event.repository.default_branch}`,
    url: safeUrl(event.compare || event.repository.html_url),
    description: lines.join('\n') || `Updated to ${clean(event.after?.slice(0, 7), 7)}`,
    color: 0x3977d5,
    footer: { text: clean(event.pusher?.name || 'GitHub push', 80) },
  };
} else if (eventName === 'release' && event.action === 'published') {
  const release = event.release;
  embed = {
    title: `Release ${clean(release.name || release.tag_name, 180)} published`,
    url: safeUrl(release.html_url),
    description: clean(release.body || 'A new release is available.', 1000),
    color: 0xf0b400,
    footer: { text: release.prerelease ? 'Pre-release' : 'Release' },
  };
} else {
  process.exit(0);
}

if (process.env.DRY_RUN === '1') {
  console.log(JSON.stringify(embed, null, 2));
  process.exit(0);
}

const response = await fetch(destination, {
  method: 'POST',
  headers: { 'content-type': 'application/json' },
  body: JSON.stringify({ username: 'Achievement Labs GitHub', embeds: [embed], allowed_mentions: { parse: [] } }),
});
if (!response.ok) throw new Error(`Discord rejected the update (HTTP ${response.status}).`);
