# <img src="AutoStance/images/icon.png" width="32" alt=""> AutoStance

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
   https://raw.githubusercontent.com/Arzhaell/AutoStance/main/repo.json
   ```
3. Type `/xlplugins`, search for **AutoStance** and install it.

## Usage

`/autostance` opens the settings:

- **Mode**: enable automatically, remove automatically, or pause.
- **When to apply it**:
  - *Always*: the stance is put back in the desired state as soon as it changes.
  - *At specific moments* (can be combined): zone change / job change / resurrection, countdown (X s before the end), on pull (optionally bosses only), duty recommence after a wipe.
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

## Disclaimer

Like any third-party tool, using Dalamud and its plugins is against the FFXIV Terms of Service. Use it at your own risk.

## Releasing a new version

Pushing a `vX.Y.Z` tag is all it takes: the GitHub workflow builds the plugin, creates the release and updates `repo.json`.

```bash
git tag v1.1.0
git push origin v1.1.0
```
