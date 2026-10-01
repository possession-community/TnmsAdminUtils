using TnmsPluginFoundation;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// The commands each admin can see, per list. Built when the admin's permissions finish loading (and rebuilt when
/// they change, the registry changes or on !adminmenu_reload) instead of on every render.
/// A list that is not built yet is built on first use.
/// </summary>
public sealed class AdminCommandCache
{
    private sealed record Lists(
        IReadOnlyDictionary<AdminMenuTree, IReadOnlyList<AdminMenuEntry>> ByTree,
        IReadOnlyDictionary<string, AdminMenuEntry> ByMenuId);

    private readonly AdminMenuRegistry _registry;
    private readonly Dictionary<ulong, Lists> _players = new();

    public AdminCommandCache(AdminMenuRegistry registry)
    {
        _registry = registry;
        _registry.Changed += Clear;
    }

    /// <summary>
    /// Players lists every command with a target; the others follow <see cref="AdminMenuEntry.Category"/>.
    /// </summary>
    public IReadOnlyList<AdminMenuEntry> Get(ulong steamId, AdminMenuTree tree) => For(steamId).ByTree[tree];

    public AdminMenuEntry? Find(ulong steamId, string menuId) => For(steamId).ByMenuId.GetValueOrDefault(menuId);

    public void Build(ulong steamId) => _players[steamId] = Create(steamId);

    public void Remove(ulong steamId) => _players.Remove(steamId);

    /// <summary>
    /// Drops every list; each is rebuilt on its next use.
    /// </summary>
    public void Clear() => _players.Clear();

    private Lists For(ulong steamId)
    {
        if (!_players.TryGetValue(steamId, out var lists))
            _players[steamId] = lists = Create(steamId);

        return lists;
    }

    private Lists Create(ulong steamId)
    {
        var authority = TnmsPlugin.AdminManager;
        var permitted = _registry.Entries.Where(e => authority.PlayerHasPermission(steamId, e.Permission)).ToList();

        var byTree = new Dictionary<AdminMenuTree, IReadOnlyList<AdminMenuEntry>>
        {
            [AdminMenuTree.Players] = permitted.Where(e => e.PrimaryTarget is not null).ToList(),
            [AdminMenuTree.Commands] = permitted.Where(e => e.Category == AdminMenuCategory.Normal).ToList(),
            [AdminMenuTree.Server] = permitted.Where(e => e.Category == AdminMenuCategory.Server).ToList(),
            [AdminMenuTree.Notification] = permitted.Where(e => e.Category == AdminMenuCategory.Notification).ToList(),
        };

        var byMenuId = permitted.GroupBy(e => e.MenuId).ToDictionary(g => g.Key, g => g.First());

        return new Lists(byTree, byMenuId);
    }
}
