using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsPluginFoundation.Extensions.Client;
using Wuling.Abstract.Tianshi.Liuli;
using Wuling.Abstract.Tianshi.Registry;

namespace TnmsAdminUtils.Modules.UiInteractions.Panel;

/// <summary>
/// The admin panel of one player. Panel ids and classes are defined in tnms_admin_panel.lxml / .vcss.
/// Pages are in the header bar; the sidebar holds the current page's own items.
/// </summary>
public sealed class AdminPanelSession : IAdminSession
{
    // Same order as the header bar buttons (tap-nav{i}).
    private enum Page
    {
        Match,
        Users,
        Commands,
        Dev,
    }

    // What the Users page shows: the list, or one player's details / commands.
    private enum UserView
    {
        List,
        Detail,
        Commands,
    }

    /// <param name="Heading">A heading, not clickable</param>
    /// <param name="Indent">Listed under a heading</param>
    private sealed record SideItem(string Label, bool Selected, Action? Click, bool Heading = false, bool Indent = false);

    private const int ListSlots = 18;
    // Command slots per page while the form takes the second column.
    private const int FormListSlots = 9;
    private const int FieldSlots = 6;
    private const int PickerSlots = 12;
    private const int PickerColumns = 4;
    private const int UserRows = 16;
    private const int SideSlots = 14;
    private const string Off = "tap-off";

    // Command list selections besides a category key: All is null.
    private const string FavoritesTab = "\0favorites";

    // Dev sub pages besides Server (null).
    private const string VersionsTab = "versions";
    private const string PluginsTab = "plugins";

    // Dev groups: the layout has two columns of two groups (tap-dvg{slot}); the first of each column (0, 2) has
    // PluginRows rows (a module's details / dependencies), the second (1, 3) DevRows.
    private const int PluginRows = 18;
    private const int DevRows = 9;
    // Rows of the module list (tap-mr{i}), all filled at once: Panorama scrolls the list on the client.
    private const int ModuleListRows = 64;

    private static readonly (AdminPanelDevGroup Group, int Slot, string HeadingKey)[] ServerGroups =
    [
        (AdminPanelDevGroup.Performance, 0, "AdminPanel.Dev.Group.Performance"),
        (AdminPanelDevGroup.Uptime, 1, "AdminPanel.Dev.Group.Uptime"),
        (AdminPanelDevGroup.Resources, 2, "AdminPanel.Dev.Group.Resources"),
        (AdminPanelDevGroup.Connections, 3, "AdminPanel.Dev.Group.Connections"),
    ];

    private static readonly (AdminPanelDevGroup Group, int Slot, string HeadingKey)[] VersionGroups =
    [
        (AdminPanelDevGroup.Runtime, 0, "AdminPanel.Dev.Group.Runtime"),
        (AdminPanelDevGroup.Components, 2, "AdminPanel.Dev.Group.Components"),
    ];

    // Detail groups: id letter in the layout (tap-d{x}g / tap-d{x}h / tap-d{x}r{i}) and heading key.
    private static readonly (AdminPanelDetailGroup Group, char Id, string HeadingKey)[] DetailGroups =
    [
        (AdminPanelDetailGroup.User, 'u', "AdminPanel.Detail.Group.User"),
        (AdminPanelDetailGroup.InGame, 'i', "AdminPanel.Detail.Group.InGame"),
        (AdminPanelDetailGroup.Authority, 'a', "AdminPanel.Detail.Group.Authority"),
        (AdminPanelDetailGroup.Other, 'o', "AdminPanel.Detail.Group.Other"),
    ];

    // User list filter: the label key and the teams it keeps (null keeps everyone).
    private static readonly (string LabelKey, CStrikeTeam[]? Teams)[] TeamFilters =
    [
        ("AdminPanel.Tab.All", null),
        ("AdminPanel.Team.Ct", [CStrikeTeam.CT]),
        ("AdminPanel.Team.T", [CStrikeTeam.TE]),
        ("AdminPanel.Team.Spec", [CStrikeTeam.Spectator, CStrikeTeam.UnAssigned]),
    ];

    private static readonly string[] Sections = ["tap-ov", "tap-us", "tap-dt", "tap-ls", "tap-dv", "tap-ml"];
    private static readonly string[] NavKeys = ["AdminPanel.Nav.Match", "AdminPanel.Nav.Users", "AdminPanel.Nav.Commands", "AdminPanel.Nav.Dev"];
    // "x" collapses an unused slot; the rest follow AdminPanelColumnWidth.
    private static readonly string[] WidthSizes = ["x", "xs", "s", "m", "l", "xl"];

    private readonly TnmsAdminUtils _plugin;
    private readonly AdminMenuService _service;
    private readonly IPlayerEntry _player;
    private readonly ILiuliSurface _surface;
    private readonly AdminCommandContext _context;
    private readonly IGameClient?[] _rowTargets = new IGameClient?[UserRows];
    private readonly AdminFavorites _favorites;

