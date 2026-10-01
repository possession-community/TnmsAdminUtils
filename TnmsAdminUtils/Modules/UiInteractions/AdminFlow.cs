using System.Collections.Immutable;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using Wuling.Abstract.Tianshi.Registry;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// A command list: the Players tree (every command with a target, after picking the player) or a category.
/// </summary>
/// <param name="Category">Category key, or null for the Players tree</param>
public readonly record struct AdminMenuList(string? Category)
{
    public static AdminMenuList Players => new(null);

    public bool IsPlayers => Category is null;
}

/// <param name="Raw">Text written to the command line</param>
/// <param name="Display">Text shown in the menu</param>
/// <param name="IsSelector">True for @all / @ct / ...</param>
/// <param name="IsSkipped">Optional argument left at its default</param>
public sealed record AdminMenuValue(string Raw, string Display, bool IsSelector = false, bool IsSkipped = false)
{
    public static string Quote(string text) => $"\"{text.Replace('"', '\'')}\"";
}

/// <summary>
/// What has been chosen so far. Immutable so that going back restores the previous choices.
/// </summary>
public sealed record AdminMenuFlow(
    AdminMenuList List,
    AdminMenuEntry? Entry,
    AdminMenuValue? ChosenTarget,
    ImmutableDictionary<int, AdminMenuValue> Values)
{
    public static AdminMenuFlow Start(AdminMenuList list) => new(list, null, null, ImmutableDictionary<int, AdminMenuValue>.Empty);
}

public enum AdminFlowItemStyle
{
    Normal,
    Primary,
}

/// <param name="OnSelect">Null for an item that cannot be selected</param>
/// <param name="FavoriteId">Set on command items, which can be added to favorites</param>
/// <param name="Command">Chat command of a command item ("!slay"), shown before <see cref="Label"/>
/// (empty when the command has no translated label)</param>
public sealed record AdminFlowItem(
    string Label,
    Action? OnSelect,
    AdminFlowItemStyle Style = AdminFlowItemStyle.Normal,
    string? FavoriteId = null,
    string? Command = null);

/// <param name="Title">The command and its chosen values, shown above the execute item</param>
public sealed record AdminFlowConfirm(string Title, string ExecuteLabel, Action Execute);

/// <summary>
/// Where an <see cref="AdminFlow"/> is drawn (the chat menu).
/// </summary>
public interface IAdminFlowView
{
    /// <param name="back">Null on the first page of the flow</param>
    /// <param name="usage">Usage line of the command whose arguments are being chosen</param>
    /// <param name="emptyText">Message for an empty page, shown as plain text rather than an item where the view can</param>
    void Show(string title, IReadOnlyList<AdminFlowItem> items, Action? back, string? usage, string? emptyText);

    void ShowConfirm(AdminFlowConfirm confirm, Action? back);

    /// <summary>
    /// The flow waits for the admin's next chat message. The pages before it are forgotten (the menu reopens as a
    /// new menu after it).
    /// </summary>
    void WaitText(string title, Action back, string? usage);

    /// <summary>
    /// The flow ended: executed, cancelled, or backed out of its first page.
    /// </summary>
    void Finish();
}

/// <summary>
/// Steps through choosing a command, its arguments and the confirm page, then runs the command as the admin.
/// Used by the chat menu; the panel fills a form instead (both through <see cref="AdminCommandContext"/>).
/// </summary>
public sealed class AdminFlow
{
    private sealed record PendingText(AdminMenuFlow Flow, int Index, long ExpiresAt);

    private readonly TnmsAdminUtils _plugin;
    private readonly IGameClient _admin;
    private readonly IPlayerEntry _player;
    private readonly IAdminFlowView _view;
    private readonly AdminCommandContext _context;
    private readonly Stack<Action> _history = new();
    private Action? _current;
    private PendingText? _pendingText;
    private bool _ended;

    public AdminFlow(TnmsAdminUtils plugin, AdminMenuService service, IGameClient admin, IPlayerEntry player, IAdminFlowView view)
    {
        _plugin = plugin;
        _admin = admin;
        _player = player;
        _view = view;
        _context = new AdminCommandContext(plugin, service, admin);
    }

    public bool IsWaitingText => _pendingText != null;

    /// <summary>
    /// Root page of the menu: <paramref name="topItems"/>, then Players and the categories.
    /// </summary>
    public void StartRoot(IReadOnlyList<AdminFlowItem> topItems) => Navigate(() => RenderRoot(topItems));

    public void StartList(AdminMenuList list)
    {
        if (list.IsPlayers)
            Navigate(() => RenderPlayersTarget(AdminMenuFlow.Start(list)));
        else
            ShowCommandList(AdminMenuFlow.Start(list));
    }

