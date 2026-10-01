using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsAdminUtils.Modules.UiInteractions.Panel;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// An open admin menu or admin panel. An admin has at most one at a time.
/// </summary>
public interface IAdminSession
{
    IGameClient Admin { get; }

    void Close();

    bool TryAcceptText(string message);
}

/// <summary>
/// Owns the admin menu registry and the open menus / panels, and feeds chat messages to the one waiting for text.
/// </summary>
public sealed class AdminMenuService(TnmsAdminUtils plugin) : IClientListener
{
    public int ListenerVersion => 1;
    public int ListenerPriority => 0;

    public AdminMenuRegistry Registry { get; } = new();
    public AdminMenuConfig Config { get; private set; } = new();
    public AdminPanelService Panel { get; } = new(plugin);

    private readonly Dictionary<ulong, IAdminSession> _sessions = new();

    public void Load()
    {
        plugin.SharedSystem.GetClientManager().InstallClientListener(this);
    }

    /// <summary>
    /// Wuling is resolved after every module has loaded, so the panel layout is registered here.
    /// </summary>
    public void OnAllPluginsLoaded()
    {
        Panel.Load(this);
    }

    public void Unload()
    {
        plugin.SharedSystem.GetClientManager().RemoveClientListener(this);

        foreach (var session in _sessions.Values.ToList())
            session.Close();

        _sessions.Clear();
        Panel.Unload();
    }

    public void Open(IGameClient admin)
    {
        if (TnmsPlugin.Wuling.Registry.GetPlayer(admin) is not { } player)
            return;

        ReloadConfig();
        Replace(admin, new AdminMenuSession(plugin, this, admin, player)).Start();
    }

    public void OpenPanel(IGameClient admin)
    {
        if (TnmsPlugin.Wuling.Registry.GetPlayer(admin) is not { } player || Panel.Surface is null)
            return;

        ReloadConfig();
        Replace(admin, new AdminPanelSession(plugin, this, admin, player)).Start();
    }

    public IAdminSession? GetSession(IGameClient client) => _sessions.GetValueOrDefault(client.SteamId);

    public IEnumerable<IAdminSession> Sessions => _sessions.Values;

    internal void OnSessionClosed(IAdminSession session)
    {
        if (_sessions.TryGetValue(session.Admin.SteamId, out var current) && current == session)
            _sessions.Remove(session.Admin.SteamId);
    }

    private T Replace<T>(IGameClient admin, T session) where T : IAdminSession
    {
        if (_sessions.TryGetValue(admin.SteamId, out var existing))
            existing.Close();

        _sessions[admin.SteamId] = session;
        return session;
    }

    /// <summary>
    /// Re-read on every open so edits to menu.json apply without a reload.
    /// </summary>
    private void ReloadConfig() => Config = AdminMenuConfig.Load(plugin.ModuleDirectory, plugin.Logger);

    public ECommandAction OnClientSayCommand(IGameClient client, bool teamOnly, bool isCommand, string commandName, string message)
    {
        if (isCommand || !_sessions.TryGetValue(client.SteamId, out var session))
            return ECommandAction.Skipped;

        return session.TryAcceptText(message) ? ECommandAction.Stopped : ECommandAction.Skipped;
    }

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (_sessions.TryGetValue(client.SteamId, out var session))
            session.Close();
    }
}
