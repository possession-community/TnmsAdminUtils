using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsPluginFoundation.Extensions.Client;
using Wuling.Abstract.Tianshi.Liuli;
using Wuling.Abstract.Tianshi.Registry;

namespace TnmsAdminUtils.Modules.UiInteractions.Panel;

/// <summary>
/// The admin panel of one player. Panel ids and classes are defined in tnms_admin_panel.lxml / .vcss.
/// </summary>
public sealed class AdminPanelSession : IAdminFlowView, IAdminSession
{
    // Same order as the sidebar buttons (tap-nav{i}).
    private enum Page
    {
        Overview,
        Favorites,
        Users,
        Commands,
        Server,
        Notification,
    }

    private const int ListSlots = 16;
    private const int UserRows = 16;
    private const string Off = "tap-off";

    private const int ConfirmRows = 6;

    private static readonly string[] Sections = ["tap-ov", "tap-us", "tap-ls", "tap-cf"];
    private static readonly string[] NavKeys = ["AdminPanel.Nav.Overview", "AdminPanel.Nav.Favorites", "AdminPanel.Nav.Users", "AdminPanel.Nav.Commands", "AdminPanel.Nav.Server", "AdminPanel.Nav.Notification"];
    // "x" collapses an unused slot; the rest follow AdminPanelColumnWidth.
    private static readonly string[] WidthSizes = ["x", "xs", "s", "m", "l", "xl"];

    private readonly TnmsAdminUtils _plugin;
    private readonly AdminMenuService _service;
    private readonly IPlayerEntry _player;
    private readonly ILiuliSurface _surface;
    private readonly IGameClient?[] _rowTargets = new IGameClient?[UserRows];
    private readonly AdminFavorites _favorites;

