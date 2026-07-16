# Command List

All commands are registered with the `ms_` prefix (e.g. `ms_slay`).
In chat, `!slay` / `css_slay` can also be used.

## Chat Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !say \<message\> | - | tnms.adminutil.chat.command.say.normal | Send a message to all players via chat |
| !csay \<message\> | - | tnms.adminutil.chat.command.say.center | Send a message to all players via center display |
| !hsay \<message\> | - | tnms.adminutil.chat.command.say.hint | Send a message to all players via hint display |
| !asay \<message\> | - | tnms.adminutil.chat.command.say.admins | Send a message to admins only |
| !psay \<player\> \<message\> | - | tnms.adminutil.chat.command.say.private | Send a private message to a specific player |

### Admin Chat Broadcast

Typing `@message` in chat (`say` / `say_team` both supported) sends an admin broadcast.

- Non-admins see: `[ADMIN] message`
- Admins see: `[ADMIN playername] message`
- The original chat message is suppressed

Permission: `tnms.adminutil.chat.command.say.admins` (used for both sending and receiving)

## Combat / Stats Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !slay \<player\> | - | tnms.adminutil.management.ingame.command.slay | Kill a player |
| !slap \<player\> [damage] | - | tnms.adminutil.management.ingame.command.slap | Slap a player with optional damage |
| !hp \<player\> \<health\> | - | tnms.adminutil.management.ingame.command.hp | Set a player's health (auto-adjusts MaxHealth above 100) |
| !god \<player\> \<0\|1\> | - | tnms.adminutil.management.ingame.command.god | Toggle god mode (invincibility) |
| !speed \<player\> \<value\> | - | tnms.adminutil.management.ingame.command.speed | Set a player's speed multiplier (0.0–10.0) |
| !gravity \<player\> \<scale\> | - | tnms.adminutil.management.ingame.command.gravity | Set a player's gravity scale (0.0–100.0) |
| !money \<player\> \<amount\> | - | tnms.adminutil.management.ingame.command.money | Set a player's money (0–60000) |
| !setkev \<player\> \<armor\> [helmet 0\|1] | - | tnms.adminutil.management.ingame.command.setkev | Set a player's armor and optional helmet |

## Movement / Spawn Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !noclip \<player\> [0\|1] | - | tnms.adminutil.management.ingame.command.noclip | Toggle noclip. Without value, toggles current state |
| !freeze \<player\> [time] | - | tnms.adminutil.management.ingame.command.freeze | Freeze a player in place (green tint). Optional auto-unfreeze timer |
| !unfreeze \<player\> | - | tnms.adminutil.management.ingame.command.freeze | Unfreeze a player |
| !bury \<player\> | - | tnms.adminutil.management.ingame.command.bury | Bury a player into the ground |
| !unbury \<player\> | - | tnms.adminutil.management.ingame.command.bury | Unbury a player from the ground |
| !respawn \<player\> | - | tnms.adminutil.management.ingame.command.respawn | Respawn a dead player |
| !revive \<player\> | - | tnms.adminutil.management.ingame.command.respawn | Respawn a player (alive or dead) |

## Visual Effect Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !blind \<player\> [time] | - | tnms.adminutil.management.ingame.command.blind | Blind a player (black screen fade). Default 999s |
| !unblind \<player\> | - | tnms.adminutil.management.ingame.command.blind | Remove blindness |
| !shake \<player\> [time] | - | tnms.adminutil.management.ingame.command.shake | Shake a player's screen. Default 999s |
| !unshake \<player\> | - | tnms.adminutil.management.ingame.command.shake | Stop shaking |
| !color \<player\> [color] | - | tnms.adminutil.management.ingame.command.color | Set a player's render color tint. Without color, resets to white |
| !glow \<player\> \<0\|1\> [color] | - | tnms.adminutil.management.ingame.command.glow | Toggle outline glow with optional color |

Available colors: `red`, `green`, `blue`, `yellow`, `orange`, `purple`, `cyan`, `pink`, `white`, `black`

## Team / Item Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !team \<player\> \<ct\|t\|spec\> | - | tnms.adminutil.management.ingame.command.team | Change a player's team (kills the player) |
| !swap \<player\> | - | tnms.adminutil.management.ingame.command.team | Swap to opposite team without killing |
| !give \<player\> \<weapon\> | - | tnms.adminutil.management.ingame.command.give | Give a weapon (auto-prefixes `weapon_`) |
| !strip \<player\> | - | tnms.adminutil.management.ingame.command.strip | Strip all weapons from a player |
| !drop \<player\> \<weaponIndex\> | - | tnms.adminutil.management.ingame.command.drop | Force a player to drop a weapon by index |
| !buyzone \<player\> \<0\|1\> | - | tnms.adminutil.management.ingame.command.buyzone | Toggle a player's buy zone access |

## Player Info Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !rename \<player\> \<newname\> | - | tnms.adminutil.management.ingame.command.rename | Rename a player |
| !clantag \<player\> \<tag\> | - | tnms.adminutil.management.ingame.command.clantag | Set a player's clan tag |
| !getmodel \<player\> | - | tnms.adminutil.management.ingame.command.model | Show a player's current model path |
| !setmodel \<player\> \<model.vmdl\> | - | tnms.adminutil.management.ingame.command.model | Set a player's model |
| !teamname \<ct\|t\> \<name\> | - | tnms.adminutil.management.ingame.command.teamname | Set a team's display name |

