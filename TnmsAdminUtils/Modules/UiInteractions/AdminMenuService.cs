using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsAdminUtils.Modules.UiInteractions.Panel;
using Wuling.Abstract.Tianshi.Authority;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// An open admin menu or admin panel. An admin has at most one at a time.
/// </summary>
public interface IAdminSession
{
    IGameClient Admin { get; }

    void Close();

    /// <summary>True while a chat message would be taken as typed text (not timed out).</summary>
    bool IsWaitingText { get; }

    bool TryAcceptText(string message);
}

/// <summary>
/// Owns the admin menu registry and the open menus / panels, and feeds chat messages to the one waiting for text.
/// </summary>
public sealed class AdminMenuService : IClientListener
{
    public int ListenerVersion => 1;
    public int ListenerPriority => 0;

    public AdminMenuRegistry Registry { get; } = new();

    /// <summary>
    /// configs/menus/*.toml, read once after every plugin has loaded and again on <see cref="Reload"/>.
    /// </summary>
    public AdminMenuConfig Config { get; private set; } = new();

    public AdminCommandCache Commands { get; }
    public AdminPanelService Panel { get; }

    private readonly TnmsAdminUtils _plugin;
    private readonly Dictionary<ulong, IAdminSession> _sessions = new();
    // The last chat message taken as text: who, what and on which tick (the other say listeners run in the same tick).
    private (ulong SteamId, string Message, int Tick)? _lastTaken;
    private IDisposable? _authoritySubscription;

    public AdminMenuService(TnmsAdminUtils plugin)
    {
        _plugin = plugin;
        Commands = new AdminCommandCache(Registry);
        Panel = new AdminPanelService(plugin);
    }

    public void Load()
    {
        _plugin.SharedSystem.GetClientManager().InstallClientListener(this);
    }

    /// <summary>
    /// Wuling is resolved after every module has loaded, so the panel layout and the permission events hook in here.
    /// </summary>
    public void OnAllPluginsLoaded()
    {
        // After the commands registered themselves, so operations named like one of them can be refused.
        LoadConfig();
        Panel.Load(this);

        // Raised (on the main thread) when a joining player's permissions finish loading and whenever they change.
        _authoritySubscription = TnmsPlugin.Wuling.EventBus.Subscribe<OnAuthorityChanged>(e =>
        {
            if (e.PlayerId is { } playerId)
                Commands.Build(playerId);
            else
                Commands.Clear();
        });
    }

    /// <summary>
    /// !adminmenu_reload: reads the menu TOMLs again and rebuilds every admin's command lists.
    /// </summary>
    public void Reload() => LoadConfig();

    private void LoadConfig()
    {
        Config = AdminMenuConfig.Load(_plugin.ModuleDirectory, Registry.IsCodeEntry, _plugin.Logger);

        // Clears the command lists through Registry.Changed.
        Registry.SetConfig(Config.Categories, Config.Operations);
    }

    public void Unload()
    {
        _authoritySubscription?.Dispose();
        _authoritySubscription = null;
        _plugin.SharedSystem.GetClientManager().RemoveClientListener(this);

        foreach (var session in _sessions.Values.ToList())
            session.Close();

        _sessions.Clear();
        Panel.Unload();
    }

    public void Open(IGameClient admin)
    {
        if (TnmsPlugin.Wuling.Registry.GetPlayer(admin) is not { } player)
            return;

        Replace(admin, new AdminMenuSession(_plugin, this, admin, player)).Start();
    }

    public void OpenPanel(IGameClient admin)
    {
        if (TnmsPlugin.Wuling.Registry.GetPlayer(admin) is not { } player || Panel.Surface is null)
            return;

        Replace(admin, new AdminPanelSession(_plugin, this, admin, player)).Start();
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
    /// Whether this chat message is (or just was) the admin's typed text for their menu / panel, so other chat handlers
    /// (admin chat's "@...") leave it alone. True while waiting for text, and for the message taken as text in this
    /// tick: the say listeners run in no fixed order, and taking the text ends the wait.
    /// </summary>
    public bool IsTextInput(IGameClient client, string message)
    {
        if (_sessions.TryGetValue(client.SteamId, out var session) && session.IsWaitingText)
            return true;

        return _lastTaken is { } taken
            && taken.SteamId == (ulong)client.SteamId
            && taken.Message == message
            && taken.Tick == _plugin.SharedSystem.GetModSharp().GetGlobals().TickCount;
    }

    public ECommandAction OnClientSayCommand(IGameClient client, bool teamOnly, bool isCommand, string commandName, string message)
    {
        if (isCommand || !_sessions.TryGetValue(client.SteamId, out var session) || !session.TryAcceptText(message))
            return ECommandAction.Skipped;

        _lastTaken = ((ulong)client.SteamId, message, _plugin.SharedSystem.GetModSharp().GetGlobals().TickCount);
        return ECommandAction.Stopped;
    }

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (_sessions.TryGetValue(client.SteamId, out var session))
            session.Close();

        Commands.Remove(client.SteamId);
    }
}
