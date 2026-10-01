using System.Collections.Immutable;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsPluginFoundation.Extensions.Client;
using Wuling.Abstract.Tianshi.Menu;
using Wuling.Abstract.Tianshi.Registry;

namespace TnmsAdminUtils.Modules.UiInteractions;

public enum AdminMenuTree
{
    Commands,
    Players,
    Server,
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

/// <summary>
/// The admin menu of one player. Uses a single Wuling menu instance and swaps its items on every step,
/// so it takes one slot of the player's menu stack however deep the flow goes.
/// </summary>
public sealed class AdminMenuSession
{
    private const MenuItemStyleFlags Selectable = MenuItemStyleFlags.Active | MenuItemStyleFlags.HasNumber;

    private static readonly (string Target, string LabelKey)[] Selectors =
    [
        ("@all", "AdminMenu.Selector.All"),
        ("@ct", "AdminMenu.Selector.Ct"),
        ("@t", "AdminMenu.Selector.T"),
        ("@spec", "AdminMenu.Selector.Spec"),
    ];

    private readonly TnmsAdminUtils _plugin;
    private readonly AdminMenuService _service;
    private readonly IGameClient _admin;
    private const int TextInputTimeoutSeconds = 60;

    private sealed record PendingText(AdminMenuFlow Flow, int Index, long ExpiresAt);

    private readonly IPlayerEntry _player;
    private readonly Stack<Action> _history = new();
    private IMenuInstance? _menu;
    private Action? _current;
    private PendingText? _pendingText;
    private bool _ended;

    public IGameClient Admin => _admin;

    public AdminMenuSession(TnmsAdminUtils plugin, AdminMenuService service, IGameClient admin, IPlayerEntry player)
    {
        _plugin = plugin;
        _service = service;
        _admin = admin;
        _player = player;
    }

    public void Start() => Navigate(RenderRoot);

    public void Close()
    {
        _pendingText = null;

        if (_menu is { IsClosed: false })
            _menu.Close();
        else
            End();
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
            Close();
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

        // The rest of the flow opens as a new menu; there is nothing to go back to.
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

        _ended = true;
        _service.OnSessionClosed(this);
    }

    private void RenderRoot()
    {
        Render(L("AdminMenu.Title"), menu =>
        {
            if (VisibleEntries(AdminMenuFlow.Start(AdminMenuTree.Commands)).Any())
            {
                menu.AddItem(Selectable, L("AdminMenu.Root.Commands"), OnSelect(() => ShowCommandList(AdminMenuFlow.Start(AdminMenuTree.Commands))));
                menu.AddItem(Selectable, L("AdminMenu.Root.Players"), OnSelect(() => Navigate(() => RenderPlayersTarget(AdminMenuFlow.Start(AdminMenuTree.Players)))));
            }

            if (VisibleEntries(AdminMenuFlow.Start(AdminMenuTree.Server)).Any())
                menu.AddItem(Selectable, L("AdminMenu.Root.Server"), OnSelect(() => ShowCommandList(AdminMenuFlow.Start(AdminMenuTree.Server))));
        }, withBack: false);
    }

    private void ShowCommandList(AdminMenuFlow flow) => Navigate(() => RenderCommandList(flow));

    private void RenderCommandList(AdminMenuFlow flow)
    {
        var title = flow.ChosenTarget is { } target
            ? $"{L("AdminMenu.Root.Players")}: {target.Display}"
            : L(flow.Tree == AdminMenuTree.Server ? "AdminMenu.Root.Server" : "AdminMenu.Root.Commands");

        Render(title, menu =>
        {
            foreach (var entry in VisibleEntries(flow))
            {
                var values = flow.ChosenTarget is { } chosen
                    ? flow.Values.SetItem(entry.PrimaryTargetIndex, chosen)
                    : flow.Values;

                var next = flow with { Entry = entry, Values = values };
                menu.AddItem(Selectable, CommandLabel(entry), OnSelect(() => Advance(next)));
            }
        });
    }

    private void RenderPlayersTarget(AdminMenuFlow flow)
    {
        Render(L("AdminMenu.Step.Target"), menu =>
        {
            AddTargetItems(menu, allowSelectors: true, value => ShowCommandList(flow with { ChosenTarget = value }));
        });
    }

    private void Advance(AdminMenuFlow flow)
    {
        var entry = flow.Entry!;

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

        AdminMenuFlow With(AdminMenuValue value) => flow with { Values = flow.Values.SetItem(index, value) };

        void AddSkipItem(IMenuInstance menu)
        {
            if (!argument.IsOptional)
                return;

            var skipped = new AdminMenuValue(argument.DefaultRaw ?? string.Empty, L("AdminMenu.Default"), IsSkipped: true);
            menu.AddItem(Selectable, L("AdminMenu.Skip"), OnSelect(() => Advance(With(skipped))));
        }

        switch (argument)
        {
            case TargetArgument target:
                Render(title, menu => AddTargetItems(menu, target.AllowSelectors, value => Advance(With(value))));
                break;

            case PresetArgument preset:
                Render(title, menu =>
                {
                    AddSkipItem(menu);

                    foreach (var value in _service.Config.GetPreset(preset.PresetKey))
                        menu.AddItem(Selectable, value, OnSelect(() => Advance(With(new AdminMenuValue(value, value)))));

                    menu.AddItem(Selectable, L("AdminMenu.TypeInChat"), OnSelect(() => BeginTextInput(flow, index, title)));
                });
                break;

            case ChoiceArgument choice:
                Render(title, menu =>
                {
                    AddSkipItem(menu);

                    foreach (var option in choice.Choices)
                    {
                        var label = option.LabelKey is null ? option.Value : L(option.LabelKey);
                        menu.AddItem(Selectable, label, OnSelect(() => Advance(With(new AdminMenuValue(option.Value, label)))));
                    }
                });
                break;

            case TextArgument when argument.IsOptional:
                Render(title, menu =>
                {
                    AddSkipItem(menu);
                    menu.AddItem(Selectable, L("AdminMenu.TypeInChat"), OnSelect(() => BeginTextInput(flow, index, title)));
                });
                break;

            case TextArgument:
            case TextListArgument:
                BeginTextInput(flow, index, title);
                break;
        }
    }

