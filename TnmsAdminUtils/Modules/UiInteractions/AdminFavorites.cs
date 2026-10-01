using System.Text.Json;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// An admin's favorite menu entries (<see cref="AdminMenuEntry.MenuId"/>), kept in a Wuling cookie as a JSON array.
/// Ids that are not registered right now are kept, so favorites survive a command being absent for a while.
/// </summary>
public sealed class AdminFavorites
{
    private const string CookieKey = "tnms.adminutil.favorites.v1";

    private readonly ulong _steamId;
    private readonly List<string> _ids;

    public IReadOnlyList<string> Ids => _ids;

    private AdminFavorites(ulong steamId, List<string> ids)
    {
        _steamId = steamId;
        _ids = ids;
    }

    public static AdminFavorites Load(IGameClient admin)
    {
        ulong steamId = admin.SteamId;
        List<string> ids = [];

        try
        {
            if (TnmsPlugin.Wuling.Cookie.GetCookie<string>(steamId, CookieKey) is { Length: > 0 } json)
                ids = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            // A broken value starts over instead of failing the menu.
        }

        return new AdminFavorites(steamId, ids);
    }

    public bool Contains(string menuId) => _ids.Contains(menuId);

    public void Toggle(string menuId)
    {
        if (!_ids.Remove(menuId))
            _ids.Add(menuId);

        TnmsPlugin.Wuling.Cookie.SetCookie(_steamId, CookieKey, JsonSerializer.Serialize(_ids));
    }
}
