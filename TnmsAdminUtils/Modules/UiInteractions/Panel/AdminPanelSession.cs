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

    // Command rows per page (tap-r{i}); the list scrolls, the form stays beside it.
    private const int ListSlots = 64;
    private const int FieldSlots = 6;
    // Choice cells per page (tap-pc{i}, rows of PickerColumns); the grid scrolls beyond three rows.
    private const int PickerSlots = 48;
    private const int PickerColumns = 4;
    // User list rows (tap-u{i}): the server maximum, scrolled; no pager.
    private const int UserRows = 64;
    // Sidebar items per page (tap-si{i}); the list scrolls, ▲▼ only with more.
    private const int SideSlots = 32;
    private const string Off = "tap-off";

    // Command list selections besides a category key: All is null.
    private const string FavoritesTab = "\0favorites";
    private const string SearchTab = "\0search";

    // Dev sub pages besides Server (null).
    private const string VersionsTab = "versions";
    private const string PluginsTab = "plugins";

    // Dev groups: the layout has two columns of two groups (tap-dvg{slot}) with these rows (slot 0 also holds a
    // module's details).
    private static readonly int[] DevGroupRows = [12, 9, 9, 9];
    // Rows of the module list (tap-mr{i}) and of a module's dependencies (tap-dvdr{i}) per page, all filled at once:
    // Panorama scrolls them on the client.
    private const int ModuleListRows = 64;
    private const int DependencyRows = 64;

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
    // Dev > Plugins: the module opened (by name), the module list's page and the names on its rows.
    private string? _module;
    private int _modulesPage;
    private readonly string?[] _moduleRows = new string?[ModuleListRows];
    // Command search: the words (kept while the panel is open, for the Commands page and a player's commands alike),
    // and the time a chat message is taken as new words until (0 while not waiting).
    private string? _search;
    private long _searchUntil;
    // What each scrolling list (by panel id) showed last; a change brings it back to the top.
    private readonly Dictionary<string, string> _scrollKeys = [];
    private List<SideItem> _side = [];
    private int _sidePage;
    private IReadOnlyList<AdminMenuEntry> _entries = [];
    private AdminCommandForm? _form;
    private List<AdminFormChoice> _choices = [];
    private bool _cursor;
    private bool _suspended;
    private int _listPage;
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

    public bool IsWaitingText => !_closed && ((_searchUntil != 0 && Environment.TickCount64 <= _searchUntil) || _form is { IsWaiting: true });

    public bool TryAcceptText(string message)
    {
        if (_closed || !(TryAcceptSearch(message) || (_form != null && _form.TryAcceptText(message))))
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

        if (_searchUntil != 0 && Environment.TickCount64 > _searchUntil)
            _searchUntil = 0;

        Render();
    }

    /// <summary>
    /// The search words while waiting for them; every message counts, "cancel" too (cancelling is in the panel).
    /// </summary>
    private bool TryAcceptSearch(string message)
    {
        if (_searchUntil == 0 || Environment.TickCount64 > _searchUntil)
            return false;

        var text = message.Trim().TrimStart('!').Trim();

        if (text.Length == 0)
            return false;

        // Another plugin's menu may be waiting for its keys.
        if (text.Length == 1 && char.IsDigit(text[0]) && TnmsPlugin.Wuling.Menu.GetActiveMenu(_player) != null)
            return false;

        _searchUntil = 0;
        _search = text;

        // The caller draws on the next frame; switch there too, after the message has been handled.
        _plugin.SharedSystem.GetModSharp().InvokeFrameAction(() =>
        {
            if (!_closed && Admin.IsValid)
                ShowTab(SearchTab);
        });

        return true;
    }

    /// <summary>
    /// The sidebar's Search: opens the search tab, waiting for words right away when there are none yet.
    /// </summary>
    private void OpenSearch()
    {
        ShowTab(SearchTab);

        if (_search is null)
            ToggleSearchInput();
    }

    /// <summary>
    /// The search field: waits for words in chat, or (while waiting) stops waiting.
    /// </summary>
    private void ToggleSearchInput()
    {
        if (_searchUntil != 0)
        {
            _searchUntil = 0;
            _context.PrintToChat("AdminMenu.Text.Cancelled");
        }
        else
        {
            _form?.CancelWait();
            _searchUntil = Environment.TickCount64 + AdminCommandContext.TextInputTimeoutSeconds * 1000L;
            _context.PrintToChat("AdminPanel.Search.Prompt", AdminCommandContext.TextInputTimeoutSeconds);
        }

        Render();
    }

    private void ClearSearch()
    {
        _search = null;
        _searchUntil = 0;
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

            case "tap-sfield":
                ToggleSearchInput();
                return;

            case "tap-sclear":
                ClearSearch();
                return;


            // Page bars: the command list and a module's dependencies page with _listPage, the module list its own.
            case "tap-lprev":
            case "tap-lnext":
            case "tap-dvdprev":
            case "tap-dvdnext":
                _listPage += panelId.EndsWith("next", StringComparison.Ordinal) ? 1 : -1;
                Render();
                return;

            case "tap-mprev":
            case "tap-mnext":
                _modulesPage += panelId == "tap-mnext" ? 1 : -1;
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
                ToggleForm(entry);
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
            // One chat input at a time: a field starting to wait ends the search wait.
            _searchUntil = 0;
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
        _searchUntil = 0;
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

        _searchUntil = 0;
        _tab = tab;
        _form = null;
        _listPage = 0;
        _module = null;
        Render();
    }

    /// <summary>
    /// Call on every render of a scrolling list with what it shows (page, tab, ...): when that changed, the list goes
    /// back to the top. The same content keeps its position, e.g. coming back from a module's details.
    /// </summary>
    private void ScrollKey(string panelId, string key)
    {
        if (_scrollKeys.TryGetValue(panelId, out var last) && last == key)
            return;

        _scrollKeys[panelId] = key;
        ResetScroll(panelId);
    }

    /// <summary>
    /// Brings a scroll panel back to the top: the server cannot set the position, so scrolling is turned off for a
    /// moment (tap-sreset). Both changes in one tick would never reach the client.
    /// </summary>
    private void ResetScroll(string panelId)
    {
        Class(panelId, "tap-sreset", true);

        _plugin.CreateTimer(0.1, () =>
        {
            if (!_closed && Admin.IsValid)
                Class(panelId, "tap-sreset", false);
        });
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

        _searchUntil = 0;
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
    /// Opens the form of a command, or closes it when it is already open. The list stays as it is (and where it is
    /// scrolled to).
    /// </summary>
    private void ToggleForm(AdminMenuEntry entry)
    {
        if (_form?.Entry == entry)
            CloseForm();
        else
            _form = new AdminCommandForm(_context, _player, entry, _playerTarget);

        Render();
    }

    private void CloseForm() => _form = null;

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

        // Only when there is a page that way.
        Class("tap-sprev", Off, _sidePage <= 0);
        Class("tap-snext", Off, _sidePage >= pages - 1);
        Text("tap-sprev", "t", L("AdminPanel.Side.Prev", _sidePage + 1, pages));
        Text("tap-snext", "t", L("AdminPanel.Side.Next", _sidePage + 1, pages));

        ScrollKey("tap-slist", $"{_page}/{_userView}/{_userTargetId}/{_sidePage}");
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
    /// Search (opens the search tab), Favorites, All, then the categories. With <paramref name="targetOnly"/> (a
    /// player's commands) only categories with commands that take a target. A selected category that went away falls
    /// back to All.
    /// </summary>
    private List<SideItem> CommandTabs(bool targetOnly)
    {
        var categories = _context.VisibleCategories()
            .Where(c => !targetOnly || _context.VisibleEntries(new AdminMenuList(c.Key)).Any(e => e.PrimaryTarget is not null))
            .ToList();

        if (_tab is not (null or FavoritesTab or SearchTab) && categories.All(c => c.Key != _tab))
            _tab = null;

        List<SideItem> items =
        [
            new(L("AdminPanel.Search.Tab"), _tab == SearchTab, OpenSearch),
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

        Header(L("AdminPanel.Users.Title"), L("AdminPanel.Users.Sub", clients.Count));
        ScrollKey("tap-uscroll", $"{_teamFilter}/{_sortColumn}/{_sortDescending}");

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
            var rowId = $"tap-u{row}";

            if (row >= clients.Count)
            {
                _rowTargets[row] = null;
                Class(rowId, Off, true);
                continue;
            }

            var client = clients[row];
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

        // By slot; a slot left null is hidden.
        var groups = new (string Heading, List<(string Label, string Value)> Rows)?[4];
        var title = view;
        string? step = null;
        IReadOnlyList<(string Name, string Version)>? dependencies = null;

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

            dependencies = AdminPanelModules.Dependencies(module);
            title = module.DisplayName ?? module.Name;
            step = view;
            view = module.Name;

            groups[0] = (L("AdminPanel.Dev.Group.Module"), AdminPanelModules.Info(module).Select(i => (L(i.LabelKey), i.Value)).ToList());
        }
        else
        {
            var args = new AdminPanelDevArgs(Admin, _service.Panel.Stats, L);

            foreach (var (group, slot, headingKey) in _tab == VersionsTab ? VersionGroups : ServerGroups)
            {
                var rows = _service.Panel.DevInfo.Of(group);

                if (rows.Count > 0)
                    groups[slot] = (L(headingKey), rows.Select(r => (L(r.LabelKey), r.Value(args))).ToList());
            }
        }

        ShowSection("tap-dv");
        Header(title, string.Empty, step, view);
        RenderDependencies(dependencies);

        for (var slot = 0; slot < groups.Length; slot++)
        {
            Class($"tap-dvg{slot}", Off, groups[slot] is null);

            if (groups[slot] is not { } group)
                continue;

            Text($"tap-dvh{slot}", "t", group.Heading);

            for (var row = 0; row < DevGroupRows[slot]; row++)
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
    /// A module's dependencies in the right column (scrolling, paged beyond <see cref="DependencyRows"/>); null
    /// hides the box for the usual groups.
    /// </summary>
    private void RenderDependencies(IReadOnlyList<(string Name, string Version)>? dependencies)
    {
        Class("tap-dvd", Off, dependencies is null);

        if (dependencies is null)
            return;

        var pages = Math.Max(1, (dependencies.Count + DependencyRows - 1) / DependencyRows);
        _listPage = Math.Clamp(_listPage, 0, pages - 1);

        Text("tap-dvdh", "t", L("AdminPanel.Dev.Group.Dependencies"));
        ListBar("tap-dvd", _listPage, pages);
        ScrollKey("tap-dvdscroll", $"{_module}/{_listPage}");

        List<(string Name, string Version)> rows = dependencies.Count == 0
            ? [(L("AdminPanel.Dev.Module.NoDependencies"), string.Empty)]
            : dependencies.Skip(_listPage * DependencyRows).Take(DependencyRows).ToList();

        for (var row = 0; row < DependencyRows; row++)
        {
            var rowId = $"tap-dvdr{row}";
            Class(rowId, Off, row >= rows.Count);

            if (row >= rows.Count)
                continue;

            Text(rowId, "k", rows[row].Name);
            Text(rowId, "v", rows[row].Version);
        }
    }

    /// <summary>
    /// Every module at once (up to <see cref="ModuleListRows"/> a page) with its display name, author and version; the state
    /// only when it is not Running. Panorama scrolls the list; the page bar only beyond a page.
    /// </summary>
    private void RenderModuleList(string view, IReadOnlyList<AdminPanelModule> all)
    {
        var pages = Math.Max(1, (all.Count + ModuleListRows - 1) / ModuleListRows);
        _modulesPage = Math.Clamp(_modulesPage, 0, pages - 1);
        var modules = all.Skip(_modulesPage * ModuleListRows).Take(ModuleListRows).ToList();

        ShowSection("tap-ml");
        Header(view, L("AdminPanel.Dev.PluginCount", all.Count), null, view);
        ListBar("tap-m", _modulesPage, pages);
        ScrollKey("tap-mscroll", $"modules/{_modulesPage}");

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
            SearchTab => SearchEntries(player != null),
            FavoritesTab => _context.FavoriteEntries(_favorites),
            null when player != null => _context.VisibleEntries(AdminMenuList.Players, _playerTarget),
            null => _context.VisibleCategories().SelectMany(c => _context.VisibleEntries(new AdminMenuList(c.Key))),
            { } category => _context.VisibleEntries(new AdminMenuList(category)),
        };

        _entries = player != null ? entries.Where(e => e.PrimaryTarget is not null).ToList() : entries.ToList();

        var view = _tab switch
        {
            SearchTab => L("AdminPanel.Search.Tab"),
            FavoritesTab => L("AdminPanel.Tab.Favorites"),
            null => L("AdminPanel.Commands.All"),
            { } category => _context.VisibleCategories().FirstOrDefault(c => c.Key == category) is { } found ? _context.CategoryName(found) : category,
        };

        var sub = _tab switch
        {
            FavoritesTab when _entries.Count == 0 => L("AdminMenu.Favorites.Empty"),
            SearchTab when _search != null => _entries.Count == 0 ? L("AdminPanel.Search.None") : L("AdminPanel.Search.Count", _entries.Count),
            _ => string.Empty,
        };

        RenderSearchBar();

        if (player != null)
            Header(player.Name, sub, player.Name, view);
        else
            Header(view, sub, null, view);

        var pages = Math.Max(1, (_entries.Count + ListSlots - 1) / ListSlots);
        _listPage = Math.Clamp(_listPage, 0, pages - 1);
        ListBar("tap-l", _listPage, pages);
        ScrollKey("tap-lscroll", $"{_page}/{_userView}/{_userTargetId}/{_tab}/{_search}/{_listPage}");

        Class("tap-ls", "tap-form", _form != null);
        Text("tap-fh", "t", L("AdminPanel.Form.Hint"));

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
            ScrollKey("tap-pkscroll", $"{form.Entry.MenuId}/{form.OpenField}/{form.PickerPage}");

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

    /// <summary>
    /// The search tab's box: the words, or a placeholder / the waiting note. The count is the header's note.
    /// </summary>
    private void RenderSearchBar()
    {
        Class("tap-ls", "tap-search", _tab == SearchTab);

        if (_tab != SearchTab)
            return;

        var waiting = _searchUntil != 0;

        Class("tap-sbox", "tap-wait", waiting);
        Class("tap-sfield", "tap-wait", waiting);
        Class("tap-sfield", "tap-empty", !waiting && _search is null);
        Text("tap-sfield", "t", waiting ? L("AdminPanel.Search.Waiting") : _search ?? L("AdminPanel.Search.Placeholder"));
    }

    /// <summary>
    /// Commands whose name, label, description or category name contains every word of the search (any case).
    /// A player's commands search only the ones that take a target (filtered by the caller).
    /// </summary>
    private IEnumerable<AdminMenuEntry> SearchEntries(bool forPlayer)
    {
        if (_search is null)
            return [];

        var words = _search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var categories = _context.VisibleCategories();

        var entries = forPlayer
            ? _context.VisibleEntries(AdminMenuList.Players, _playerTarget)
            : categories.SelectMany(c => _context.VisibleEntries(new AdminMenuList(c.Key)));

        return entries.Where(entry =>
        {
            var category = categories.FirstOrDefault(c => c.Key == entry.Category);

            string[] texts =
            [
                entry.CommandName,
                _context.TranslatedLabel(entry) ?? string.Empty,
                _context.Description(entry) ?? string.Empty,
                category is null ? string.Empty : _context.CategoryName(category),
            ];

            return words.All(w => texts.Any(t => t.Contains(w, StringComparison.OrdinalIgnoreCase)));
        });
    }

    private AdminMenuEntry? EntryAt(int slot)
    {
        if (slot >= ListSlots)
            return null;

        var index = _listPage * ListSlots + slot;
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

    /// <summary>
    /// A page bar under a scrolling list ({prefix}bar / prev / page / next): only with more than one page, dim at the
    /// ends (clicks there are clamped away by the next render).
    /// </summary>
    private void ListBar(string prefix, int page, int pages)
    {
        Class($"{prefix}bar", Off, pages <= 1);
        Text($"{prefix}prev", "t", $"‹  {L("AdminPanel.Prev")}");
        Text($"{prefix}next", "t", $"{L("AdminPanel.Next")}  ›");
        Text($"{prefix}page", "t", $"{page + 1} / {pages}");
        Class($"{prefix}prev", "tap-dis", page <= 0);
        Class($"{prefix}next", "tap-dis", page >= pages - 1);
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
