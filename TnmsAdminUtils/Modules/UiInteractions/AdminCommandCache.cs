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
        IReadOnlyList<AdminMenuEntry> Players,
        IReadOnlyDictionary<string, IReadOnlyList<AdminMenuEntry>> ByCategory,
        IReadOnlyList<AdminMenuCategoryDefinition> Categories,
        IReadOnlyDictionary<string, AdminMenuEntry> ByMenuId);

    private readonly AdminMenuRegistry _registry;
    private readonly Dictionary<ulong, Lists> _players = new();

    public AdminCommandCache(AdminMenuRegistry registry)
    {
        _registry = registry;
        _registry.Changed += Clear;
    }

    /// <summary>
    /// Players lists every command with a target; a category lists its own commands.
    /// </summary>
    public IReadOnlyList<AdminMenuEntry> Get(ulong steamId, AdminMenuList list)
    {
        var lists = For(steamId);

        if (list.Category is not { } category)
            return lists.Players;

        return lists.ByCategory.GetValueOrDefault(category) ?? [];
    }

    /// <summary>
    /// Categories with at least one command the admin can run, in registry order.
    /// </summary>
    public IReadOnlyList<AdminMenuCategoryDefinition> Categories(ulong steamId) => For(steamId).Categories;

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

        // A category's permission hides its commands everywhere, the Players tree and favorites included.
        var categories = _registry.Categories
            .Where(c => c.Permission is null || authority.PlayerHasPermission(steamId, c.Permission))
            .ToList();

        var categoryKeys = categories.Select(c => c.Key).ToHashSet();

        var permitted = _registry.Entries
            .Where(e => categoryKeys.Contains(e.Category) && authority.PlayerHasPermission(steamId, e.Permission))
            .ToList();

        var byCategory = permitted.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => (IReadOnlyList<AdminMenuEntry>)g.ToList());
        var byMenuId = permitted.GroupBy(e => e.MenuId).ToDictionary(g => g.Key, g => g.First());

        return new Lists(
            permitted.Where(e => e.PrimaryTarget is not null).ToList(),
            byCategory,
            categories.Where(c => byCategory.ContainsKey(c.Key)).ToList(),
            byMenuId);
    }
}
