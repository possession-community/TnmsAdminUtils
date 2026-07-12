# Command List

All commands are registered with the `ms_` prefix (e.g. `ms_slay`).
In chat, `!slay` / `css_slay` can also be used.

## Chat Commands

| Command | Permission Node | Description |
|---|---|---|
| !say \<message\> | tnms.adminutil.chat.command.say | Send a message to all players via chat |
| !csay \<message\> | tnms.adminutil.chat.command.say | Send a message to all players via center display |
| !hsay \<message\> | tnms.adminutil.chat.command.say | Send a message to all players via hint display |
| !asay \<message\> | tnms.adminutil.chat.command.say | Send a message to admins only |
| !psay \<player\> \<message\> | tnms.adminutil.chat.command.say | Send a private message to a specific player |
| @\<message\> (in chat) | tnms.adminutil.chat.command.say | Admin broadcast — non-admins see `[ADMIN] msg`, admins see `[ADMIN name] msg` |

## Player Management Commands

### Combat / Stats

| Command | Permission Node | Description |
|---|---|---|
| !slay \<player\> | tnms.adminutil.management.ingame.command.slay | Kill a player |
| !slap \<player\> [damage] | tnms.adminutil.management.ingame.command.slay | Slap a player with optional damage |
| !hp \<player\> \<health\> | tnms.adminutil.management.ingame.command.hp | Set a player's health (auto-adjusts MaxHealth above 100) |
| !god \<player\> \<0\|1\> | tnms.adminutil.management.ingame.command.god | Toggle god mode (invincibility) |
| !speed \<player\> \<value\> | tnms.adminutil.management.ingame.command.speed | Set a player's speed multiplier (0.0–10.0) |
| !gravity \<player\> \<scale\> | tnms.adminutil.management.ingame.command.gravity | Set a player's gravity scale (0.0–100.0) |

### Movement / Spawn

| Command | Permission Node | Description |
|---|---|---|
| !noclip \<player\> [0\|1] | tnms.adminutil.management.ingame.command.noclip | Toggle noclip. Without value, toggles current state |
| !freeze \<player\> [time] | tnms.adminutil.management.ingame.command.freeze | Freeze a player in place (green tint). Optional auto-unfreeze timer |
| !unfreeze \<player\> | tnms.adminutil.management.ingame.command.freeze | Unfreeze a player |
| !bury \<player\> | tnms.adminutil.management.ingame.command.bury | Bury a player into the ground |
| !unbury \<player\> | tnms.adminutil.management.ingame.command.bury | Unbury a player from the ground |
| !respawn \<player\> | tnms.adminutil.management.ingame.command.respawn | Respawn a dead player |
| !revive \<player\> | tnms.adminutil.management.ingame.command.respawn | Respawn a player (alive or dead) |

### Visual Effects

| Command | Permission Node | Description |
|---|---|---|
| !blind \<player\> [time] | tnms.adminutil.management.ingame.command.blind | Blind a player (black screen fade). Default 999s |
| !unblind \<player\> | tnms.adminutil.management.ingame.command.blind | Remove blindness |
| !shake \<player\> [time] | tnms.adminutil.management.ingame.command.shake | Shake a player's screen. Default 999s |
| !unshake \<player\> | tnms.adminutil.management.ingame.command.shake | Stop shaking |
| !color \<player\> [color] | tnms.adminutil.management.ingame.command.color | Set a player's render color tint. Without color, resets to white |
| !glow \<player\> \<0\|1\> [color] | tnms.adminutil.management.ingame.command.glow | Toggle outline glow with optional color |

Available colors: `red`, `green`, `blue`, `yellow`, `orange`, `purple`, `cyan`, `pink`, `white`, `black`

### Team / Items / Other

| Command | Permission Node | Description |
|---|---|---|
| !team \<player\> \<ct\|t\|spec\> | tnms.adminutil.management.ingame.command.team | Change a player's team (kills the player) |
| !swap \<player\> | tnms.adminutil.management.ingame.command.team | Swap to opposite team without killing |
| !give \<player\> \<weapon\> | tnms.adminutil.management.ingame.command.give | Give a weapon (auto-prefixes `weapon_`) |
| !rename \<player\> \<newname\> | tnms.adminutil.management.ingame.command.rename | Rename a player |

### Teleports

| Command | Permission Node | Description |
|---|---|---|
| !bring \<player\> | tnms.adminutil.management.ingame.command.bring | Teleport a player to executor's position |
| !goto \<player\> | tnms.adminutil.management.ingame.command.goto | Teleport to a player's position |
| !send \<target\> \<to\> | tnms.adminutil.management.ingame.command.send | Teleport a player to another player's position |

## Server Management Commands

| Command | Permission Node | Description |
|---|---|---|
| !rcon \<command\> | tnms.adminutil.management.server.command.rcon | Execute a server console command |
| !cvar \<cvar\> [value] | tnms.adminutil.management.server.command.cvar | View or set a ConVar value |
| !settime \<seconds\> | tnms.adminutil.management.ingame.command.settime | Set the current round timer |
| !addtime \<seconds\> | tnms.adminutil.management.ingame.command.addtime | Add/subtract time from the current round |
| !endround [reason] | tnms.adminutil.management.ingame.command.terminateround | Terminate the current round |

## Client Management Commands

| Command | Permission Node | Description |
|---|---|---|
| !qcvar \<target\> \<cvar\> | tnms.adminutil.management.client.command.querycvar | Query a client ConVar value |
| !rcvar \<target\> \<cvar\> \<value\> | tnms.adminutil.management.client.command.replicatecvar | Replicate a ConVar value to a client |

## Vote Commands

| Command | Permission Node | Description |
|---|---|---|
| !vote \<question\> \<option1\> \<option2\> [...] | tnms.adminutil.vote.command.vote | Start a multi-choice vote (NativeVoteManagerMS) |
| !ynvote \<question\> | tnms.adminutil.vote.command.vote | Start a Yes/No vote (CS2 native vote UI) |

## Target Specifiers

The `<player>` parameter supports the following target specifiers:

| Specifier | Description |
|---|---|
| `name` | Partial name match |
| `@all` | All players |
| `@me` | Yourself |
| `@ct` | Counter-Terrorists |
| `@t` | Terrorists |
| `@spec` | Spectators |
| `@alive` | Alive players |
| `@dead` | Dead players |
| `@bots` | Bot players |
| `@humans` | Human players |
