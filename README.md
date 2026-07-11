# CWEssentials

[![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)](#)
[![Status](https://img.shields.io/badge/status-release-green.svg)](#)
[![Game](https://img.shields.io/badge/game-Rust-orange.svg)](#)
[![Framework](https://img.shields.io/badge/framework-Oxide%20%2F%20uMod-yellow.svg)](#)
[![Language](https://img.shields.io/badge/language-C%23-239120.svg)](#)
[![License](https://img.shields.io/badge/license-Apache%202.0-lightgrey.svg)](LICENSE)

**CWEssentials** is a comprehensive admin and moderation plugin for Rust servers running on the Oxide/uMod framework. It provides a wide range of essential tools for server management, including maintenance mode, teleportation, player state controls, health and inventory utilities, moderation commands, and system information.

All commands work both in chat and console, can be individually enabled or disabled, and are permission‑based for fine‑grained access control.

---

## Features

- **Maintenance mode** with whitelist – kick all non‑bypassed players with a custom message.
- **Player states** – god mode, flight, noclip, vanish, and speed control (all toggleable per player).
- **Teleportation** – teleport to a player, bring a player to you, or jump to coordinates.
- **Health & inventory** – heal, eat, clear inventory, give items, repair single or all items.
- **Moderation** – kick players with a custom reason.
- **Information** – list online players, check ping, view in‑game time, set day/night.
- **Rules** – display server rules from a text file.
- **System** – dynamic help pages, reload configuration, show plugin version.
- **Global enable/disable** – turn off the entire plugin with one config toggle.
- **Per‑command enable/disable** – each command can be independently switched on/off.
- **Console support** – most commands have console equivalents for Rcon or server console.
- **Targeting** – commands can affect other players when you have the `cwessentials.target.others` permission.
- **State persistence** – player states (god, fly, etc.) are saved and restored on reload.
- **Localized messages** – English only (easily extendable).
- **Clean chat formatting** – uses a configurable prefix, colors, and sizes.

---

## Installation

1. Place the `CWEssentials.cs` file in your `oxide/plugins` folder.
2. Start or reload the server – the plugin will generate a default configuration file.
3. Adjust the configuration in `oxide/config/CWEssentials.json` as needed.
4. Grant permissions to players or groups using Oxide's permission system (e.g., `oxide.grant user <steamid> cwessentials.admin`).

---

## Configuration

The configuration file is located at `oxide/config/CWEssentials.json`. It is automatically created with defaults when the plugin loads.

### Settings

| Key | Description |
| :-- | :---------- |
| `Enabled` | Global plugin enable/disable (`true`/`false`). |
| `ChatPrefix` | The formatted prefix shown before every chat message. |
| `PluginIcon` | SteamID of the avatar icon displayed next to chat messages. |
| `MessageSize` | Font size for regular chat messages. |
| `TitleSize` | Font size for titles (e.g., help headers). |
| `Colors` | Color codes for different message types (`Info`, `Success`, `Warning`, `Error`, `Highlight`). |
| `MaintenanceMessage` | Kick message shown during maintenance mode. |
| `RulesFile` | Filename (relative to `oxide/config/CWEssentials/`) containing the server rules. |

### Commands

Every command has its own `Enabled` flag under the `Commands` section. Set any to `false` to completely disable that command.

| Command | Config Key |
| :------ | :--------- |
| Maintenance | `Maintenance` |
| God | `God` |
| Fly | `Fly` |
| Noclip | `Noclip` |
| Vanish | `Vanish` |
| Speed | `Speed` |
| TP | `TP` |
| TPHere | `TPHere` |
| TPLoc | `TPLoc` |
| Heal | `Heal` |
| Eat | `Eat` |
| Clear | `Clear` |
| Give | `Give` |
| Repair | `Repair` |
| RepairAll | `RepairAll` |
| Kick | `Kick` |
| List | `List` |
| Ping | `Ping` |
| Time | `Time` |
| Day/Night | `DayNight` |
| Rules | `Rules` |
| Help | `Help` |
| Reload | `Reload` |
| Version | `Version` |

### Maintenance

- `Enabled` – whether maintenance mode is active on server start.
- `Whitelist` – list of SteamID strings that are allowed to join even when maintenance is on.

---

## Commands

All commands work in **chat** (prefixed with `/`) and in **console** (without the slash).  
Optional arguments are shown in `[brackets]`, required arguments in `<angle brackets>`.

### Maintenance

| Chat | Console | Description |
| :--- | :------ | :---------- |
| `/maintenance` | `maintenance` | Toggle maintenance mode on/off. |
| `/maintenance add <name\|id>` | `maintenance add <name\|id>` | Add a player to the whitelist. |
| `/maintenance remove <name\|id>` | `maintenance remove <name\|id>` | Remove a player from the whitelist. |
| `/maintenance list` | `maintenance list` | Show all whitelisted players. |
| `/maintenance status` | `maintenance status` | Display whether maintenance is active. |

### Player States

| Chat | Console | Description |
| :--- | :------ | :---------- |
| `/god [name\|id]` | `god [name\|id]` | Toggle god mode (invulnerability). |
| `/fly [name\|id]` | `fly [name\|id]` | Toggle flight. |
| `/noclip [name\|id]` | `noclip [name\|id]` | Toggle noclip (flight through walls). |
| `/vanish [name\|id]` | `vanish [name\|id]` | Toggle invisibility (players won't see you). |
| `/speed <1-10>` | `speed <1-10>` | Set your own movement speed (1=normal, 10=max). |
| `/speed <name\|id> <1-10>` | `speed <name\|id> <1-10>` | Set another player's speed. |
| `/speed <name\|id> reset` | `speed <name\|id> reset` | Reset another player's speed to normal. |

### Teleportation

| Chat | Console | Description |
| :--- | :------ | :---------- |
| `/tp <name\|id>` | `tp <name\|id>` | Teleport yourself to the target player. |
| `/tphere <name\|id>` | `tphere <name\|id>` | Teleport the target player to your location. |
| `/tploc <x> <y> <z>` | `tploc <x> <y> <z>` | Teleport yourself to the given coordinates. |
| `/tploc <x> <y> <z> <name\|id>` | `tploc <x> <y> <z> <name\|id>` | Teleport a target player to the given coordinates. |

### Health / Inventory

| Chat | Console | Description |
| :--- | :------ | :---------- |
| `/heal [name\|id]` | `heal [name\|id]` | Fully restore health and clear radiation, poison, bleeding. |
| `/eat [name\|id]` | `eat [name\|id]` | Fully restore calories and hydration. |
| `/clear [name\|id]` | `clear [name\|id]` | Clear the target player's entire inventory. |
| `/give <item> <amount> [name\|id]` | `give <item> <amount> [name\|id]` | Give an item (by shortname) to yourself or another player. |
| `/repair [name\|id]` | `repair [name\|id]` | Repair the active (held) item. |
| `/repairall [name\|id]` | `repairall [name\|id]` | Repair all items in the target player's inventory. |

### Moderation

| Chat | Console | Description |
| :--- | :------ | :---------- |
| `/kick <name\|id> [reason]` | `kick <name\|id> [reason]` | Kick a player with an optional reason. |

### Information

| Chat | Console | Description |
| :--- | :------ | :---------- |
| `/list` | `list` | Show all online players with their SteamID. |
| `/online` | `online` | Alias for `/list`. |
| `/players` | `players` | Alias for `/list`. |
| `/ping [name\|id]` | `ping [name\|id]` | Show your own or another player's ping. |
| `/time` | `time` | Show the current in‑game time. |
| `/day` | `day` | Set the world time to day (12:00). |
| `/night` | `night` | Set the world time to night (00:00). |
| `/rules` | `rules` | Display the server rules from the configured text file. |

### System

| Chat | Console | Description |
| :--- | :------ | :---------- |
| `/cwe help [page]` | `cwe help [page]`<br>`cwe.help [page]` | Show a paginated list of available commands. |
| `/cwe reload` | `cwe reload`<br>`cwe.reload` | Reload the plugin configuration and data. |
| `/cwe version` | `cwe version`<br>`cwe.version` | Show the plugin version. |
| `/cweversion` | `cweversion` | Alias for `cwe version`. |

> **Note:** Console commands do not require the `/` prefix. For example, in Rcon you can type `maintenance` to toggle maintenance mode.

---

## Permissions

The plugin uses a fine‑grained permission system. All permissions are registered automatically.

| Permission | Description |
| :--------- | :---------- |
| `cwessentials.admin` | Grants full access to all commands and targeting (overrides per‑command checks). |
| `cwessentials.target.others` | Allows using commands with a target argument on other players. |
| `cwessentials.maintenance` | Allows using the `/maintenance` commands. |
| `cwessentials.maintenance.bypass` | Prevents being kicked during maintenance mode (also whitelisted via config). |
| `cwessentials.god` | Allows using `/god`. |
| `cwessentials.fly` | Allows using `/fly`. |
| `cwessentials.noclip` | Allows using `/noclip`. |
| `cwessentials.vanish` | Allows using `/vanish`. |
| `cwessentials.speed` | Allows using `/speed`. |
| `cwessentials.tp` | Allows using `/tp`. |
| `cwessentials.tphere` | Allows using `/tphere`. |
| `cwessentials.tploc` | Allows using `/tploc`. |
| `cwessentials.heal` | Allows using `/heal`. |
| `cwessentials.eat` | Allows using `/eat`. |
| `cwessentials.clear` | Allows using `/clear`. |
| `cwessentials.give` | Allows using `/give`. |
| `cwessentials.repair` | Allows using `/repair`. |
| `cwessentials.repairall` | Allows using `/repairall`. |
| `cwessentials.kick` | Allows using `/kick`. |
| `cwessentials.list` | Allows using `/list`, `/online`, `/players`. |
| `cwessentials.ping` | Allows using `/ping`. |
| `cwessentials.time` | Allows using `/time`. |
| `cwessentials.time.set` | Allows using `/day` and `/night`. |
| `cwessentials.rules` | Allows using `/rules`. |
| `cwessentials.help` | Allows using `/cwe help`. |
| `cwessentials.reload` | Allows using `/cwe reload`. |
| `cwessentials.version` | Allows using `/cwe version`. |

> **Tip:** The `cwessentials.admin` permission also grants `cwessentials.target.others` implicitly.

---

## Rules File

The plugin reads a text file located at:

```
oxide/config/CWEssentials/rules.txt
```

You can change the filename via the `RulesFile` setting in the configuration.  
Each line in the file is displayed as a separate line in chat when a player runs `/rules`.

If the file does not exist or is empty, an appropriate message is shown.

---

## Notes

- All commands are case‑insensitive.
- When a command is disabled in the config, it will not appear in the help list and will not execute.
- The global `Settings.Enabled` switch can turn off the entire plugin without removing it.
- Player states (god, fly, noclip, vanish, speed) are saved in `oxide/data/CWEssentials_Data.json` and persist across reloads.
- The plugin automatically creates the `oxide/config/CWEssentials/` directory for the rules file.
- Maintenance mode kicks players immediately when enabled and also prevents new joins.

---

## License

This project is licensed under the **Apache License, Version 2.0**.  
You may obtain a copy of the license at [http://www.apache.org/licenses/LICENSE-2.0](http://www.apache.org/licenses/LICENSE-2.0).

---

## Credits

- **Author**: whitecristafer  
- **Contributions**: Feedback and suggestions are welcome.
