using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// Owns the admin menu registry and the open menus, and feeds chat messages to menus waiting for text.
/// </summary>
public sealed class AdminMenuService(TnmsAdminUtils plugin) : IClientListener
{
    public int ListenerVersion => 1;
    public int ListenerPriority => 0;

    public AdminMenuRegistry Registry { get; } = new();
    public AdminMenuConfig Config { get; private set; } = new();

    private readonly Dictionary<ulong, AdminMenuSession> _sessions = new();

    public void Load()
    {
        plugin.SharedSystem.GetClientManager().InstallClientListener(this);
    }

    public void Unload()
    {
        plugin.SharedSystem.GetClientManager().RemoveClientListener(this);

        foreach (var session in _sessions.Values.ToList())
            session.Close();

        _sessions.Clear();
    }

    public void Open(IGameClient admin)
    {
        if (TnmsPlugin.Wuling.Registry.GetPlayer(admin) is not { } player)
            return;

        if (_sessions.TryGetValue(admin.SteamId, out var existing))
            existing.Close();

        // Re-read on every open so edits to menu.json apply without a reload.
        Config = AdminMenuConfig.Load(plugin.ModuleDirectory, plugin.Logger);

        var session = new AdminMenuSession(plugin, this, admin, player);
        _sessions[admin.SteamId] = session;
        session.Start();
    }

    internal void OnSessionClosed(AdminMenuSession session)
    {
        if (_sessions.TryGetValue(session.Admin.SteamId, out var current) && current == session)
            _sessions.Remove(session.Admin.SteamId);
    }

    public ECommandAction OnClientSayCommand(IGameClient client, bool teamOnly, bool isCommand, string commandName, string message)
    {
        if (isCommand || !_sessions.TryGetValue(client.SteamId, out var session))
            return ECommandAction.Skipped;

        return session.TryAcceptText(message) ? ECommandAction.Stopped : ECommandAction.Skipped;
    }
}
