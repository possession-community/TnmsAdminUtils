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
public sealed class AdminPanelSession : IAdminSession
{
    // The fixed sidebar buttons (tap-nav{i}) in order; Category is any of the category buttons below them.
    private enum Page
    {
        Overview,
        Favorites,
        Users,
        Category,
    }

    private const int ListSlots = 18;
    // Command slots per page while the form takes the second column.
    private const int FormListSlots = 9;
    private const int FieldSlots = 6;
    private const int PickerSlots = 12;
    private const int PickerColumns = 4;
    private const int UserRows = 16;
    private const string Off = "tap-off";

    private static readonly string[] Sections = ["tap-ov", "tap-us", "tap-ls"];
    private static readonly string[] NavKeys = ["AdminPanel.Nav.Overview", "AdminPanel.Nav.Favorites", "AdminPanel.Nav.Users"];
    // Category buttons (tap-cat{i}) per sidebar page.
    private const int CategorySlots = 11;
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
    // Key of the category shown on the Category page.
    private string? _category;
    private IReadOnlyList<AdminMenuCategoryDefinition> _categories = [];
    private int _categoryPage;
    // Set on the Users page after a row click: the command list of that player.
    private AdminMenuValue? _playerTarget;
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

                if (_page == Page.Users && _playerTarget is null)
                    _userPage += step;
                else
                    _listPage += step;

                Render();
                return;

            case "tap-catprev":
            case "tap-catnext":
                _categoryPage += panelId == "tap-catnext" ? 1 : -1;
                Render();
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
            GoTo((Page)nav);
        }
        else if (TryIndex(panelId, "tap-cat", out var categorySlot))
        {
            var index = _categoryPage * CategorySlots + categorySlot;

            if (index < _categories.Count)
                GoTo(Page.Category, _categories[index].Key);
        }
        else if (TryIndex(panelId, "tap-sort", out var column))
        {
            ToggleSort(column);
        }
        else if (TryIndex(panelId, "tap-u", out var row))
        {
            if (_rowTargets[row] is { IsValid: true } target)
                ShowPlayerCommands(AdminCommandContext.PlayerValue(target));
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

    private void GoTo(Page page, string? category = null)
    {
        _page = page;
        _category = category;
        _playerTarget = null;
        _form = null;
        _listPage = 0;
        Render();
    }

    private void ShowPlayerCommands(AdminMenuValue target)
    {
        _playerTarget = target;
        _form = null;
        _listPage = 0;
        Render();
    }

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
        RenderSidebar();

        switch (_page)
        {
            case Page.Overview:
                RenderOverview();
                break;
            case Page.Users when _playerTarget is null:
                RenderUsers();
                break;
            default:
                RenderCommands();
                break;
        }
    }

    /// <summary>
    /// The fixed buttons and one page of category buttons. A category that went away (reload, permission change)
    /// sends the panel back to the overview.
    /// </summary>
    private void RenderSidebar()
    {
        _categories = _context.VisibleCategories();

        if (_page == Page.Category && _categories.All(c => c.Key != _category))
        {
            _page = Page.Overview;
            _category = null;
            _form = null;
        }

        for (var i = 0; i < NavKeys.Length; i++)
            Class($"tap-nav{i}", "tap-sel", i == (int)_page);

        var pages = Math.Max(1, (_categories.Count + CategorySlots - 1) / CategorySlots);
        _categoryPage = Math.Clamp(_categoryPage, 0, pages - 1);

        for (var slot = 0; slot < CategorySlots; slot++)
        {
            var id = $"tap-cat{slot}";
            var index = _categoryPage * CategorySlots + slot;

            if (index >= _categories.Count)
            {
                Class(id, Off, true);
                continue;
            }

            var category = _categories[index];
            Class(id, Off, false);
            Class(id, "tap-sel", _page == Page.Category && category.Key == _category);
            Text(id, "t", _context.CategoryName(category));
        }

        // Up / down stay visible and dim at the ends (both with a single page); clicks there are clamped away.
        Class("tap-catprev", "tap-dis", _categoryPage <= 0);
        Class("tap-catnext", "tap-dis", _categoryPage >= pages - 1);
    }

    private void RenderOverview()
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
    /// Command pages: the list (two columns, or one beside the form of the open command).
    /// </summary>
    private void RenderCommands()
    {
        ShowSection("tap-ls");

        _entries = _page switch
        {
            Page.Favorites => _context.FavoriteEntries(_favorites),
            Page.Users => _context.VisibleEntries(AdminMenuList.Players, _playerTarget),
            _ => _context.VisibleEntries(new AdminMenuList(_category)),
        };

        var title = _page switch
        {
            Page.Users => $"{L("AdminMenu.Root.Players")}: {_playerTarget?.Display}",
            Page.Category => _categories.FirstOrDefault(c => c.Key == _category) is { } category ? _context.CategoryName(category) : string.Empty,
            _ => L(NavKeys[(int)_page]),
        };

        var sub = _page == Page.Favorites && _entries.Count == 0 ? L("AdminMenu.Favorites.Empty") : string.Empty;
        Header(title, sub);

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

    private void Header(string title, string sub)
    {
        Text("tap-title", "t", title);
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
