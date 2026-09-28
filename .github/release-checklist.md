# Release checklist

- [ ] Build the self-contained single-file executable with tools/Build-Release.ps1
- [ ] Confirm the release folder contains exactly AchievementLabs.exe
- [ ] Run the packaged executable release smoke test
- [ ] Run the licensing and security test suite
- [ ] Record the executable SHA-256
- [ ] Confirm no source, private Events data, account database, or credentials are packaged
- [ ] Create the GitHub release using tag vX.Y.Z and attach AchievementLabs.exe
- [ ] Put the SHA-256, file size, signing status, and principal changes in the release notes
- [ ] Update and deploy the website release metadata
- [ ] Announce the release in Discord
