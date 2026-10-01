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

- Root order: Open Panel (with permission) / Favorites / Players / General / Server / Notification. Favorites is listed even when empty so the item numbers stay put.
- Command lists show the chat command next to the label, e.g. `!slay | Slay`.
- Players lists every command that takes a target, whatever its category.
- Targets list `@all` / `@ct` / `@t` / `@spec` and the players you can target.
- Commands you have no permission for are hidden. A root is hidden when it has no command.
- Every page has a Back item. The menu closes after execution.

The menu runs the command as you (`ms_<command> ...`), so permission checks, logs and broadcasts are the same as typing it in chat.

## Options

| Kind | How it is chosen |
|---|---|
| Preset | Values from `menu.json`, or "Type in chat" for any value |
| Choice | Fixed list (team, color, on/off, round end reason) |
| Text | Typed in chat |
| Optional | "Default (skip)" leaves the argument to the command's default |

### Text input

When a step needs text, the menu closes and asks in chat.

- Your next chat message is used as the value and is not shown in chat.
- Type `cancel` to stop.
- The input expires after 60 seconds; later messages are sent to chat as usual.
- After the input, the rest of the flow opens as a new menu.

Vote options are typed at once, separated by commas (`,` or `、`). At least 2 options are required.

## menu.json

Read when the plugin loads (created in the module directory with the default presets if missing).
After editing it, run `!adminmenu_reload` (permission `tnms.adminutil.menu.reload`).
Keys that are missing fall back to the defaults.

```json
{
  "Presets": {
    "slap": ["0", "1", "5", "10", "50", "100"],
    "give": ["ak47", "m4a1_silencer", "awp", "deagle", "knife"]
  }
}
```

| Key | Used by |
|---|---|
| slap / hp / money / setkev / drop / gravity / speed | Value of the command |
| freeze / blind / shake | Duration (seconds) |
| give | Weapon list |
| addtime / settime | Seconds |
| terminateround | Delay (seconds) |

## Command list cache

Each admin's list of usable commands is built once their permissions finish loading after joining; the menu and the
panel read from it. It is rebuilt when their permissions change, when commands are registered or removed, and on
`!adminmenu_reload`.

## Translations

Menu texts are `AdminMenu.*` keys in `lang/<culture>.json`.
Command labels are `AdminMenu.Command.<command name>`; the command name is shown when the key is missing.

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
| `Preset(key)` | Preset from `menu.json` |
| `Choice(titleKey, ...)` / `Toggle()` / `TeamChoice()` | Fixed choices |
| `Text(titleKey, quote)` | Text typed in chat |
| `TextList(titleKey, minCount)` | Comma separated texts, each quoted |
| `Optional(defaultRaw)` | Makes the last argument optional |
| `InCategory(category)` | Which list the command goes in (`Normal` / `Server` / `Notification`). Defaults to `Normal` with a `Target`, `Server` without |
| `Id(menuId)` | Identity stored in favorites (defaults to the command name). Pass the old name when renaming a command so favorites keep working |

Without `InCategory`, commands without a `Target` are listed under Server Commands.