    private Page _page;
    private UserView _userView;
    // The player opened from the user list; UserId tells a reconnect in the same slot apart.
    private IGameClient? _userTarget;
    private int _userTargetId;
    // Pre-filled target of the player's command pages.
    private AdminMenuValue? _playerTarget;
    // The command list shown: FavoritesTab, a category key, or null for All.
    private string? _tab;
    private int _teamFilter;
    // Dev > Plugins: the module opened (by name); the names on the module list's rows.
    private string? _module;
    private readonly string?[] _moduleRows = new string?[ModuleListRows];
    private List<SideItem> _side = [];
    private int _sidePage;
    private IReadOnlyList<AdminMenuEntry> _entries = [];
    private AdminCommandForm? _form;
    private List<AdminFormChoice> _choices = [];
    private bool _cursor;
    private bool _suspended;
    private int _listPage;
    private int _userPage;
    // User list sort by column key; null keeps the default order (team, then name).
    private string? _sortColumn;
    private bool _sortDescending;
    private bool _closed;

    public IGameClient Admin { get; }

    public AdminPanelSession(TnmsAdminUtils plugin, AdminMenuService service, IGameClient admin, IPlayerEntry player)
    {
        _plugin = plugin;
        _service = service;
        _player = player;
        _surface = service.Panel.Surface!;
        _context = new AdminCommandContext(plugin, service, admin);
        Admin = admin;
        _favorites = AdminFavorites.Load(admin);
    }

    public void Start()
    {
        for (var i = 0; i < NavKeys.Length; i++)
            Text($"tap-nav{i}", "t", L(NavKeys[i]));

        Text("tap-brand", "t", L("AdminPanel.Brand"));
        Text("tap-close", "t", L("AdminPanel.Nav.Close"));
        Text("tap-prev", "t", L("AdminPanel.Prev"));
        Text("tap-next", "t", L("AdminPanel.Next"));
        Text("tap-hint", "t", L("AdminPanel.CursorHint"));
        Text("tap-fx", "t", L("AdminMenu.Confirm.Execute"));

        _surface.Show(_player);

        GoTo(Page.Match);
    }

    /// <summary>
    /// Called every tick. The cursor is shown only while the admin holds the inspect key, so the game stays playable
    /// with the panel open. While a Wuling menu is open the panel steps aside (that menu takes the same key and the
    /// cursor), and comes back when the menu closes.
    /// </summary>
    public void UpdateInput(bool inspectHeld)
    {
        if (_closed)
            return;

        var menuOpen = TnmsPlugin.Wuling.Menu.GetActiveMenu(_player) != null;

        if (menuOpen != _suspended)
        {
            _suspended = menuOpen;

            if (menuOpen)
            {
                SetCapture(false);
                _surface.Hide(_player);
            }
            else
            {
                _surface.Show(_player);
            }
        }

        SetCapture(!_suspended && inspectHeld);
    }

    private void SetCapture(bool capture)
    {
        if (capture == _cursor)
            return;

        _cursor = capture;
        _surface.SetInputCapture(_player, capture);
    }

    public void Close()
    {
        if (_closed)
            return;

        _closed = true;
        _form = null;

        _surface.SetInputCapture(_player, false);
        _surface.Hide(_player);
        _service.OnSessionClosed(this);
    }

    public bool TryAcceptText(string message)
    {
        if (_closed || _form is null || !_form.TryAcceptText(message))
            return false;

        // Called from the say listener; draw on the next frame like the other input paths.
        _plugin.SharedSystem.GetModSharp().InvokeFrameAction(() =>
        {
            if (!_closed && Admin.IsValid)
                Render();
        });

        return true;
    }

    public void Refresh()
    {
        if (_closed || !Admin.IsValid)
            return;

        _form?.ExpireWait();
        Render();
    }

