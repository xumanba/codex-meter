# Bundled Win-CodexBar CLI

The Windows portable package includes the standalone console CLI used only to
read Codex quota data. Users do not need to install the Win-CodexBar desktop
application separately.

- Project: <https://github.com/nesszer/Win-CodexBar>
- CLI version: `0.45.2`
- File: `codexbar-cli.exe`
- SHA-256: `C0B737E1B36E0D90524AA6FAB169D718EBB9E54F00656695E340522D284ADFAD`
- License: MIT; see `../../ThirdPartyLicenses/Win-CodexBar-LICENSE.txt`

`windows/build.ps1` refuses to build if the bundled binary is absent or its
hash differs. `CodexBarClient` verifies the same hash before executing the
bundled copy. To update the CLI, replace the binary and update the version and
hash together in this file, `windows/build.ps1`, and
`windows/src/CodexBarClient.cs`, then run the full Windows test and package
workflow.
