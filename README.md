# <img src="AutoStanceXIV/images/icon.png" width="32" alt=""> AutoStanceXIV

🇫🇷 [Version française](README.fr.md)

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that automatically turns your tank stance on or off, on every tank:

| Job | Stance |
|---|---|
| Paladin / Gladiator | Iron Will |
| Warrior / Marauder | Defiance |
| Dark Knight | Grit |
| Gunbreaker | Royal Guard |

## Installation

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this URL, click **+**, tick the checkbox and save:
   ```
   https://raw.githubusercontent.com/Arzhaell/AutoStanceXIV/main/repo.json
   ```
3. Type `/xlplugins`, search for **AutoStanceXIV** and install it.

## Usage

`/autostance` opens the settings:

- **Classic or advanced**:
  - *Classic*: the same settings everywhere.
  - *Advanced*: one tab of settings per type of duty. Advanced settings start as a copy of the classic ones.
    - **Open world**: everything outside of duties (FATEs, field operations…)
    - **Dungeons**: including variant and deep dungeons, guildhests and treasure dungeons
    - **Trials**: normal, extreme and unreal
    - **Raids**: 8-player raids — normal, savage and ultimate
    - **Alliance**: 24-player alliance raids, including chaotic
- **Mode**: enable automatically, remove automatically, or pause.
- **When to apply it**:
  - *Always*: the stance is put back in the desired state as soon as it changes.
  - *At specific moments* (can be combined): zone change, resurrection, job change, countdown (X s before the end), on pull (optionally bosses only), duty recommence after a wipe.
- **Options**: only in duties, allow in combat, show in the server info bar, chat message.
- **Language**: auto (follows Dalamud), English or French.

Quick mode switching:

| Command | Effect |
|---|---|
| `/autostance on` | keep the stance on |
| `/autostance off` | keep the stance off |
| `/autostance toggle` | switch between on and off |
| `/autostance pause` | stop touching the stance |

In the server info bar ("Stance: ON"): left click toggles on/off, right click pauses.

In advanced mode, the commands and the server info bar act on the type of duty you are currently in.

## Disclaimer

Like any third-party tool, using Dalamud and its plugins is against the FFXIV Terms of Service. Use it at your own risk.

## Releasing a new version

Pushing an annotated `vX.Y.Z` tag is all it takes: the GitHub workflow runs the tests, builds the plugin, creates the release (its notes are the tag message) and updates `repo.json`.

```bash
git tag -a v1.2.0 --cleanup=verbatim -F notes.txt
git push origin v1.2.0
```