    public void OnClicked(string panelId)
    {
        if (_closed)
            return;

        switch (panelId)
        {
            case "tap-close":
                Close();
                return;

            case "tap-fm-close":
                CloseForm();
                Render();
                return;

            case "tap-fx":
                Execute();
                return;

            case "tap-prev":
            case "tap-next":
                var step = panelId == "tap-next" ? 1 : -1;

                if (_page == Page.Users && _userView == UserView.List)
                    _userPage += step;
                else
                    _listPage += step;

                Render();
                return;

            case "tap-sprev":
            case "tap-snext":
                _sidePage += panelId == "tap-snext" ? 1 : -1;
                Render();
                return;

            // Breadcrumb: the page's top, then the player's details (Users) or the module list (Dev).
            case "tap-bc0":
                GoTo(_page);
                return;

            case "tap-bc1":
                if (_page == Page.Dev)
                    CloseModule();
                else
                    ShowUser(UserView.Detail);

                return;

            case "tap-pkprev":
            case "tap-pknext":
                if (_form != null)
                    _form.PickerPage += panelId == "tap-pknext" ? 1 : -1;

                Render();
                return;
        }

        if (TryIndex(panelId, "tap-nav", out var nav))
        {
            // A click on Dev that arrives after the permission went away.
            if ((Page)nav != Page.Dev || CanSeeDev())
                GoTo((Page)nav);
        }
        else if (TryIndex(panelId, "tap-si", out var sideSlot))
        {
            var index = _sidePage * SideSlots + sideSlot;

            if (index < _side.Count)
                _side[index].Click?.Invoke();
        }
        else if (TryIndex(panelId, "tap-mr", out var moduleRow))
        {
            if (_page == Page.Dev && _tab == PluginsTab && _module is null && moduleRow < ModuleListRows && _moduleRows[moduleRow] is { } name)
                OpenModule(name);
        }
        else if (TryIndex(panelId, "tap-sort", out var column))
        {
            ToggleSort(column);
        }
        else if (TryIndex(panelId, "tap-u", out var row))
        {
            if (_rowTargets[row] is { IsValid: true } target)
                OpenUser(target);
        }
        else if (TryIndex(panelId, "tap-l", out var slot))
        {
            if (EntryAt(slot) is { } entry)
                ToggleForm(entry, _listPage * ListSlotsNow() + slot);
        }
        else if (TryIndex(panelId, "tap-s", out var star))
        {
            if (EntryAt(star) is { } entry)
            {
                _favorites.Toggle(entry.MenuId);
                Render();
            }
        }
        else if (TryIndex(panelId, "tap-fa", out var fieldA) || TryIndex(panelId, "tap-fb", out fieldA))
        {
            _form?.Click(fieldA);
            Render();
        }
        else if (TryIndex(panelId, "tap-pc", out var choice))
        {
            // Spacer cells past the last choice are still hit-testable; the bound check ignores them.
            var index = (_form?.PickerPage ?? 0) * PickerSlots + choice;

            if (index < _choices.Count)
            {
                _choices[index].Pick();
                Render();
            }
        }
    }

    private void GoTo(Page page)
    {
        _page = page;
        _tab = null;
        _sidePage = 0;
        _userView = UserView.List;
        _userTarget = null;
        _playerTarget = null;
        _form = null;
        _listPage = 0;
        _module = null;
        Render();
    }

    /// <summary>
    /// Switches the command list (Commands page, or the player's commands), or the Dev sub page.
    /// </summary>
    private void ShowTab(string? tab)
    {
        if (_page == Page.Users)
        {
            ShowUser(UserView.Commands, tab);
            return;
        }

        _tab = tab;
        _form = null;
        _listPage = 0;
        _module = null;
        Render();
    }

    private void OpenModule(string name)
    {
        _module = name;
        _listPage = 0;
        Render();
    }

    private void CloseModule()
    {
        if (_module is null)
            return;

        _module = null;
        _listPage = 0;
        Render();
    }

    private void OpenUser(IGameClient target)
    {
        _userTarget = target;
        _userTargetId = target.UserId.AsPrimitive();
        _playerTarget = AdminCommandContext.PlayerValue(target);
        _sidePage = 0;
        ShowUser(UserView.Detail);
    }

    private void ShowUser(UserView view, string? tab = null)
    {
        if (_userTarget is null)
            return;

        _userView = view;
        _tab = tab;
        _form = null;
        _listPage = 0;
        Render();
    }

    /// <summary>
    /// The opened player while they are still connected (a reconnect gets a new UserId).
    /// </summary>
    private IGameClient? CurrentUser()
        => _userTarget is { IsValid: true } target && target.UserId.AsPrimitive() == _userTargetId ? target : null;

    /// <summary>
    /// Opens the form of a command, or closes it when it is already open. The list page follows so the clicked
    /// command stays in view when the list switches between 18 and 9 per page.
    /// </summary>
    private void ToggleForm(AdminMenuEntry entry, int index)
    {
        if (_form?.Entry == entry)
        {
            CloseForm();
        }
        else
        {
            _form = new AdminCommandForm(_context, _player, entry, _playerTarget);
            _listPage = index / FormListSlots;
        }

        Render();
    }

    private void CloseForm()
    {
        if (_form is null)
            return;

        _listPage = _listPage * FormListSlots / ListSlots;
        _form = null;
    }

    /// <summary>
    /// Runs the command and keeps the form as it is, so the same command can be sent again.
    /// </summary>
    private void Execute()
    {
        if (_form is not { IsReady: true } form)
            return;

        var entry = form.Entry;
        var values = form.FinalValues();

        // Clicks arrive inside the panel's input handling; run the command on the next frame.
        _plugin.SharedSystem.GetModSharp().InvokeFrameAction(() =>
        {
            if (!_closed && Admin.IsValid)
                _context.Execute(entry, values);
        });
    }

