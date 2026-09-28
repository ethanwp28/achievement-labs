# Achievement Labs

Achievement, stat and save tooling for Windows PC and Xbox titles — Xbox, Xbox 360,
Games for Windows Live, Steam, Epic and Ubisoft workflows in a single desktop
application, with the save converters and stat editors that go with them.

**[Download the latest release →](https://github.com/ethanwp28/achievement-labs/releases/latest)**

Windows only. A desktop application, not a service — nothing runs unless you open it.

---

## Read before first run

Editing achievement and stat data carries risk on any live service — up to and
including action against your account by the platform holder. That risk is real
and it is yours to take.

Use a throwaway account if the account matters to you, and keep the backups the
app writes before it edits anything.

---

## Requirements

| Component | Requirement |
| --- | --- |
| Operating system | Windows 10 or 11, 64-bit |
| Runtime | Included in the single executable |
| Xbox / Epic / Ubisoft | An account for the service, and an internet connection |
| Steam | Steam installed and signed in |
| GFWL | Nothing extra — the x86 injector ships with the app |
| Xbox 360 | Profile package (CON / LIVE / PIRS) copied to the PC |
| Disk space | ~500 MB, mostly title data and backups |

## Installation

Download the executable, place it in a writable folder, and run it.

**1. Put it somewhere you own.** A folder under your user directory or a second
drive. Avoid `Program Files`: the app writes backups and title data next to
itself, and that directory needs admin rights for every write.

**2. Verify the download** against the SHA-256 published on the release:

```powershell
Get-FileHash .\AchievementLabs.exe -Algorithm SHA256
```

**3. Unblock the file.** Windows marks downloads as blocked, which makes the
injector fail in ways that look like bugs:

```powershell
Unblock-File .\AchievementLabs.exe
```

**4. Expect an antivirus warning.** The GFWL module injects into a running process
and the Steam tools read and write game memory. Heuristic scanners flag that
pattern regardless of intent.

## Uninstalling

Delete the executable and the data folder it created beside itself. Nothing is written to the registry or to `Program Files`.

## Support

Questions, bug reports and release announcements go to Discord:
**https://discord.gg/EY6AJpNfVu**

Releases are announced there first.

## About this repository

This repository distributes **built releases only** — it is not the source tree.
Issues and discussion are welcome; pull requests are not, since there is no
source here to patch.


