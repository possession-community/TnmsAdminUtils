using System.Collections.Immutable;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsPluginFoundation.Extensions.Client;
using Wuling.Abstract.Tianshi.Registry;

namespace TnmsAdminUtils.Modules.UiInteractions;

public enum AdminMenuTree
{
    Commands,
    Players,
    Server,
    Notification,
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
    AdminMenuTree Tree,
    AdminMenuEntry? Entry,
    AdminMenuValue? ChosenTarget,
    ImmutableDictionary<int, AdminMenuValue> Values)
{
    public static AdminMenuFlow Start(AdminMenuTree tree) => new(tree, null, null, ImmutableDictionary<int, AdminMenuValue>.Empty);
}

public enum AdminFlowItemStyle
{
    Normal,
    Primary,
}

/// <param name="OnSelect">Null for an item that cannot be selected</param>
/// <param name="FavoriteId">Set on command items, which can be added to favorites</param>
/// <param name="Command">Chat command of a command item ("!slay"), drawn as an aligned column before <see cref="Label"/>
/// (empty when the command has no translated label)</param>
public sealed record AdminFlowItem(
    string Label,
    Action? OnSelect,
    AdminFlowItemStyle Style = AdminFlowItemStyle.Normal,
    string? FavoriteId = null,
    string? Command = null);

/// <param name="Title">One-line summary, used where only a title fits (the chat menu)</param>
/// <param name="Command">Command label</param>
/// <param name="Preview">The command as it would be typed in chat</param>
/// <param name="Rows">Argument names and chosen values, then the resolved targets</param>
/// <param name="Usage">The command's usage line, if it has one</param>
public sealed record AdminFlowConfirm(
    string Title,
    string Command,
    string Preview,
    IReadOnlyList<(string Key, string Value)> Rows,
    string ExecuteLabel,
    Action Execute,
    string? Usage);

/// <summary>
/// Where an <see cref="AdminFlow"/> is drawn: the chat menu or the panel.
/// </summary>
public interface IAdminFlowView
{
    /// <summary>
    /// False to forget the pages before a text input (the menu reopens as a new menu after it).
    /// </summary>
    bool KeepHistoryAfterText { get; }

    /// <param name="back">Null on the first page of the flow</param>
    /// <param name="usage">Usage line of the command whose arguments are being chosen</param>
    /// <param name="emptyText">Message for an empty page, shown as plain text rather than an item where the view can</param>
    void Show(string title, IReadOnlyList<AdminFlowItem> items, Action? back, string? usage, string? emptyText);

    void ShowConfirm(AdminFlowConfirm confirm, Action? back);

    /// <summary>
    /// The flow waits for the admin's next chat message.
    /// </summary>
    void WaitText(string title, Action back, string? usage);

    /// <summary>
    /// The flow ended: executed, cancelled, or backed out of its first page.
    /// </summary>
    void Finish();
}

/// <summary>
/// Steps through choosing a command, its arguments and the confirm page, then runs the command as the admin.
/// Shared by the admin menu and the admin panel.
/// </summary>
public sealed class AdminFlow
{
    private const int TextInputTimeoutSeconds = 60;

    private static readonly (string Target, string LabelKey)[] Selectors =
    [
        ("@all", "AdminMenu.Selector.All"),
        ("@ct", "AdminMenu.Selector.Ct"),
        ("@t", "AdminMenu.Selector.T"),
        ("@spec", "AdminMenu.Selector.Spec"),
    ];

    private sealed record PendingText(AdminMenuFlow Flow, int Index, long ExpiresAt);

    private readonly TnmsAdminUtils _plugin;
    private readonly AdminMenuService _service;
    private readonly IGameClient _admin;
    private readonly IPlayerEntry _player;
    private readonly IAdminFlowView _view;
    private readonly Stack<Action> _history = new();
    private Action? _current;
    private PendingText? _pendingText;
    private bool _ended;

    public AdminFlow(TnmsAdminUtils plugin, AdminMenuService service, IGameClient admin, IPlayerEntry player, IAdminFlowView view)
    {
        _plugin = plugin;
        _service = service;
        _admin = admin;
        _player = player;
        _view = view;
    }

    public bool IsWaitingText => _pendingText != null;

    /// <summary>
    /// Root page of the menu: Commands / Players / Server, plus <paramref name="extraItems"/>.
    /// </summary>
    public void StartRoot(IReadOnlyList<AdminFlowItem> extraItems) => Navigate(() => RenderRoot(extraItems));

    public void StartTree(AdminMenuTree tree)
    {
        if (tree == AdminMenuTree.Players)
            Navigate(() => RenderPlayersTarget(AdminMenuFlow.Start(tree)));
        else
            ShowCommandList(AdminMenuFlow.Start(tree));
    }