    private void Render()
    {
        // The player left: back to the list.
        if (_page == Page.Users && _userView != UserView.List && CurrentUser() is null)
        {
            _userView = UserView.List;
            _userTarget = null;
            _playerTarget = null;
            _form = null;
        }

        // Checked every render, so a permission taken away mid-session closes the page.
        var canSeeDev = CanSeeDev();

        if (_page == Page.Dev && !canSeeDev)
        {
            _page = Page.Match;
            _tab = null;
            _listPage = 0;
            _module = null;
        }

        for (var i = 0; i < NavKeys.Length; i++)
            Class($"tap-nav{i}", "tap-sel", i == (int)_page);

        Class($"tap-nav{(int)Page.Dev}", Off, !canSeeDev);

        RenderSide();

        switch (_page)
        {
            case Page.Match:
                RenderMatch();
                break;
            case Page.Dev:
                RenderDev();
                break;
            case Page.Users when _userView == UserView.List:
                RenderUsers();
                break;
            case Page.Users when _userView == UserView.Detail:
                RenderDetail();
                break;
            default:
                RenderCommands();
                break;
        }
    }

    /// <summary>
    /// The current page's sidebar items, a page of <see cref="SideSlots"/> at a time.
    /// </summary>
    private void RenderSide()
    {
        _side = SideItems();

        var pages = Math.Max(1, (_side.Count + SideSlots - 1) / SideSlots);
        _sidePage = Math.Clamp(_sidePage, 0, pages - 1);

        for (var slot = 0; slot < SideSlots; slot++)
        {
            var id = $"tap-si{slot}";
            var index = _sidePage * SideSlots + slot;

            if (index >= _side.Count)
            {
                Class(id, Off, true);
                continue;
            }

            var item = _side[index];
            Class(id, Off, false);
            Class(id, "tap-sel", item.Selected);
            Class(id, "tap-sih", item.Heading);
            Class(id, "tap-ind", item.Indent);
            Text(id, "t", item.Label);
        }

        // Always shown, dim at the ends (both with a single page); clicks there are clamped away.
        Class("tap-sprev", "tap-dis", _sidePage <= 0);
        Class("tap-snext", "tap-dis", _sidePage >= pages - 1);
    }

    private List<SideItem> SideItems()
    {
        switch (_page)
        {
            case Page.Users when _userView == UserView.List:
            {
                List<SideItem> items = [new(L("AdminPanel.Side.Team"), false, null, Heading: true)];

                for (var i = 0; i < TeamFilters.Length; i++)
                {
                    var filter = i;
                    items.Add(new SideItem(L(TeamFilters[i].LabelKey), i == _teamFilter, () =>
                    {
                        _teamFilter = filter;
                        _userPage = 0;
                        Render();
                    }, Indent: true));
                }

                return items;
            }

            case Page.Users:
            {
                List<SideItem> items =
                [
                    new(L("AdminPanel.Side.Details"), _userView == UserView.Detail, () => ShowUser(UserView.Detail)),
                    new(L("AdminPanel.Detail.Execute"), false, null, Heading: true),
                ];

                items.AddRange(CommandTabs(targetOnly: true).Select(t => t with { Selected = t.Selected && _userView == UserView.Commands, Indent = true }));
                return items;
            }

            case Page.Commands:
                return CommandTabs(targetOnly: false);

            case Page.Dev:
                return
                [
                    new(L("AdminPanel.Dev.Server"), _tab is null, () => ShowTab(null)),
                    new(L("AdminPanel.Dev.Versions"), _tab == VersionsTab, () => ShowTab(VersionsTab)),
                    new(L("AdminPanel.Dev.Plugins"), _tab == PluginsTab, () => ShowTab(PluginsTab)),
                ];

            default:
                return [];
        }
    }

    /// <summary>
    /// Favorites, All, then the categories. With <paramref name="targetOnly"/> (a player's commands) only categories
    /// with commands that take a target. A selected category that went away falls back to All.
    /// </summary>
    private List<SideItem> CommandTabs(bool targetOnly)
    {
        var categories = _context.VisibleCategories()
            .Where(c => !targetOnly || _context.VisibleEntries(new AdminMenuList(c.Key)).Any(e => e.PrimaryTarget is not null))
            .ToList();

        if (_tab is not (null or FavoritesTab) && categories.All(c => c.Key != _tab))
            _tab = null;

        List<SideItem> items =
        [
            new(L("AdminPanel.Tab.Favorites"), _tab == FavoritesTab, () => ShowTab(FavoritesTab)),
            new(L("AdminPanel.Tab.All"), _tab is null, () => ShowTab(null)),
        ];

        foreach (var category in categories)
            items.Add(new SideItem(_context.CategoryName(category), _tab == category.Key, () => ShowTab(category.Key)));

        return items;
    }