## Teleport Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !bring \<player\> | - | tnms.adminutil.management.ingame.command.bring | Teleport a player to executor's position |
| !goto \<player\> | - | tnms.adminutil.management.ingame.command.goto | Teleport to a player's position |
| !send \<target\> \<to\> | - | tnms.adminutil.management.ingame.command.send | Teleport a player to another player's position |

## Game Rules Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !settime \<seconds\> | - | tnms.adminutil.management.ingame.command.settime | Set the current round timer |
| !addtime \<seconds\> | - | tnms.adminutil.management.ingame.command.addtime | Add/subtract time from the current round |
| !endround [reason] | - | tnms.adminutil.management.ingame.command.terminateround | Terminate the current round |

## Server Management Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !users | - | tnms.adminutil.management.server.command.users | List online players (slot, name, steamid) |
| !rcon \<command\> | - | tnms.adminutil.management.server.command.rcon | Execute a server console command |
| !cvar \<cvar\> [value] | - | tnms.adminutil.management.server.command.cvar | View or set a ConVar value |

## Client Management Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !qcvar \<target\> \<cvar\> | - | tnms.adminutil.management.client.command.querycvar | Query a client ConVar value |
| !rcvar \<target\> \<cvar\> \<value\> | - | tnms.adminutil.management.client.command.replicatecvar | Replicate a ConVar value to a client |

## Vote Commands

| Command | Alias | Permission Node | Description |
|---|---|---|---|
| !vote \<question\> \<option1\> \<option2\> [...] | - | tnms.adminutil.vote.command.vote | Start a multi-choice vote (NativeVoteManagerMS) |
| !ynvote \<question\> | - | tnms.adminutil.vote.command.vote | Start a Yes/No vote (CS2 native vote UI) |

## Permission Nodes

| Node | Description |
|---|---|
| tnms.adminutil.chat.command.say.normal | Use !say |
| tnms.adminutil.chat.command.say.center | Use !csay |
| tnms.adminutil.chat.command.say.hint | Use !hsay |
| tnms.adminutil.chat.command.say.admins | Send/receive !asay and @broadcast |
| tnms.adminutil.chat.command.say.private | Use !psay |
| tnms.adminutil.management.ingame.command.slay | Use !slay |
| tnms.adminutil.management.ingame.command.slap | Use !slap |
| tnms.adminutil.management.ingame.command.hp | Use !hp |
| tnms.adminutil.management.ingame.command.god | Use !god |
| tnms.adminutil.management.ingame.command.speed | Use !speed |
| tnms.adminutil.management.ingame.command.gravity | Use !gravity |
| tnms.adminutil.management.ingame.command.money | Use !money |
| tnms.adminutil.management.ingame.command.setkev | Use !setkev |
| tnms.adminutil.management.ingame.command.noclip | Use !noclip |
| tnms.adminutil.management.ingame.command.freeze | Use !freeze / !unfreeze |
| tnms.adminutil.management.ingame.command.bury | Use !bury / !unbury |
| tnms.adminutil.management.ingame.command.respawn | Use !respawn / !revive |
| tnms.adminutil.management.ingame.command.blind | Use !blind / !unblind |
| tnms.adminutil.management.ingame.command.shake | Use !shake / !unshake |
| tnms.adminutil.management.ingame.command.color | Use !color |
| tnms.adminutil.management.ingame.command.glow | Use !glow |
| tnms.adminutil.management.ingame.command.team | Use !team / !swap |
| tnms.adminutil.management.ingame.command.give | Use !give |
| tnms.adminutil.management.ingame.command.strip | Use !strip |
| tnms.adminutil.management.ingame.command.drop | Use !drop |
| tnms.adminutil.management.ingame.command.buyzone | Use !buyzone |
| tnms.adminutil.management.ingame.command.rename | Use !rename |
| tnms.adminutil.management.ingame.command.clantag | Use !clantag |
| tnms.adminutil.management.ingame.command.model | Use !getmodel / !setmodel |
| tnms.adminutil.management.ingame.command.teamname | Use !teamname |
| tnms.adminutil.management.ingame.command.bring | Use !bring |
| tnms.adminutil.management.ingame.command.goto | Use !goto |
| tnms.adminutil.management.ingame.command.send | Use !send |
| tnms.adminutil.management.ingame.command.settime | Use !settime |
| tnms.adminutil.management.ingame.command.addtime | Use !addtime |
| tnms.adminutil.management.ingame.command.terminateround | Use !endround |
| tnms.adminutil.management.server.command.users | Use !users |
| tnms.adminutil.management.server.command.rcon | Use !rcon |
| tnms.adminutil.management.server.command.cvar | Use !cvar |
| tnms.adminutil.management.client.command.querycvar | Use !qcvar |
| tnms.adminutil.management.client.command.replicatecvar | Use !rcvar |
| tnms.adminutil.vote.command.vote | Use !vote / !ynvote |

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