    /// <summary>
    /// Favorite commands in the order they were added. Read on every render so un-favoriting updates the list.
    /// </summary>
    public void StartFavorites(AdminFavorites favorites) => Navigate(() => RenderFavorites(favorites));

    /// <summary>
    /// Starts from the command list of an already chosen target (the panel's user list).
    /// </summary>
    public void StartWithTarget(AdminMenuValue target)
        => ShowCommandList(AdminMenuFlow.Start(AdminMenuTree.Players) with { ChosenTarget = target });

    /// <summary>
    /// Draws the current page again, e.g. to update player counts.
    /// </summary>
    public void Refresh()
    {
        if (!_ended && _pendingText is null)
            _current?.Invoke();
    }

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
            PrintToChat("AdminMenu.Text.Cancelled");
            End();
            return true;
        }

        text = new string(text.Where(c => c is not (';' or '\r' or '\n')).ToArray());

        var argument = pending.Flow.Entry!.Arguments[pending.Index];
        AdminMenuValue value;

        if (argument is TextListArgument list)
        {
            var items = text.Split([',', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (items.Length < list.MinCount)
            {
                PrintToChat("AdminMenu.List.NeedMore", list.MinCount);
                _pendingText = pending with { ExpiresAt = Environment.TickCount64 + TextInputTimeoutSeconds * 1000L };
                return true;
            }

            value = new AdminMenuValue(string.Join(' ', items.Select(AdminMenuValue.Quote)), string.Join(" / ", items));
        }
        else
        {
            value = argument is TextArgument { Quote: true }
                ? new AdminMenuValue(AdminMenuValue.Quote(text), text)
                : new AdminMenuValue(text, text);
        }

        var flow = pending.Flow with { Values = pending.Flow.Values.SetItem(pending.Index, value) };

        Defer(() =>
        {
            if (!_view.KeepHistoryAfterText)
            {
                _history.Clear();
                _current = null;
            }

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

    private void RenderRoot(IReadOnlyList<AdminFlowItem> extraItems)
    {
        var items = new List<AdminFlowItem>();

        if (VisibleEntries(AdminMenuFlow.Start(AdminMenuTree.Commands)).Any())
        {
            items.Add(new AdminFlowItem(L("AdminMenu.Root.Commands"), Deferred(() => StartTree(AdminMenuTree.Commands))));
            items.Add(new AdminFlowItem(L("AdminMenu.Root.Players"), Deferred(() => StartTree(AdminMenuTree.Players))));
        }

        if (VisibleEntries(AdminMenuFlow.Start(AdminMenuTree.Server)).Any())
            items.Add(new AdminFlowItem(L("AdminMenu.Root.Server"), Deferred(() => StartTree(AdminMenuTree.Server))));

        if (VisibleEntries(AdminMenuFlow.Start(AdminMenuTree.Notification)).Any())
            items.Add(new AdminFlowItem(L("AdminMenu.Root.Notification"), Deferred(() => StartTree(AdminMenuTree.Notification))));

        items.AddRange(extraItems);
        Show(L("AdminMenu.Title"), items);
    }

    private void ShowCommandList(AdminMenuFlow flow) => Navigate(() => RenderCommandList(flow));

    private void RenderCommandList(AdminMenuFlow flow)
    {
        var title = flow.ChosenTarget is { } target
            ? $"{L("AdminMenu.Root.Players")}: {target.Display}"
            : L(flow.Tree switch
            {
                AdminMenuTree.Server => "AdminMenu.Root.Server",
                AdminMenuTree.Notification => "AdminMenu.Root.Notification",
                _ => "AdminMenu.Root.Commands",
            });

        var items = new List<AdminFlowItem>();

        foreach (var entry in VisibleEntries(flow))
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
        var authority = TnmsPlugin.AdminManager;
        var entries = _service.Registry.Entries
            .Where(e => authority.PlayerHasPermission(_admin.SteamId, e.Permission))
            .GroupBy(e => e.MenuId)
            .ToDictionary(g => g.Key, g => g.First());

        var items = new List<AdminFlowItem>();

        foreach (var id in favorites.Ids)
        {
            if (!entries.TryGetValue(id, out var entry))
                continue;

            var tree = entry.Category switch
            {
                AdminMenuCategory.Server => AdminMenuTree.Server,
                AdminMenuCategory.Notification => AdminMenuTree.Notification,
                _ => AdminMenuTree.Commands,
            };

            var flow = AdminMenuFlow.Start(tree) with { Entry = entry };
            items.Add(CommandItem(entry, Deferred(() => Advance(flow))));
        }

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
    /// Commands tree asks the options first and the primary target last; other trees follow the command line order.
    /// </summary>
    private static IEnumerable<int> AskOrder(AdminMenuFlow flow)
    {
        var entry = flow.Entry!;
        var indices = Enumerable.Range(0, entry.Arguments.Count);

        if (flow.Tree != AdminMenuTree.Commands || entry.PrimaryTargetIndex < 0)
            return indices;

        return indices.Where(i => i != entry.PrimaryTargetIndex).Append(entry.PrimaryTargetIndex);
    }

    private void RenderArgument(AdminMenuFlow flow, int index)
    {
        var entry = flow.Entry!;
        var argument = entry.Arguments[index];
        var title = $"{CommandLabel(entry)}: {L(argument.TitleKey)}";
        var items = new List<AdminFlowItem>();

        AdminMenuFlow With(AdminMenuValue value) => flow with { Values = flow.Values.SetItem(index, value) };

        if (argument.IsOptional && argument is not TargetArgument)
        {
            var skipped = new AdminMenuValue(argument.DefaultRaw ?? string.Empty, L("AdminMenu.Default"), IsSkipped: true);
            items.Add(new AdminFlowItem(L("AdminMenu.Skip"), Deferred(() => Advance(With(skipped)))));
        }

        switch (argument)
        {
            case TargetArgument target:
                items.AddRange(TargetItems(target.AllowSelectors, value => Advance(With(value))));
                break;

            case PresetArgument preset:
                foreach (var value in _service.Config.GetPreset(preset.PresetKey))
                    items.Add(new AdminFlowItem(value, Deferred(() => Advance(With(new AdminMenuValue(value, value))))));

                items.Add(new AdminFlowItem(L("AdminMenu.TypeInChat"), Deferred(() => BeginTextInput(flow, index, title))));
                break;

            case ChoiceArgument choice:
                foreach (var option in choice.Choices)
                {
                    var label = option.LabelKey is null ? option.Value : L(option.LabelKey);
                    items.Add(new AdminFlowItem(label, Deferred(() => Advance(With(new AdminMenuValue(option.Value, label))))));
                }
                break;

            case TextArgument when argument.IsOptional:
                items.Add(new AdminFlowItem(L("AdminMenu.TypeInChat"), Deferred(() => BeginTextInput(flow, index, title))));
                break;

            case TextArgument:
            case TextListArgument:
                BeginTextInput(flow, index, title);
                return;
        }

        Show(title, items, Usage(entry));
    }

    private void BeginTextInput(AdminMenuFlow flow, int index, string title)
    {
        _pendingText = new PendingText(flow, index, Environment.TickCount64 + TextInputTimeoutSeconds * 1000L);
        _view.WaitText(title, Deferred(Back), Usage(flow.Entry!));

        PrintToChat("AdminMenu.Text.Prompt", title, TextInputTimeoutSeconds);
    }

    private void RenderConfirm(AdminMenuFlow flow)
    {
        var entry = flow.Entry!;
        var values = Enumerable.Range(0, entry.Arguments.Count).Select(i => flow.Values[i]).ToList();
        var label = CommandLabel(entry);

        var summary = string.Join(" / ", values.Select(v => v.Display));
        var title = L("AdminMenu.Confirm.Title", summary.Length > 0 ? $"{label} - {summary}" : label);

        // How the admin would type it: selectors as is, players by name, skipped trailing arguments left out.
        var count = values.FindLastIndex(v => !v.IsSkipped) + 1;
        var preview = string.Join(' ', values.Take(count)
            .Select((v, i) => entry.Arguments[i] is TargetArgument && !v.IsSelector ? AdminMenuValue.Quote(v.Display) : v.IsSkipped ? v.Raw : v.Display)
            .Prepend("!" + entry.CommandName));

        var rows = new List<(string, string)>();

        for (var i = 0; i < values.Count; i++)
            rows.Add((L(entry.Arguments[i].TitleKey), values[i].Display));

        if (entry.PrimaryTargetIndex >= 0)
            rows.Add((L("AdminMenu.Confirm.Targets"), DescribeTargets(values[entry.PrimaryTargetIndex])));

        _pendingText = null;
        _view.ShowConfirm(
            new AdminFlowConfirm(title, label, preview, rows, L("AdminMenu.Confirm.Execute"), Deferred(() => Execute(flow)), Usage(entry)),
            _history.Count > 0 ? Deferred(Back) : null);
    }

    /// <summary>
    /// Players the target resolves to right now, e.g. "3: alice, bob, carol".
    /// </summary>
    private string DescribeTargets(AdminMenuValue target)
    {
        const int maxNames = 8;

        var raw = target.Raw.Trim('"');
        var names = TnmsPlugin.TargetingManager.GetByTarget(_admin, raw).Select(c => c.Name).ToList();

        if (names.Count == 0)
            return L("AdminMenu.Confirm.NoTargets");

        var shown = string.Join(", ", names.Take(maxNames));

        return names.Count > maxNames
            ? $"{names.Count}: {shown} {L("AdminMenu.Confirm.More", names.Count - maxNames)}"
            : $"{names.Count}: {shown}";
    }

    private void Execute(AdminMenuFlow flow)
    {
        var entry = flow.Entry!;
        var values = Enumerable.Range(0, entry.Arguments.Count).Select(i => flow.Values[i]).ToList();

        // Skipped arguments at the end are left out so the command uses its own defaults.
        var count = values.FindLastIndex(v => !v.IsSkipped) + 1;
        var commandLine = string.Join(' ', values.Take(count).Select(v => v.Raw).Prepend("ms_" + entry.CommandName));

        End();

        if (_admin.IsValid)
            _admin.ExecuteStringCommand(commandLine);
    }

    private List<AdminFlowItem> TargetItems(bool allowSelectors, Action<AdminMenuValue> onChosen)
    {
        var items = new List<AdminFlowItem>();
        var authority = TnmsPlugin.AdminManager;

        if (allowSelectors)
        {
            foreach (var (target, labelKey) in Selectors)
            {
                var count = TnmsPlugin.TargetingManager.GetByTarget(_admin, target).Count();
                var value = new AdminMenuValue(target, L(labelKey), IsSelector: true);
                items.Add(new AdminFlowItem($"{value.Display} ({count})", Deferred(() => onChosen(value))));
            }
        }

        var any = false;

        foreach (var client in _plugin.SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (client.IsHltv)
                continue;

            if (!client.IsFakeClient && !authority.PlayerCanTarget(_admin.SteamId, client.SteamId))
                continue;

            var value = PlayerValue(client);
            items.Add(new AdminFlowItem(client.Name, Deferred(() => onChosen(value))));
            any = true;
        }

        if (!any)
            items.Add(new AdminFlowItem(L("AdminMenu.NoPlayers"), null));

        return items;
    }

    /// <summary>
    /// Humans by SteamID64, bots by their literal name.
    /// </summary>
    public static AdminMenuValue PlayerValue(IGameClient client)
        => new(client.IsFakeClient ? $"\"#{client.Name}\"" : ((ulong)client.SteamId).ToString(), client.Name);

    private IEnumerable<AdminMenuEntry> VisibleEntries(AdminMenuFlow flow)
    {
        var authority = TnmsPlugin.AdminManager;

        foreach (var entry in _service.Registry.Entries)
        {
            if (!authority.PlayerHasPermission(_admin.SteamId, entry.Permission))
                continue;

            var primary = entry.PrimaryTarget;

            // Players lists every command with a target, whatever its category.
            var visible = flow.Tree switch
            {
                AdminMenuTree.Players => primary is not null && (flow.ChosenTarget is not { IsSelector: true } || primary.AllowSelectors),
                AdminMenuTree.Server => entry.Category == AdminMenuCategory.Server,
                AdminMenuTree.Notification => entry.Category == AdminMenuCategory.Notification,
                _ => entry.Category == AdminMenuCategory.Normal,
            };

            if (visible)
                yield return entry;
        }
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

    private void PrintToChat(string key, params object[] args)
        => _admin.GetPlayerController()?.PrintToChat(_plugin.LocalizeWithPluginPrefix(_admin, key, args));

    private string? Usage(AdminMenuEntry entry) => entry.UsageKey is { } key ? L(key) : null;

    private string CommandLabel(AdminMenuEntry entry)
    {
        var label = L(entry.LabelKey);
        return label == entry.LabelKey ? entry.CommandName : label;
    }

    /// <summary>
    /// Command lists show the chat command too, for admins who know it by name: "!slay | Slay".
    /// </summary>
    private AdminFlowItem CommandItem(AdminMenuEntry entry, Action onSelect)
    {
        var label = L(entry.LabelKey);
        return new AdminFlowItem(label == entry.LabelKey ? string.Empty : label, onSelect, FavoriteId: entry.MenuId, Command: $"!{entry.CommandName}");
    }

    private string L(string key) => _plugin.LocalizeStringForPlayer(_admin, key);

    private string L(string key, params object[] args) => _plugin.LocalizeStringForPlayer(_admin, key, args);
}