    private void RenderMatch()
    {
        ShowSection("tap-ov");
        Pager(0, 1);

        var sharp = _plugin.SharedSystem.GetModSharp();
        var rules = sharp.GetGameRules();
        var teams = _plugin.SharedSystem.GetEntityManager();
        var clients = Clients().ToList();

        string TeamCount(CStrikeTeam team)
        {
            var members = clients.Where(c => c.GetPlayerController()?.Team == team).ToList();
            var alive = members.Count(c => c.GetPlayerPawn() is { IsAlive: true });
            return $"{alive} / {members.Count}";
        }

        Header(L("AdminPanel.Match.Title"), sharp.GetMapName() ?? string.Empty);

        (string Key, string Value)[] stats =
        [
            ("AdminPanel.Stat.Map", sharp.GetMapName() ?? "-"),
            ("AdminPanel.Stat.RoundTime", rules.IsWarmupPeriod ? L("AdminPanel.Stat.Warmup") : AdminPanelColumns.FormatDuration(rules.GetRoundRemainingTime())),
            ("AdminPanel.Stat.ScoreCt", teams.GetGlobalCStrikeTeam(CStrikeTeam.CT)?.Score.ToString() ?? "-"),
            ("AdminPanel.Stat.ScoreT", teams.GetGlobalCStrikeTeam(CStrikeTeam.TE)?.Score.ToString() ?? "-"),
            ("AdminPanel.Stat.Ct", TeamCount(CStrikeTeam.CT)),
            ("AdminPanel.Stat.T", TeamCount(CStrikeTeam.TE)),
            ("AdminPanel.Stat.Spec", clients.Count(c => c.GetPlayerController()?.Team is CStrikeTeam.Spectator or CStrikeTeam.UnAssigned).ToString()),
            ("AdminPanel.Stat.Players", $"{clients.Count} / {sharp.GetGlobals().MaxClients}"),
        ];

        for (var i = 0; i < stats.Length; i++)
        {
            Text($"tap-ov{i}", "k", L(stats[i].Key));
            Text($"tap-ov{i}", "v", stats[i].Value);
        }
    }

    private void RenderUsers()
    {
        ShowSection("tap-us");

        var teams = TeamFilters[_teamFilter].Teams;
        var columns = _service.Panel.Columns.Columns;
        var clients = SortUsers(Clients()
            .Where(c => teams is null || teams.Contains(c.GetPlayerController()?.Team ?? CStrikeTeam.UnAssigned))
            .OrderBy(AdminPanelColumns.TeamOrder)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase), columns);

        var pages = Math.Max(1, (clients.Count + UserRows - 1) / UserRows);
        _userPage = Math.Clamp(_userPage, 0, pages - 1);

        Header(L("AdminPanel.Users.Title"), L("AdminPanel.Users.Sub", clients.Count));
        Pager(_userPage, pages);

        for (var slot = 0; slot < AdminPanelColumns.MaxColumns; slot++)
        {
            var size = slot < columns.Count ? WidthSizes[(int)columns[slot].Width + 1] : "x";

            foreach (var candidate in WidthSizes)
                Class("tap-us", $"w{slot}-{candidate}", candidate == size);

            if (slot >= columns.Count)
                continue;

            var sorted = columns[slot].Key == _sortColumn;
            var arrow = sorted ? _sortDescending ? " ▼" : " ▲" : string.Empty;

            Text("tap-uh", $"c{slot}", L(columns[slot].HeaderKey) + arrow);
            Class($"tap-sort{slot}", "tap-sortable", columns[slot].SortKey is not null);
            Class($"tap-sort{slot}", "tap-sorted", sorted);
        }

