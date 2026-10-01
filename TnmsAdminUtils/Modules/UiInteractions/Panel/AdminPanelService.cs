using Microsoft.Extensions.Logging;
using Sharp.Shared.Enums;
using Sharp.Shared.HookParams;
using Sharp.Shared.Types;
using TnmsPluginFoundation;
using Wuling.Abstract.Tianshi.Liuli;
using Wuling.Abstract.Tianshi.Registry;

namespace TnmsAdminUtils.Modules.UiInteractions.Panel;

/// <summary>
/// Registers the panel layout (tnms_admin_panel.lxml), routes its clicks and refreshes open panels every second.
/// </summary>
public sealed class AdminPanelService(TnmsAdminUtils plugin)
{
    public const string Permission = "tnms.adminutil.panel";
    public const string IpPermission = "tnms.adminutil.panel.ip";

    private const string SurfaceKey = "tnms.adminpanel";

    public AdminPanelColumns Columns { get; } = new();

    public ILiuliSurface? Surface { get; private set; }

    private AdminMenuService _menu = null!;
    private Guid _refreshTimer;
    private Func<IPlayerRunCommandHookParams, HookReturnValue<EmptyHookReturn>, HookReturnValue<EmptyHookReturn>>? _runCommandHook;

    public void Load(AdminMenuService menu)
    {
        _menu = menu;

        foreach (var column in AdminPanelColumns.BuiltIn())
            Columns.Register(column);

        try
        {
            // A surface left by a previous load (e.g. one that did not unload cleanly) still holds its old handlers.
            TnmsPlugin.Wuling.Liuli.TryGetSurface(SurfaceKey)?.Unregister();

            Surface = TnmsPlugin.Wuling.Liuli.RegisterLayout(new LiuliLayoutDescriptor
            {
                Key = SurfaceKey,
                LayoutPath = "panorama/layout/custom_game/tnms_admin_panel.vxml_c",
                BandPanelId = "tapx",
                BoxPanelId = "tap",
                DisplayName = "Admin Panel",
                DefaultPlacement = new LiuliPlacement(50f, 50f, 100),
            });
        }
        catch (Exception e)
        {
            plugin.Logger.LogError(e, "Failed to register the admin panel layout");
            return;
        }

        Surface.Clicked += OnClicked;
        _refreshTimer = plugin.CreateTimer(1.0, Refresh, GameTimerFlags.Repeatable);

        _runCommandHook = OnRunCommandPre;
        plugin.SharedSystem.GetHookManager().PlayerRunCommand.InstallHookPre(_runCommandHook);
    }

    public void Unload()
    {
        plugin.StopTimer(_refreshTimer);

        if (_runCommandHook is not null)
        {
            plugin.SharedSystem.GetHookManager().PlayerRunCommand.RemoveHookPre(_runCommandHook);
            _runCommandHook = null;
        }

        if (Surface is null)
            return;

        Surface.Clicked -= OnClicked;
        Surface.Unregister();
        Surface = null;
    }

    private void OnClicked(IPlayerEntry player, string panelId)
    {
        if (_menu.GetSession(player.Client) is AdminPanelSession session)
            session.OnClicked(panelId);
    }

    /// <summary>
    /// Same input as Wuling's Liuli menu: the cursor follows the inspect key (default F) being held.
    /// </summary>
    private HookReturnValue<EmptyHookReturn> OnRunCommandPre(IPlayerRunCommandHookParams param, HookReturnValue<EmptyHookReturn> currentReturn)
    {
        if (_menu.GetSession(param.Client) is AdminPanelSession session)
            session.UpdateInput((param.KeyButtons & UserCommandButtons.LookAtWeapon) != 0);

        return currentReturn;
    }

    private void Refresh()
    {
        foreach (var session in _menu.Sessions.OfType<AdminPanelSession>().ToList())
            session.Refresh();
    }
}