    /// <summary>
    /// Closes the menu and waits for the admin's next chat message.
    /// </summary>
    private void BeginTextInput(AdminMenuFlow flow, int index, string title)
    {
        // Set before closing so that OnClosed keeps the session alive.
        _pendingText = new PendingText(flow, index, Environment.TickCount64 + TextInputTimeoutSeconds * 1000L);
        _menu?.Close();

        PrintToChat("AdminMenu.Text.Prompt", title, TextInputTimeoutSeconds);
    }

    private void RenderConfirm(AdminMenuFlow flow)
    {
        var entry = flow.Entry!;
        var summary = string.Join(" / ", Enumerable.Range(0, entry.Arguments.Count).Select(i => flow.Values[i].Display));
        var command = summary.Length > 0 ? $"{CommandLabel(entry)} - {summary}" : CommandLabel(entry);

        Render(L("AdminMenu.Confirm.Title", command), menu =>
        {
            menu.AddItem(Selectable, L("AdminMenu.Confirm.Execute"), OnSelect(() => Execute(flow)));
        });
    }

    private void Execute(AdminMenuFlow flow)
    {
        var entry = flow.Entry!;
        var values = Enumerable.Range(0, entry.Arguments.Count).Select(i => flow.Values[i]).ToList();

        // Skipped arguments at the end are left out so the command uses its own defaults.
        var count = values.FindLastIndex(v => !v.IsSkipped) + 1;
        var commandLine = string.Join(' ', values.Take(count).Select(v => v.Raw).Prepend("ms_" + entry.CommandName));

        Close();

        if (_admin.IsValid)
            _admin.ExecuteStringCommand(commandLine);
    }

    private void AddTargetItems(IMenuInstance menu, bool allowSelectors, Action<AdminMenuValue> onChosen)
    {
        if (allowSelectors)
        {
            foreach (var (target, labelKey) in Selectors)
            {
                var value = new AdminMenuValue(target, L(labelKey), IsSelector: true);
                menu.AddItem(Selectable, value.Display, OnSelect(() => onChosen(value)));
            }
        }

        var any = false;
        var authority = TnmsPlugin.AdminManager;

        foreach (var client in _plugin.SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (client.IsHltv)
                continue;

            if (!client.IsFakeClient && !authority.PlayerCanTarget(_admin.SteamId, client.SteamId))
                continue;

            // Humans by SteamID64, bots by their literal name.
            var raw = client.IsFakeClient ? $"\"#{client.Name}\"" : ((ulong)client.SteamId).ToString();
            var value = new AdminMenuValue(raw, client.Name);
            menu.AddItem(Selectable, client.Name, OnSelect(() => onChosen(value)));
            any = true;
        }

        if (!any)
            menu.AddItem(MenuItemStyleFlags.Disabled, L("AdminMenu.NoPlayers"));
    }

    private IEnumerable<AdminMenuEntry> VisibleEntries(AdminMenuFlow flow)
    {
        var authority = TnmsPlugin.AdminManager;

        foreach (var entry in _service.Registry.Entries)
        {
            if (!authority.PlayerHasPermission(_admin.SteamId, entry.Permission))
                continue;

            var primary = entry.PrimaryTarget;

            var visible = flow.Tree switch
            {
                AdminMenuTree.Server => primary is null,
                AdminMenuTree.Players => primary is not null && (flow.ChosenTarget is not { IsSelector: true } || primary.AllowSelectors),
                _ => primary is not null,
            };

            if (visible)
                yield return entry;
        }
    }

    private void Render(string title, Action<IMenuInstance> fill, bool withBack = true)
    {
        _pendingText = null;

        var menu = EnsureMenu();
        menu.ClearItems();
        menu.Title = title;
        fill(menu);

        if (withBack)
            menu.AddItem(Selectable, L("AdminMenu.Back"), OnSelect(Back));

        if (!menu.DisplayToPlayer(_player))
            Close();
    }

    /// <summary>
    /// A closed Wuling menu cannot be shown again, so a new instance is created after text input.
    /// </summary>
    private IMenuInstance EnsureMenu()
    {
        if (_menu is { IsClosed: false })
            return _menu;

        var menu = TnmsPlugin.Wuling.Menu.CreateMenu();
        menu.ItemControls = MenuItemControlFlags.Back | MenuItemControlFlags.Next | MenuItemControlFlags.Exit;
        menu.OnClosed = _ =>
        {
            if (_menu == menu && _pendingText is null)
                End();
        };

        _menu = menu;
        return menu;
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
        if (!_history.TryPop(out var previous))
        {
            Close();
            return;
        }

        _current = previous;
        previous();
    }

    /// <summary>
    /// Menu handlers run inside Wuling's key handling; rebuild the items on the next frame instead.
    /// </summary>
    private MenuItemHandler OnSelect(Action action) => (_, _, _, _) => Defer(action);

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

    private string CommandLabel(AdminMenuEntry entry)
    {
        var label = L(entry.LabelKey);
        return label == entry.LabelKey ? entry.CommandName : label;
    }

    private string L(string key) => _plugin.LocalizeStringForPlayer(_admin, key);

    private string L(string key, params object[] args) => _plugin.LocalizeStringForPlayer(_admin, key, args);
}
