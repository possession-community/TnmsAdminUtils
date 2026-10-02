# Admin Menu

`!admin` opens a menu that runs the admin commands without typing them.

Permission: `tnms.adminutil.menu`

> Requires TnmsPluginFoundation with the command `OnRegistered` hook (commit `8267947` or later).
> Replace `shared/TnmsPluginFoundation` before deploying this version; with an older Foundation the plugin fails to load.

## Menu Tree

| Root | Flow |
|---|---|
| Players | target → command → options → confirm → execute |
| General Commands | command → options → target → confirm → execute |
| Server Commands | command → options → confirm → execute |
| Notifications (say / asay / csay / hsay / psay / toast) | command → (target) → message → confirm → execute |
| Categories added by a TOML | Same as General Commands |

- Root order: Open Panel (with permission) / Favorites / Players / General / Server / Notification / TOML categories. Favorites is listed even when empty so the item numbers stay put.
- In the admin panel, the Commands page's sidebar lists Search / Favorites / All / the categories (see [ADMIN_PANEL](ADMIN_PANEL.md)).
- Command lists show the chat command next to the label, e.g. `!slay | Slay`.
- Players lists every command that takes a target, whatever its category.
- Targets list `@all` / `@ct` / `@t` / `@spec`, the other selectors registered to TargetingManager (e.g. `@alive`, a
  plugin's `@zombies`), and the players you can target.
- Commands you have no permission for are hidden. A root is hidden when it has no command.
- Every page has a Back item. The menu closes after execution.

The menu runs the command as you (`ms_<command> ...`), so permission checks, logs and broadcasts are the same as typing it in chat.

## Options

| Kind | How it is chosen |
|---|---|
| Preset | Values from the menu TOMLs, or "Type in chat" for any value |
| Choice | Fixed list (team, color, on/off, round end reason) |
| Text | Typed in chat |
| Optional | "Default (skip)" leaves the argument to the command's default |

### Text input

When a step needs text, the menu closes and asks in chat.

- Your next chat message is used as the value and is not shown in chat.
- Type `cancel` to stop (the admin panel cancels in the panel instead, see [ADMIN_PANEL](ADMIN_PANEL.md)).
- The input expires after 60 seconds; later messages are sent to chat as usual.
- After the input, the rest of the flow opens as a new menu.

Vote options are typed at once, separated by commas (`,` or `、`). At least 2 options are required.

## Menu TOMLs (configs/menus)

Every `*.toml` under `configs/menus/` of the module directory is read, subfolders included, in path order, and merged
into one menu. Another plugin can ship its own file (e.g. `configs/menus/myplugin.toml`) to add its commands to the menu.
The files are read once after every plugin has loaded. After editing them, run `!adminmenu_reload`
(permission `tnms.adminutil.menu.reload`).

The default `configs/menus/default.toml` is copied on deploy only when the server has none (an existing one is never
overwritten). Its comments include a written-out example.

A mistake skips only the table it is in (a preset, category or operation), with the file, the table and the reason in
the error log; the rest still loads. Unknown keys are ignored with a warning.

> `menu.json` is no longer read; a warning is logged at load while it exists. Move its presets to `[admin.menu.presets]`.

### Presets

Values offered for the built-in commands. Missing keys fall back to the defaults.

```toml
[admin.menu.presets]
slap = ["0", "1", "5", "10", "50", "100"]
give = ["ak47", "m4a1_silencer", "awp", "deagle", "knife"]
```

| Key | Used by |
|---|---|
| slap / hp / money / setkev / drop / gravity / speed | Value of the command |
| freeze / blind / shake | Duration (seconds) |
| give | Weapon list |
| addtime / settime | Seconds |
| terminateround | Delay (seconds) |
| toast | Display time |

### Categories

An extra command list. The table key (`fun`) is its id. Listed after the built-in lists, in the order read.

```toml
[admin.menu.category.fun]
Name = { en = "Fun Commands", ja = "おもしろコマンド" }
RequiredPermission = "myplugin.menu.fun"   # optional: hides the whole list from admins without it
Icon = "kill"                      # optional: the panel sidebar's icon (see below); "●" without it
IconSize = "l"                     # optional, glyphs only: "s" / "m" (default) / "l" when one looks too small or large
```

`Icon` is the name of one of CS2's own UI icons (drawn like the built-in lists' icons), or any other text, drawn as
a glyph (BMP symbols such as `⚔` or `★`; no emoji, and glyphs differ in size, hence `IconSize`). The names:
`addplayer`, `alert`, `arrowhead`, `bomb_c4`, `bot`, `broadcast_ring`, `buyzone`, `camera`, `cancel`, `casual`, `check`, `clock`, `community_servers`, `competitive`, `crosshair_circle`, `ct_logo_1c`, `defuser_white`, `elimination`, `exit`, `favorite_star_filled`, `film`, `filter`, `filter_team`, `find`, `gift`, `graph`, `home`, `hostage_alive`, `hourglass`, `info`, `info_i`, `inventory`, `invite`, `kill`, `kill_headshot`, `leader`, `link`, `lobby`, `locked`, `map_onmap`, `menu`, `message_arrow`, `music_kit`, `muted`, `overwatch`, `picture`, `player`, `plus`, `power`, `random`, `refresh`, `remove`, `report_server`, `search`, `secure_connection`, `settings`, `settings_sliders`, `shield`, `shield_alert`, `smile`, `sort`, `sound_2`, `sound_off`, `star`, `stats`, `stream`, `t_logo_1c`, `teamcolor`, `timer`, `trade`, `trash`, `trophy`, `tune`, `undo`, `unmuted`, `vacnet`, `vote_check`, `votesurrender`, `voteteamswitch`, `warning`, `watch`, `watch_tv`, `zoom_in`.

