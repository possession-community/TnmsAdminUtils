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
| Overview | Match (sidebar): map, round time, scores and team counts |
| Users | The player list (team filter in the sidebar). A row opens the player's details and the commands for them |
| Commands | Favorites / All / the categories, and the form of the chosen command (see [ADMIN_MENU](ADMIN_MENU.md)) |
| Dev | Server state, versions and the loaded modules |

## Lists

Lists scroll with the mouse wheel while the cursor is shown (Panorama scrolls them in the client):

| List | Per page |
|---|---|
| Sidebar | 32; Previous / Next as its first / last entries when there are more |
| Command list | 64; a page bar (‹ Previous \| 2 / 3 \| Next ›) under it when there are more |
| Users | 64 (the server maximum) |
| Choices of a form field | 48, three rows high; ‹ › under them when there are more |
| Modules / dependencies (Dev) | 64; the page bar when there are more |

A list goes back to the top when what it shows changes (another tab, page, player, filter or sort); coming back to
the same list (e.g. from a module's details) keeps where it was scrolled to.

The command list is one column; the form of the open command is always beside it ("Select a command on the left"
without one), and opening or closing a form keeps the list where it is.

## Search

Search at the top of the command sidebars (the Commands page and a player's commands) opens the search tab. Above
the list is a search field: click it to type the words in chat (the field turns gold while waiting), click it again
to stop. ✕ clears the words. With no words yet, opening the tab starts waiting right away.

- Every chat message is the search, `cancel` included.
- Waiting also stops when moving to another tab or page, and after 60 seconds.
- A command matches when its name, label, description or category name contains every word (any case).
- A player's commands search only the commands that take a target.
- The words stay until cleared or the panel is closed, for the Commands page and a player's commands alike.
- The count is shown after the breadcrumb.

## Text input in the form

A field that needs text (and Type in chat in a field's choices) waits for your next chat message. Click the waiting
field again to cancel; a typed `cancel` is a value like any other here. The input expires after 60 seconds.

Target fields take any target string typed in chat, e.g. a plugin's selector such as `@zombies`, a name or a
SteamID64. It is resolved by TargetingManager: when nothing matches, the panel asks again; a field that takes one
player takes a string matching exactly one player and refuses more.

While the panel (or the chat menu) waits for text, admin chat (`@...`) leaves the message alone, so typing a selector
does not send it to the admins.

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
  loaded elsewhere), scrolling, 64 per page.

ModSharp has no API listing its modules. The list is read from its module manager by reflection, the same list as
`ms modules`. If a ModSharp update changes those internals, the panel falls back to the assemblies loaded per module:
display name and state then show `-`, and author comes from the assembly's company.