        for (var row = 0; row < UserRows; row++)
        {
            var index = _userPage * UserRows + row;
            var rowId = $"tap-u{row}";

            if (index >= clients.Count)
            {
                _rowTargets[row] = null;
                Class(rowId, Off, true);
                continue;
            }

            var client = clients[index];
            _rowTargets[row] = client;
            Class(rowId, Off, false);

            for (var slot = 0; slot < columns.Count; slot++)
                Text(rowId, $"c{slot}", columns[slot].Value(client, Admin));
        }
    }

    /// <summary>
    /// The opened player's details in headed groups (an empty group is hidden); their commands are in the sidebar.
    /// </summary>
    private void RenderDetail()
    {
        var target = CurrentUser()!;

        ShowSection("tap-dt");
        Pager(0, 1);
        Header(target.Name, string.Empty, target.Name);

        var args = new AdminPanelDetailArgs(target, Admin, L);

        foreach (var (group, id, headingKey) in DetailGroups)
        {
            var fields = _service.Panel.Details.Of(group, _service.Panel.Columns);

            Class($"tap-d{id}g", Off, fields.Count == 0);
            Text($"tap-d{id}h", "t", L(headingKey));

            for (var row = 0; row < AdminPanelDetails.Capacity(group); row++)
            {
                var rowId = $"tap-d{id}r{row}";

                if (row >= fields.Count)
                {
                    Class(rowId, Off, true);
                    continue;
                }

                Class(rowId, Off, false);
                Text(rowId, "k", L(fields[row].LabelKey));
                Text(rowId, "v", fields[row].Value(args));
            }
        }
    }

    /// <summary>
    /// Dev: the server's measurements, the versions or one module, as headed groups of "label  value" rows; the
    /// module list has its own section.
    /// </summary>
    private void RenderDev()
    {
        var view = L(_tab switch
        {
            VersionsTab => "AdminPanel.Dev.Versions",
            PluginsTab => "AdminPanel.Dev.Plugins",
            _ => "AdminPanel.Dev.Server",
        });

        // By slot; a slot left null is hidden. Wide groups give the label (a module / assembly name) most of the row.
        var groups = new (string Heading, List<(string Label, string Value)> Rows, bool Wide)?[4];
        var title = view;
        string? step = null;
        var pages = 1;

        if (_tab == PluginsTab)
        {
            var modules = AdminPanelModules.List(_plugin.SharedSystem);

            // The list, or the opened module was unloaded: back to the list.
            if (_module is null || modules.FirstOrDefault(m => m.Name == _module) is not { } module)
            {
                _module = null;
                RenderModuleList(view, modules);
                return;
            }

            var dependencies = AdminPanelModules.Dependencies(module);
            pages = Math.Max(1, (dependencies.Count + PluginRows - 1) / PluginRows);
            _listPage = Math.Clamp(_listPage, 0, pages - 1);

            title = module.DisplayName ?? module.Name;
            step = view;
            view = module.Name;

            groups[0] = (L("AdminPanel.Dev.Group.Module"), AdminPanelModules.Info(module).Select(i => (L(i.LabelKey), i.Value)).ToList(), false);
            groups[2] = (L("AdminPanel.Dev.Group.Dependencies"), dependencies.Count == 0
                ? [(L("AdminPanel.Dev.Module.NoDependencies"), string.Empty)]
                : dependencies.Skip(_listPage * PluginRows).Take(PluginRows).ToList(), true);
        }
        else
        {
            var args = new AdminPanelDevArgs(Admin, _service.Panel.Stats, L);

            foreach (var (group, slot, headingKey) in _tab == VersionsTab ? VersionGroups : ServerGroups)
            {
                var rows = _service.Panel.DevInfo.Of(group);

                if (rows.Count > 0)
                    groups[slot] = (L(headingKey), rows.Select(r => (L(r.LabelKey), r.Value(args))).ToList(), false);
            }
        }

        ShowSection("tap-dv");
        Header(title, string.Empty, step, view);
        Pager(_listPage, pages);

        for (var slot = 0; slot < groups.Length; slot++)
        {
            Class($"tap-dvg{slot}", Off, groups[slot] is null);

            if (groups[slot] is not { } group)
                continue;

            Class($"tap-dvg{slot}", "tap-dvw", group.Wide);
            Text($"tap-dvh{slot}", "t", group.Heading);

            for (var row = 0; row < (slot % 2 == 0 ? PluginRows : DevRows); row++)
            {
                var rowId = $"tap-dv{slot}r{row}";

                if (row >= group.Rows.Count)
                {
                    Class(rowId, Off, true);
                    continue;
                }

                Class(rowId, Off, false);
                Text(rowId, "k", group.Rows[row].Label);
                Text(rowId, "v", group.Rows[row].Value);
            }
        }
    }

    /// <summary>
    /// Every module at once (up to <see cref="ModuleListRows"/>) with its display name, author and version; the state
    /// only when it is not Running. Panorama scrolls the list, so there is no pager.
    /// </summary>
    private void RenderModuleList(string view, IReadOnlyList<AdminPanelModule> modules)
    {
        ShowSection("tap-ml");
        Header(view, L("AdminPanel.Dev.PluginCount", modules.Count), null, view);
        Pager(0, 1);

        string[] heads = ["AdminPanel.Dev.Module.Name", "AdminPanel.Dev.Module.DisplayName", "AdminPanel.Dev.Module.Author", "AdminPanel.Dev.Module.Version"];

        for (var i = 0; i < heads.Length; i++)
            Text("tap-mh", $"c{i}", L(heads[i]));

        for (var row = 0; row < ModuleListRows; row++)
        {
            var rowId = $"tap-mr{row}";

            if (row >= modules.Count)
            {
                _moduleRows[row] = null;
                Class(rowId, Off, true);
                continue;
            }

            var module = modules[row];
            _moduleRows[row] = module.Name;

            Class(rowId, Off, false);
            Text(rowId, "c0", module.Name);
            Text(rowId, "c1", module.DisplayName ?? "-");
            Text(rowId, "c2", module.Author ?? "-");
            Text(rowId, "c3", module.State is null or "Running" ? module.Version : $"{module.Version}  {module.State}");
        }
    }

    private bool CanSeeDev() => TnmsPlugin.AdminManager.PlayerHasPermission(Admin.SteamId, AdminPanelService.DevPermission);

    /// <summary>
    /// Header click: a new column sorts ascending, the same column flips the direction.
    /// </summary>
    private void ToggleSort(int slot)
    {
        var columns = _service.Panel.Columns.Columns;

        if (slot >= columns.Count || columns[slot].SortKey is null)
            return;

        var key = columns[slot].Key;
        _sortDescending = key == _sortColumn && !_sortDescending;
        _sortColumn = key;
        _userPage = 0;
        Render();
    }

    /// <summary>
    /// Sorts by the chosen column. Ties keep the incoming order and empty values stay at the bottom either way.
    /// </summary>
    private List<IGameClient> SortUsers(IEnumerable<IGameClient> clients, IReadOnlyList<AdminPanelColumn> columns)
    {
        if (columns.FirstOrDefault(c => c.Key == _sortColumn)?.SortKey is not { } sortKey)
            return clients.ToList();

        var comparer = Comparer<IComparable?>.Create(AdminPanelColumns.CompareSortKeys);
        var keyed = clients.Select(c => (Client: c, Key: sortKey(c, Admin))).OrderBy(k => k.Key is null);
        var ordered = _sortDescending ? keyed.ThenByDescending(k => k.Key, comparer) : keyed.ThenBy(k => k.Key, comparer);

        return ordered.Select(k => k.Client).ToList();
    }

    /// <summary>
    /// Command lists (Commands, or a player's commands with the player pre-filled and only commands that take a
    /// target): two columns, or one beside the form of the open command.
    /// </summary>
    private void RenderCommands()
    {
        ShowSection("tap-ls");

        var player = _page == Page.Users ? CurrentUser()! : null;

        IEnumerable<AdminMenuEntry> entries = _tab switch
        {
            FavoritesTab => _context.FavoriteEntries(_favorites),
            null when player != null => _context.VisibleEntries(AdminMenuList.Players, _playerTarget),
            null => _context.VisibleCategories().SelectMany(c => _context.VisibleEntries(new AdminMenuList(c.Key))),
            { } category => _context.VisibleEntries(new AdminMenuList(category)),
        };

        _entries = player != null ? entries.Where(e => e.PrimaryTarget is not null).ToList() : entries.ToList();

        var view = _tab switch
        {
            FavoritesTab => L("AdminPanel.Tab.Favorites"),
            null => L("AdminPanel.Commands.All"),
            { } category => _context.VisibleCategories().FirstOrDefault(c => c.Key == category) is { } found ? _context.CategoryName(found) : category,
        };

        var sub = _tab == FavoritesTab && _entries.Count == 0 ? L("AdminMenu.Favorites.Empty") : string.Empty;

        if (player != null)
            Header(player.Name, sub, player.Name, view);
        else
            Header(view, sub, null, view);

        var slots = ListSlotsNow();
        var pages = Math.Max(1, (_entries.Count + slots - 1) / slots);
        _listPage = Math.Clamp(_listPage, 0, pages - 1);
        Pager(_listPage, pages);

        Class("tap-ls", "tap-form", _form != null);

        for (var slot = 0; slot < ListSlots; slot++)
        {
            var rowId = $"tap-r{slot}";

            if (EntryAt(slot) is not { } entry)
            {
                Class(rowId, Off, true);
                continue;
            }

            var itemId = $"tap-l{slot}";
            var label = _context.TranslatedLabel(entry);

            // The chat command small above the label; without a label the command is the label.
            Class(rowId, Off, false);
            Class(itemId, "tap-cmd", label != null);
            Class(itemId, "tap-on", _form?.Entry == entry);
            Text(itemId, "c", label != null ? $"!{entry.CommandName}" : string.Empty);
            Text(itemId, "t", label ?? $"!{entry.CommandName}");

            Class($"tap-s{slot}", "tap-fav", _favorites.Contains(entry.MenuId));
        }

        if (_form != null)
            RenderForm(_form);
    }

    private void RenderForm(AdminCommandForm form)
    {
        var entry = form.Entry;
        var label = _context.TranslatedLabel(entry);

        Text("tap-fm-name", "c", label != null ? $"!{entry.CommandName}" : string.Empty);
        Text("tap-fm-name", "t", label ?? $"!{entry.CommandName}");

        var description = _context.Description(entry);
        Class("tap-fm-d", Off, description is null);
        Text("tap-fm-d", "t", description ?? string.Empty);

        var usage = _context.Usage(entry);
        Class("tap-fm-u", Off, usage is null);
        Text("tap-fm-u", "t", usage ?? string.Empty);

        // Fields up to the open one go above the choice grid (a slots), the rest below it (b slots).
        for (var i = 0; i < FieldSlots; i++)
        {
            var exists = i < entry.Arguments.Count;
            var above = exists && (form.OpenField < 0 || i <= form.OpenField);

            RenderField($"tap-fa{i}", form, i, above);
            RenderField($"tap-fb{i}", form, i, exists && !above);
        }

        _choices = form.Choices();
        Class("tap-pk", "tap-show", form.OpenField >= 0);

        if (form.OpenField >= 0)
        {
            var pages = Math.Max(1, (_choices.Count + PickerSlots - 1) / PickerSlots);
            form.PickerPage = Math.Clamp(form.PickerPage, 0, pages - 1);

            var first = form.PickerPage * PickerSlots;

            // Empty rows collapse; empty cells in a used row stay as invisible spacers so the columns keep their width.
            for (var row = 0; row < PickerSlots / PickerColumns; row++)
                Class($"tap-pkr{row}", Off, first + row * PickerColumns >= _choices.Count);

            for (var slot = 0; slot < PickerSlots; slot++)
            {
                var index = first + slot;
                var id = $"tap-pc{slot}";

                if (index >= _choices.Count)
                {
                    Class(id, "tap-void", true);
                    continue;
                }

                Class(id, "tap-void", false);
                Class(id, "tap-sel", _choices[index].Selected);
                Text(id, "t", _choices[index].Label);
            }

            Class("tap-pk-pager", Off, pages <= 1);
            Text("tap-pkpage", "t", $"{form.PickerPage + 1} / {pages}");
            PageButtons("tap-pkprev", "tap-pknext", form.PickerPage, pages);
        }

        Text("tap-fm-line", "t", _context.CommandLine(entry, form.PreviewValues));

        var target = entry.PrimaryTargetIndex >= 0 ? form.Values[entry.PrimaryTargetIndex] : null;
        Class("tap-fm-tg", Off, target is null);
        Text("tap-fm-tg", "t", target is null ? string.Empty : $"{L("AdminMenu.Confirm.Targets")}: {_context.DescribeTargets(target)}");

        Class("tap-fx", "tap-dis", !form.IsReady);
    }

    private void RenderField(string id, AdminCommandForm form, int index, bool show)
    {
        Class(id, Off, !show);

        if (!show)
            return;

        var argument = form.Entry.Arguments[index];
        var value = form.Values[index];
        var waiting = form.WaitField == index;
        var open = form.OpenField == index;
        var isText = !argument.HasChoices;

        var shown = waiting ? L("AdminPanel.Form.Waiting")
            : value != null ? value.Display
            : argument.IsOptional ? L("AdminMenu.Default")
            : L("AdminPanel.Form.NotSet");

        Text(id, "k", _context.FieldLabel(argument));
        Text(id, "v", shown);
        Text(id, "a", isText && !argument.IsOptional ? string.Empty : open ? "▲" : "▼");

        Class(id, "tap-open", open);
        Class(id, "tap-wait", waiting);
        Class(id, "tap-empty", !waiting && value is null);
    }

    private int ListSlotsNow() => _form != null ? FormListSlots : ListSlots;

    private AdminMenuEntry? EntryAt(int slot)
    {
        var slots = ListSlotsNow();

        if (slot >= slots)
            return null;

        var index = _listPage * slots + slot;
        return index < _entries.Count ? _entries[index] : null;
    }

    private IEnumerable<IGameClient> Clients()
        => _plugin.SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true).Where(c => !c.IsHltv);

    /// <summary>
    /// Title, and the breadcrumb: the page (a button back to its top), then <paramref name="step"/> (a button, the
    /// player's details) and <paramref name="current"/> (text), each left out when null. <paramref name="sub"/> follows it.
    /// </summary>
    private void Header(string title, string sub, string? step = null, string? current = null)
    {
        Text("tap-title", "t", title);
        Text("tap-bc0", "t", L(NavKeys[(int)_page]));

        Class("tap-bcs0", Off, step is null);
        Class("tap-bc1", Off, step is null);
        Text("tap-bc1", "t", step ?? string.Empty);

        Class("tap-bcs1", Off, current is null);
        Class("tap-bc2", Off, current is null);
        Text("tap-bc2", "t", current ?? string.Empty);

        Text("tap-sub", "t", sub);
    }

    private void Pager(int page, int pages)
    {
        var paged = pages > 1;
        Class("tap-prev", Off, !paged);
        Class("tap-next", Off, !paged);
        Class("tap-page", Off, !paged);
        Text("tap-page", "t", $"{page + 1} / {pages}");
        PageButtons("tap-prev", "tap-next", page, pages);
    }

    /// <summary>
    /// Hides prev on the first page and next on the last one. Invisible rather than collapsed so the pager does not
    /// shift; a click on a hidden button is clamped away by the next render.
    /// </summary>
    private void PageButtons(string prevId, string nextId, int page, int pages)
    {
        Class(prevId, "tap-ghost", page <= 0);
        Class(nextId, "tap-ghost", page >= pages - 1);
    }

    private void ShowSection(string id)
    {
        foreach (var section in Sections)
            Class(section, Off, section != id);
    }

    private void Text(string panelId, string variable, string value) => _surface.SetText(_player, panelId, variable, value);

    private void Class(string panelId, string className, bool present)
        => _surface.SetClass(_player, panelId, className, present ? LiuliClassState.Present : LiuliClassState.Absent);

    private static bool TryIndex(string panelId, string prefix, out int index)
    {
        index = -1;
        return panelId.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(panelId.AsSpan(prefix.Length), out index);
    }

    private string L(string key) => _context.L(key);

    private string L(string key, params object[] args) => _context.L(key, args);
}