    /// <summary>
    /// Favorite commands in the order they were added.
    /// </summary>
    public void StartFavorites(AdminFavorites favorites) => Navigate(() => RenderFavorites(favorites));

    /// <summary>
    /// Ends the flow without notifying the view (the view itself went away).
    /// </summary>
    public void Abandon()
    {
        _pendingText = null;
        _ended = true;
    }

    /// <summary>
    /// Uses a chat message as the pending text argument. Returns true when the message was consumed.
    /// </summary>
    public bool TryAcceptText(string message)
    {
        if (_pendingText is not { } pending)
            return false;

        if (Environment.TickCount64 > pending.ExpiresAt)
        {
            End();
            return false;
        }

        var text = message.Trim();

        if (text.Length == 0)
            return false;

        // Another plugin's menu may be waiting for its keys.
        if (text.Length == 1 && char.IsDigit(text[0]) && TnmsPlugin.Wuling.Menu.GetActiveMenu(_player) != null)
            return false;

        _pendingText = null;

        if (text.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            _context.PrintToChat("AdminMenu.Text.Cancelled");
            End();
            return true;
        }

        if (!_context.TryParseText(pending.Flow.Entry!.Arguments[pending.Index], text, out var value))
        {
            _pendingText = pending with { ExpiresAt = TextInputExpiry() };
            return true;
        }

        var flow = pending.Flow with { Values = pending.Flow.Values.SetItem(pending.Index, value) };

        Defer(() =>
        {
            _history.Clear();
            _current = null;
            Advance(flow);
        });

        return true;
    }

    private void End()
    {
        if (_ended)
            return;

        _pendingText = null;
        _ended = true;
        _view.Finish();
    }

    private void RenderRoot(IReadOnlyList<AdminFlowItem> topItems)
    {
        var items = new List<AdminFlowItem>(topItems);

        if (_context.VisibleEntries(AdminMenuList.Players).Any())
            items.Add(new AdminFlowItem(L("AdminMenu.Root.Players"), Deferred(() => StartList(AdminMenuList.Players))));

        // The command lists stay together after Players.
        foreach (var category in _context.VisibleCategories())
            items.Add(new AdminFlowItem(_context.CategoryName(category), Deferred(() => StartList(new AdminMenuList(category.Key)))));

        Show(L("AdminMenu.Title"), items);
    }

    private void ShowCommandList(AdminMenuFlow flow) => Navigate(() => RenderCommandList(flow));

    private void RenderCommandList(AdminMenuFlow flow)
    {
        var title = flow.ChosenTarget is { } target
            ? $"{L("AdminMenu.Root.Players")}: {target.Display}"
            : _context.VisibleCategories().FirstOrDefault(c => c.Key == flow.List.Category) is { } category
                ? _context.CategoryName(category)
                : flow.List.Category ?? string.Empty;

        var items = new List<AdminFlowItem>();

        foreach (var entry in _context.VisibleEntries(flow.List, flow.ChosenTarget))
        {
            var values = flow.ChosenTarget is { } chosen
                ? flow.Values.SetItem(entry.PrimaryTargetIndex, chosen)
                : flow.Values;

            var next = flow with { Entry = entry, Values = values };
            items.Add(CommandItem(entry, Deferred(() => Advance(next))));
        }

        Show(title, items);
    }

    private void RenderFavorites(AdminFavorites favorites)
    {
        var items = _context.FavoriteEntries(favorites)
            .Select(entry =>
            {
                var flow = AdminMenuFlow.Start(AdminCommandContext.ListOf(entry)) with { Entry = entry };
                return CommandItem(entry, Deferred(() => Advance(flow)));
            })
            .ToList();

        Show(L("AdminMenu.Favorites"), items, emptyText: L("AdminMenu.Favorites.Empty"));
    }

    private void RenderPlayersTarget(AdminMenuFlow flow)
        => Show(L("AdminMenu.Step.Target"), TargetItems(allowSelectors: true, value => ShowCommandList(flow with { ChosenTarget = value })));

    private void Advance(AdminMenuFlow flow)
    {
        foreach (var index in AskOrder(flow))
        {
            if (flow.Values.ContainsKey(index))
                continue;

            Navigate(() => RenderArgument(flow, index));
            return;
        }

        Navigate(() => RenderConfirm(flow));
    }

    /// <summary>
    /// General and the TOML categories ask the options first and the primary target last; Players (target already
    /// chosen), Server and Notification follow the command line order.
    /// </summary>
    private static IEnumerable<int> AskOrder(AdminMenuFlow flow)
    {
        var entry = flow.Entry!;
        var indices = Enumerable.Range(0, entry.Arguments.Count);
        var commandLineOrder = flow.List.Category is null or AdminMenuCategory.Server or AdminMenuCategory.Notification;

        if (commandLineOrder || entry.PrimaryTargetIndex < 0)
            return indices;

        return indices.Where(i => i != entry.PrimaryTargetIndex).Append(entry.PrimaryTargetIndex);
    }