`general` / `server` / `notification` are the ids of the built-in lists (General / Server / Notification) and cannot be
redefined.

### Operations

One entry of a list. The table key (`fun_slap`) is its id, which favorites store. An id that is the name of a built-in
command is refused (built-ins cannot be replaced).

```toml
[admin.menu.operations.fun_slap]
Category = "fun"                     # required: an added category or general / server / notification
Command = "slap"                     # required: the command name without ms_ or !; run as ms_<Command>
RequiredPermission = "tnms.adminutil.management.ingame.command.slap"   # required
Name = { en = "Big Slap", ja = "強めのスラップ" }
Description = { en = "Slap hard.", ja = "強めに叩きます。" }           # optional

[[admin.menu.operations.fun_slap.args]]
Type = "target"

[[admin.menu.operations.fun_slap.args]]
Type = "int"
Name = { en = "Damage", ja = "ダメージ" }
SuggestValues = [50, 100, 200]
Min = 0
Max = 1000
```

Arguments are `[[...args]]` tables in command line order (at most 6).

| Type | What | Keys |
|---|---|---|
| `target` | Player select | `AllowSelectors` (offer `@all` and the like, default true) |
| `int` / `float` | Number. Typed values are checked for being a whole number / number and the range | `SuggestValues`, `AllowCustomInput`, `Min`, `Max` |
| `string` | Text | `SuggestValues`, `AllowCustomInput`, `Quote` (pass as one quoted argument, default false) |
| `string_list` | Comma separated texts, each quoted | `MinCount` (default 1) |
| `bool` | On / off (`1` / `0`) | |
| `team` | CT / T / Spectator (`ct` / `t` / `spec`) | |
| `choice` | Fixed choices | `Choices = [{ Value = "red", Name = { en = "Red", ja = "赤" } }, { Value = "blue" }]` |

- `SuggestValues` are the offered values; `AllowCustomInput` adds "Type in chat" (default true). An argument with no
  suggestions and `AllowCustomInput = false` is an error.
- Every type takes `Name` (heading of the field), `Optional` and `Default` (written for a skipped argument when a later
  one is set). An optional argument that is not the last one needs a `Default`.
- Texts are `Name = "Text"` (every language), `Name = { en = "...", ja = "..." }` (the player's language, else en,
  else the first), or `NameKey = "<lang key>"` (a key of TnmsAdminUtils' `lang/`). `Description` works the same.
- An operation without `Name` uses the `AdminMenu.Command.<Command>` translation.

## Command list cache

Each admin's list of usable commands is built once their permissions finish loading after joining; the menu and the
panel read from it. It is rebuilt when their permissions change, when commands are registered or removed, and on
`!adminmenu_reload`.

## Translations

Menu texts are `AdminMenu.*` keys in `lang/<culture>.json`.
Command labels are `AdminMenu.Command.<command name>`; the command name is shown when the key is missing.
Categories and operations added by a TOML can carry their own texts (`Name = { en = ..., ja = ... }`).

## Adding a command to the menu

Register an entry in `OnRegistered` of the command. Arguments are listed in command line order.

```csharp
private const string Permission = "tnms.adminutil.management.ingame.command.slap";

protected override void OnRegistered()
    => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(
        AdminMenuEntry.Create(CommandName, Permission).Target().Preset("slap").Optional());
```

| Method | Argument |
|---|---|
| `Target(allowSelectors)` | Player select. The first one is the target picked in the Players tree |
| `Preset(key)` | Preset from the menu TOMLs |
| `Choice(titleKey, ...)` / `Toggle()` / `TeamChoice()` | Fixed choices |
| `Text(titleKey, quote)` | Text typed in chat |
| `TextList(titleKey, minCount)` | Comma separated texts, each quoted |
| `Optional(defaultRaw)` | Makes the last argument optional |
| `InCategory(category)` | Which list the command goes in (`AdminMenuCategory.General` / `Server` / `Notification`, or a TOML category id). Defaults to `General` with a `Target`, `Server` without |
| `Id(menuId)` | Identity stored in favorites (defaults to the command name). Pass the old name when renaming a command so favorites keep working |

Without `InCategory`, commands without a `Target` are listed under Server Commands.