    private Page _page;
    private AdminFlow? _flow;
    private string _listTitle = string.Empty;
    private IReadOnlyList<AdminFlowItem> _items = [];
    private AdminFlowConfirm? _confirm;
    private Action? _back;
    private bool _cursor;
    private bool _suspended;
    private string? _waitPrompt;
    private string? _emptyText;
    private string? _usage;
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
        Admin = admin;
        _favorites = AdminFavorites.Load(admin);
    }

    public void Start()
    {
        for (var i = 0; i < NavKeys.Length; i++)
            Text($"tap-nav{i}", "t", L(NavKeys[i]));

        Text("tap-brand", "t", L("AdminPanel.Brand"));
        Text("tap-close", "t", L("AdminPanel.Nav.Close"));
        Text("tap-back", "t", L("AdminMenu.Back"));
        Text("tap-prev", "t", L("AdminPanel.Prev"));
        Text("tap-next", "t", L("AdminPanel.Next"));
        Text("tap-hint", "t", L("AdminPanel.CursorHint"));

        _surface.Show(_player);

        GoTo(Page.Overview);
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
        _flow?.Abandon();
        _flow = null;

        _surface.SetInputCapture(_player, false);
        _surface.Hide(_player);
        _service.OnSessionClosed(this);
    }

    public bool TryAcceptText(string message) => _flow?.TryAcceptText(message) ?? false;

    public void Refresh()
    {
        if (_closed || !Admin.IsValid)
            return;

        if (_flow != null)
            _flow.Refresh();
        else
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

            case "tap-back":
                _back?.Invoke();
                return;

            case "tap-cf-exec":
                _confirm?.Execute();
                return;

            case "tap-prev":
            case "tap-next":
                var step = panelId == "tap-next" ? 1 : -1;

                if (_flow != null)
                    _listPage += step;
                else
                    _userPage += step;

                Render();
                return;
        }

        if (TryIndex(panelId, "tap-nav", out var nav))
        {
            GoTo((Page)nav);
        }
        else if (TryIndex(panelId, "tap-sort", out var column))
        {
            ToggleSort(column);
        }
        else if (TryIndex(panelId, "tap-u", out var row))
        {
            if (_rowTargets[row] is { IsValid: true } target)
                StartFlow(flow => flow.StartWithTarget(AdminFlow.PlayerValue(target)));
        }
        else if (TryIndex(panelId, "tap-l", out var slot))
        {
            var index = _listPage * ListSlots + slot;

            if (index < _items.Count)
                _items[index].OnSelect?.Invoke();
        }
        else if (TryIndex(panelId, "tap-s", out var star))
        {
            var index = _listPage * ListSlots + star;

            if (index < _items.Count && _items[index].FavoriteId is { } favoriteId)
            {
                _favorites.Toggle(favoriteId);
                Refresh();
            }
        }
    }

    bool IAdminFlowView.KeepHistoryAfterText => true;

    void IAdminFlowView.Show(string title, IReadOnlyList<AdminFlowItem> items, Action? back, string? usage, string? emptyText)
    {
        _emptyText = items.Count == 0 ? emptyText : null;
        _usage = usage;
        if (title != _listTitle)
            _listPage = 0;

        _listTitle = title;
        _items = items;
        _confirm = null;
        _back = back;
        _waitPrompt = null;
        Render();
    }

    void IAdminFlowView.ShowConfirm(AdminFlowConfirm confirm, Action? back)
    {
        _usage = confirm.Usage;
        _confirm = confirm;
        _back = back;
        Render();
    }

    void IAdminFlowView.WaitText(string title, Action back, string? usage)
    {
        _usage = usage;
        _listTitle = title;
        _items = [];
        _confirm = null;
        _back = back;
        _waitPrompt = L("AdminPanel.WaitText");
        _emptyText = null;
        _listPage = 0;
        Render();
    }

    /// <summary>
    /// Returns to the page the flow was started from; Commands / Server show their command list again.
    /// </summary>
    void IAdminFlowView.Finish()
    {
        if (_closed)
            return;

        _flow = null;
        GoTo(_page);
    }

    private void GoTo(Page page)
    {
        _flow?.Abandon();
        _flow = null;
        _page = page;
        _confirm = null;
        _usage = null;
        _back = null;

        switch (page)
        {
            case Page.Favorites:
                StartFlow(flow => flow.StartFavorites(_favorites));
                break;
            case Page.Commands:
                StartFlow(flow => flow.StartTree(AdminMenuTree.Commands));
                break;
            case Page.Server:
                StartFlow(flow => flow.StartTree(AdminMenuTree.Server));
                break;
            case Page.Notification:
                StartFlow(flow => flow.StartTree(AdminMenuTree.Notification));
                break;
            default:
                Render();
                break;
        }
    }

    private void StartFlow(Action<AdminFlow> start)
    {
        _flow?.Abandon();
        _flow = new AdminFlow(_plugin, _service, Admin, _player, this);
        _listTitle = string.Empty;
        _confirm = null;
        start(_flow);
    }

    private void Render()
    {
        for (var i = 0; i < NavKeys.Length; i++)
            Class($"tap-nav{i}", "tap-sel", i == (int)_page);

        if (_flow != null && _confirm != null)
            RenderConfirm(_confirm);
        else if (_flow != null)
            RenderList();
        else if (_page == Page.Users)
            RenderUsers();
        else
            RenderOverview();
    }

    private void RenderConfirm(AdminFlowConfirm confirm)
    {
        ShowSection("tap-cf");
        Header(L("AdminPanel.Confirm.Title"), string.Empty);
        Pager(0, 1, hasBack: _back != null);

        Text("tap-cf-cmd", "t", confirm.Command);
        Text("tap-cf-line", "t", confirm.Preview);
        Text("tap-cf-exec", "t", confirm.ExecuteLabel);

        for (var i = 0; i < ConfirmRows; i++)
        {
            var rowId = $"tap-cf{i}";

            if (i >= confirm.Rows.Count)
            {
                Class(rowId, Off, true);
                continue;
            }

            Class(rowId, Off, false);
            Text(rowId, "k", confirm.Rows[i].Key);
            Text(rowId, "v", confirm.Rows[i].Value);
        }
    }

    private void RenderOverview()
    {
        ShowSection("tap-ov");
        Pager(0, 1, hasBack: false);

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

        Header(L("AdminPanel.Overview.Title"), sharp.GetMapName() ?? string.Empty);

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

        var columns = _service.Panel.Columns.Columns;
        var clients = SortUsers(Clients()
            .OrderBy(AdminPanelColumns.TeamOrder)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase), columns);

        var pages = Math.Max(1, (clients.Count + UserRows - 1) / UserRows);
        _userPage = Math.Clamp(_userPage, 0, pages - 1);

        Header(L("AdminPanel.Users.Title"), L("AdminPanel.Users.Sub", clients.Count));
        Pager(_userPage, pages, hasBack: false);

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

    private void RenderList()
    {
        ShowSection("tap-ls");
        Header(_listTitle, _waitPrompt ?? _emptyText ?? string.Empty);

        var pages = Math.Max(1, (_items.Count + ListSlots - 1) / ListSlots);
        _listPage = Math.Clamp(_listPage, 0, pages - 1);
        Pager(_listPage, pages, hasBack: _back != null);

        for (var slot = 0; slot < ListSlots; slot++)
        {
            var index = _listPage * ListSlots + slot;
            var rowId = $"tap-r{slot}";
            var itemId = $"tap-l{slot}";
            var starId = $"tap-s{slot}";

            if (index >= _items.Count)
            {
                Class(rowId, Off, true);
                continue;
            }

            var item = _items[index];
            Class(rowId, Off, false);
            Class(itemId, "tap-dis", item.OnSelect is null);
            Class(itemId, "tap-pri", item.Style == AdminFlowItemStyle.Primary);
            // Command items: the chat command small above the label. Without a label the command is the label.
            var twoLine = item.Command is not null && item.Label.Length > 0;
            Class(itemId, "tap-cmd", twoLine);
            Text(itemId, "c", twoLine ? item.Command! : string.Empty);
            Text(itemId, "t", item.Label.Length > 0 ? item.Label : item.Command ?? string.Empty);

            Class(starId, Off, item.FavoriteId is null);
            Class(starId, "tap-fav", item.FavoriteId is { } id && _favorites.Contains(id));
        }
    }

    private IEnumerable<IGameClient> Clients()
        => _plugin.SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true).Where(c => !c.IsHltv);

    private void Header(string title, string sub)
    {
        Text("tap-title", "t", title);
        Text("tap-sub", "t", sub);

        // Only flow pages of a chosen command carry a usage line.
        var usage = _flow != null ? _usage : null;
        Class("tap-usage", Off, usage is null);
        Text("tap-usage", "t", usage is null ? string.Empty : $"> {usage}");
    }

    private void Pager(int page, int pages, bool hasBack)
    {
        Class("tap-back", Off, !hasBack);

        var paged = pages > 1;
        Class("tap-prev", Off, !paged);
        Class("tap-next", Off, !paged);
        Class("tap-page", Off, !paged);
        Text("tap-page", "t", $"{page + 1} / {pages}");
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

    private string L(string key) => _plugin.LocalizeStringForPlayer(Admin, key);

    private string L(string key, params object[] args) => _plugin.LocalizeStringForPlayer(Admin, key, args);
}
