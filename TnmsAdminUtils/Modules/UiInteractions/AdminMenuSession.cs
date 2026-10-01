using Sharp.Shared.Objects;
using TnmsAdminUtils.Modules.UiInteractions.Panel;
using TnmsPluginFoundation;
using Wuling.Abstract.Tianshi.Menu;
using Wuling.Abstract.Tianshi.Registry;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// The admin menu of one player. Uses a single Wuling menu instance and swaps its items on every step,
/// so it takes one slot of the player's menu stack however deep the flow goes.
/// </summary>
public sealed class AdminMenuSession : IAdminFlowView, IAdminSession
{
    private const MenuItemStyleFlags Selectable = MenuItemStyleFlags.Active | MenuItemStyleFlags.HasNumber;

    private readonly TnmsAdminUtils _plugin;
    private readonly AdminMenuService _service;
    private readonly IPlayerEntry _player;
    private readonly AdminFlow _flow;
    private IMenuInstance? _menu;
    private bool _ended;

    public IGameClient Admin { get; }

    public AdminMenuSession(TnmsAdminUtils plugin, AdminMenuService service, IGameClient admin, IPlayerEntry player)
    {
        _plugin = plugin;
        _service = service;
        _player = player;
        Admin = admin;
        _flow = new AdminFlow(plugin, service, admin, player, this);
    }

    public void Start()
    {
        List<AdminFlowItem> extra = [];

        var favorites = AdminFavorites.Load(Admin);

        if (favorites.Ids.Count > 0)
            extra.Add(new AdminFlowItem(_plugin.LocalizeStringForPlayer(Admin, "AdminMenu.Favorites"), () => Defer(() => _flow.StartFavorites(favorites))));

        if (TnmsPlugin.AdminManager.PlayerHasPermission(Admin.SteamId, AdminPanelService.Permission))
            extra.Add(new AdminFlowItem(_plugin.LocalizeStringForPlayer(Admin, "AdminMenu.OpenPanel"), () => Defer(() => _service.OpenPanel(Admin))));

        _flow.StartRoot(extra);
    }

    public void Close()
    {
        _flow.Abandon();
        Finish();
    }

    public bool TryAcceptText(string message) => _flow.TryAcceptText(message);

    bool IAdminFlowView.KeepHistoryAfterText => false;

    void IAdminFlowView.Show(string title, IReadOnlyList<AdminFlowItem> items, Action? back, string? usage, string? emptyText)
    {
        var menu = EnsureMenu();
        menu.ClearItems();
        menu.Title = title;

        // The chat menu has no text area, so the message goes in as a disabled item.
        if (items.Count == 0 && emptyText != null)
            menu.AddItem(MenuItemStyleFlags.Disabled, emptyText);

        // The menu font is proportional, so the command column is padded by eye: a space is about half a letter wide.
        var commandWidth = items.Max(i => i.Command?.Length) ?? 0;

        string Text(AdminFlowItem item)
        {
            if (item.Command is not { } command)
                return item.Label;

            return item.Label.Length == 0 ? command : $"{command}{new string(' ', (commandWidth - command.Length) * 2)} | {item.Label}";
        }

        foreach (var item in items)
        {
            if (item.OnSelect is { } onSelect)
                menu.AddItem(Selectable, Text(item), (_, _, _, _) => onSelect());
            else
                menu.AddItem(MenuItemStyleFlags.Disabled, Text(item));
        }

        if (back != null)
            menu.AddItem(Selectable, _plugin.LocalizeStringForPlayer(Admin, "AdminMenu.Back"), (_, _, _, _) => back());

        if (!menu.DisplayToPlayer(_player))
            Close();
    }

    void IAdminFlowView.ShowConfirm(AdminFlowConfirm confirm, Action? back)
        => ((IAdminFlowView)this).Show(confirm.Title, [new AdminFlowItem(confirm.ExecuteLabel, confirm.Execute, AdminFlowItemStyle.Primary)], back, null, null);

    /// <summary>
    /// Closes the menu while the admin types in chat; the next step opens a new menu.
    /// </summary>
    void IAdminFlowView.WaitText(string title, Action back, string? usage) => _menu?.Close();

    public void Finish()
    {
        if (_ended)
            return;

        _ended = true;

        if (_menu is { IsClosed: false })
            _menu.Close();

        _service.OnSessionClosed(this);
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
            // Closed by the player (exit key) or on disconnect; keep going while waiting for text.
            if (_menu == menu && !_flow.IsWaitingText)
                Close();
        };

        _menu = menu;
        return menu;
    }

    private void Defer(Action action) => _plugin.SharedSystem.GetModSharp().InvokeFrameAction(action);
}
