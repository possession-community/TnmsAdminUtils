# Admin Panel

A Panorama panel for the admin menu. Open it from `!admin` → Open Panel. Hold the inspect key (default F) to use the
cursor; the game stays playable while the panel is open.

Needs the Wuling Liuli addon (`csgo_addons/panorama_layout`, built with the panel) on the client.

## Permissions

| Permission | Allows |
|---|---|
| `tnms.adminutil.panel` | Opening the panel (the Open Panel item in `!admin`) |
| `tnms.adminutil.panel.ip` | Seeing players' IP addresses |
| `tnms.adminutil.panel.dev` | The Dev page. Without it the header has no Dev button |

## Pages

The pages are in the header bar; the sidebar holds the current page's own items.

| Page | Shows |
|---|---|
| Match | Map, round time, scores and team counts |
| Users | The player list (team filter in the sidebar). A row opens the player's details and the commands for them |
| Commands | Favorites / All / the categories, and the form of the chosen command (see [ADMIN_MENU](ADMIN_MENU.md)) |
| Dev | Server state, versions and the loaded modules |

## Dev

Values refresh every second.

### Server

| Group | Rows |
|---|---|
| Performance | Tickrate and server FPS (measured over the last second), frame time, frame jitter, tick |
| Uptime | Server uptime (process), map time, server time |
| Resources | Process memory, managed heap, GC collections (gen 0 / 1 / 2), entities / MaxEntities |
| Connections | Players, bots, connecting, slots (connected / MaxClients) |

Tickrate and FPS show `-` until the second sample, a second after the plugin loads. Entities are counted by index
while the page is shown.

### Versions

| Group | Rows |
|---|---|
| Runtime | ModSharp, Sharp.Shared, CS2 (`PatchVersion` and date from `csgo/steam.inf`), .NET, OS |
| Components | TnmsAdminUtils, TnmsPluginFoundation, Wuling.Abstract |

Versions with a commit hash show its first 7 characters.

### Plugins

The loaded ModSharp modules (name, display name, author, version; the state when it is not Running) in one list that
scrolls with the mouse wheel, up to 64. A row opens the module:

- Module: name, display name, author, state, version, module class, target framework, build configuration,
  description and repository URL (the last two only when the module's csproj sets them).
- Dependencies: the other assemblies in the module's load context (the ones it ships itself; shared libraries are
  loaded elsewhere), 18 per page.

ModSharp has no API listing its modules. The list is read from its module manager by reflection, the same list as
`ms modules`. If a ModSharp update changes those internals, the panel falls back to the assemblies loaded per module:
display name and state then show `-`, and author comes from the assembly's company.