    private void RenderArgument(AdminMenuFlow flow, int index)
    {
        var entry = flow.Entry!;
        var argument = entry.Arguments[index];
        var title = $"{_context.CommandLabel(entry)}: {_context.Title(argument)}";
        var items = new List<AdminFlowItem>();

        AdminMenuFlow With(AdminMenuValue value) => flow with { Values = flow.Values.SetItem(index, value) };

        if (argument.IsOptional && argument is not TargetArgument)
        {
            var skipped = _context.Skipped(argument);
            items.Add(new AdminFlowItem(L("AdminMenu.Skip"), Deferred(() => Advance(With(skipped)))));
        }

        if (argument is TargetArgument target)
        {
            items.AddRange(TargetItems(target.AllowSelectors, value => Advance(With(value))));
        }
        else if (argument.HasChoices)
        {
            foreach (var choice in _context.ValueChoices(argument))
                items.Add(new AdminFlowItem(choice.Label, Deferred(() => Advance(With(choice.Value)))));

            if (argument.AcceptsText)
                items.Add(new AdminFlowItem(L("AdminMenu.TypeInChat"), Deferred(() => BeginTextInput(flow, index, title))));
        }
        else if (argument.IsOptional)
        {
            items.Add(new AdminFlowItem(L("AdminMenu.TypeInChat"), Deferred(() => BeginTextInput(flow, index, title))));
        }
        else
        {
            // Typed only: go straight to the chat prompt.
            BeginTextInput(flow, index, title);
            return;
        }

        Show(title, items, _context.Usage(entry));
    }

    private void BeginTextInput(AdminMenuFlow flow, int index, string title)
    {
        _pendingText = new PendingText(flow, index, TextInputExpiry());
        _view.WaitText(title, Deferred(Back), _context.Usage(flow.Entry!));

        _context.PrintToChat("AdminMenu.Text.Prompt", title, AdminCommandContext.TextInputTimeoutSeconds);
    }

    private void RenderConfirm(AdminMenuFlow flow)
    {
        var entry = flow.Entry!;
        var values = Enumerable.Range(0, entry.Arguments.Count).Select(i => flow.Values[i]).ToList();
        var label = _context.CommandLabel(entry);

        var summary = string.Join(" / ", values.Select(v => v.Display));
        var title = L("AdminMenu.Confirm.Title", summary.Length > 0 ? $"{label} - {summary}" : label);

        _pendingText = null;
        _view.ShowConfirm(
            new AdminFlowConfirm(title, L("AdminMenu.Confirm.Execute"), Deferred(() =>
            {
                End();
                _context.Execute(entry, values);
            })),
            _history.Count > 0 ? Deferred(Back) : null);
    }

    private List<AdminFlowItem> TargetItems(bool allowSelectors, Action<AdminMenuValue> onChosen)
    {
        var choices = _context.TargetChoices(allowSelectors);
        var items = choices.Select(c => new AdminFlowItem(c.Label, Deferred(() => onChosen(c.Value)))).ToList();

        if (!choices.Any(c => !c.Value.IsSelector))
            items.Add(new AdminFlowItem(L("AdminMenu.NoPlayers"), null));

        return items;
    }

    private void Show(string title, IReadOnlyList<AdminFlowItem> items, string? usage = null, string? emptyText = null)
    {
        _pendingText = null;
        _view.Show(title, items, _history.Count > 0 ? Deferred(Back) : null, usage, emptyText);
    }

    private void Navigate(Action render)
    {
        if (_current != null)
            _history.Push(_current);

        _current = render;
        render();
    }

    private void Back()
    {
        _pendingText = null;

        if (!_history.TryPop(out var previous))
        {
            End();
            return;
        }

        _current = previous;
        previous();
    }

    /// <summary>
    /// Item handlers run inside the view's input handling; continue on the next frame instead.
    /// </summary>
    private Action Deferred(Action action) => () => Defer(action);

    private void Defer(Action action)
    {
        _plugin.SharedSystem.GetModSharp().InvokeFrameAction(() =>
        {
            if (!_ended && _admin.IsValid)
                action();
        });
    }

    private static long TextInputExpiry() => Environment.TickCount64 + AdminCommandContext.TextInputTimeoutSeconds * 1000L;

    /// <summary>
    /// Command lists show the chat command too, for admins who know it by name: "!slay | Slay".
    /// </summary>
    private AdminFlowItem CommandItem(AdminMenuEntry entry, Action onSelect)
        => new(_context.TranslatedLabel(entry) ?? string.Empty, onSelect, FavoriteId: entry.MenuId, Command: $"!{entry.CommandName}");

    private string L(string key) => _context.L(key);

    private string L(string key, params object[] args) => _context.L(key, args);
}
