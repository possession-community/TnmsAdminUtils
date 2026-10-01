using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsPluginFoundation.Extensions.Client;

namespace TnmsAdminUtils.Modules.UiInteractions.Panel;

/// <param name="Target">The player whose details are shown</param>
/// <param name="Viewer">The admin looking at them</param>
/// <param name="Localize">Translates a key into the viewer's language</param>
public sealed record AdminPanelDetailArgs(IGameClient Target, IGameClient Viewer, Func<string, string> Localize);

/// <summary>
/// Headed groups of the detail page.
/// </summary>
public enum AdminPanelDetailGroup
{
    /// <summary>Who the player is: name, SteamIDs, IP, language, connection.</summary>
    User,
    /// <summary>The player in the match: team, health, armor, stats, money.</summary>
    InGame,
    /// <summary>Wuling groups and immunity.</summary>
    Authority,
    /// <summary>Anything else, e.g. the user list columns of other plugins.</summary>
    Other,
}

/// <param name="Key">Unique key; registering the same key replaces the field</param>
/// <param name="LabelKey">Translation key of the row's label</param>
/// <param name="Group">The group the row is listed in</param>
/// <param name="Value">Row text. Called every refresh, keep it cheap</param>
public sealed record AdminPanelDetail(string Key, string LabelKey, AdminPanelDetailGroup Group, Func<AdminPanelDetailArgs, string> Value);

/// <summary>
/// Rows of the panel's player detail page, in display order within their group. Columns registered to
/// <see cref="AdminPanelColumns"/> besides the built-in ones are listed under Other, so a plugin adding a column gets
/// a row too. The layout has <see cref="Capacity"/> rows per group.
/// </summary>
public sealed class AdminPanelDetails
{
    private static readonly HashSet<string> BuiltInColumnKeys = AdminPanelColumns.BuiltIn().Select(c => c.Key).ToHashSet();

    private readonly List<AdminPanelDetail> _fields = [];

    public IReadOnlyList<AdminPanelDetail> Fields => _fields;

    public static int Capacity(AdminPanelDetailGroup group) => group switch
    {
        AdminPanelDetailGroup.User => 12,
        AdminPanelDetailGroup.InGame => 10,
        AdminPanelDetailGroup.Authority => 4,
        _ => 6,
    };

    /// <returns>False when all rows of the field's group are used</returns>
    public bool Register(AdminPanelDetail field)
    {
        var index = _fields.FindIndex(f => f.Key == field.Key);

        if (index >= 0)
        {
            _fields[index] = field;
            return true;
        }

        if (_fields.Count(f => f.Group == field.Group) >= Capacity(field.Group))
            return false;

        _fields.Add(field);
        return true;
    }

    public void Unregister(string key) => _fields.RemoveAll(f => f.Key == key);

    /// <summary>
    /// The rows of one group: the registered fields, then (Other) the extra user list columns, up to its capacity.
    /// </summary>
    public IReadOnlyList<AdminPanelDetail> Of(AdminPanelDetailGroup group, AdminPanelColumns columns)
    {
        var fields = _fields.Where(f => f.Group == group);

        if (group == AdminPanelDetailGroup.Other)
        {
            fields = fields.Concat(columns.Columns
                .Where(c => !BuiltInColumnKeys.Contains(c.Key) && _fields.All(f => f.Key != c.Key))
                .Select(c => new AdminPanelDetail(c.Key, c.HeaderKey, group, a => c.Value(a.Target, a.Viewer))));
        }

        return fields.Take(Capacity(group)).ToList();
    }

    public static IEnumerable<AdminPanelDetail> BuiltIn()
    {
        const AdminPanelDetailGroup user = AdminPanelDetailGroup.User;
        const AdminPanelDetailGroup inGame = AdminPanelDetailGroup.InGame;
        const AdminPanelDetailGroup authority = AdminPanelDetailGroup.Authority;

        var columns = AdminPanelColumns.BuiltIn().ToDictionary(c => c.Key);

        AdminPanelDetail Column(string key, string labelKey, AdminPanelDetailGroup group)
            => new(key, labelKey, group, a => columns[key].Value(a.Target, a.Viewer));

        // UserInfo
        yield return Column("name", "AdminPanel.Column.Name", user);
        yield return new AdminPanelDetail("steamid64", "AdminPanel.Detail.SteamId64", user, a => a.Target.IsFakeClient ? "BOT" : ((ulong)a.Target.SteamId).ToString());
        yield return new AdminPanelDetail("steamid3", "AdminPanel.Detail.SteamId3", user, a => a.Target.IsFakeClient ? "BOT" : $"[U:1:{a.Target.SteamId.AccountId}]");

        // STEAM_1 as CS has always shown it.
        yield return new AdminPanelDetail("steamid2", "AdminPanel.Detail.SteamId2", user, a =>
            a.Target.IsFakeClient ? "BOT" : $"STEAM_1:{a.Target.SteamId.AccountId & 1}:{a.Target.SteamId.AccountId >> 1}");

        yield return Column("ip", "AdminPanel.Column.Ip", user);

        yield return new AdminPanelDetail("language", "AdminPanel.Detail.Language", user, a =>
            a.Target.IsFakeClient ? "-" : TnmsPlugin.Wuling.Localizer.GetPlayerCulture(a.Target.SteamId).Name);

        yield return new AdminPanelDetail("slot", "AdminPanel.Detail.Slot", user, a => $"{a.Target.Slot.AsPrimitive()} / {a.Target.UserId.AsPrimitive()}");
        yield return Column("ping", "AdminPanel.Column.Ping", user);
        yield return Column("time", "AdminPanel.Column.Time", user);

        yield return new AdminPanelDetail("clantag", "AdminPanel.Detail.ClanTag", user, a =>
            a.Target.GetPlayerController()?.ClanTag is { Length: > 0 } tag ? tag : "-");

        // InGameInfo
        yield return Column("team", "AdminPanel.Column.Team", inGame);

        yield return new AdminPanelDetail("state", "AdminPanel.Detail.State", inGame, a => a.Target.GetPlayerController() switch
        {
            null => "-",
            { } controller when controller.GetPlayerPawn() is { IsAlive: true } => a.Localize("AdminPanel.Detail.Alive"),
            _ => a.Localize("AdminPanel.Detail.Dead"),
        });

        yield return Column("hp", "AdminPanel.Column.Hp", inGame);

        yield return new AdminPanelDetail("armor", "AdminPanel.Detail.Armor", inGame, a =>
        {
            if (a.Target.GetPlayerPawn() is not { IsAlive: true } pawn)
                return "-";

            return pawn.GetItemService()?.HasHelmet ?? false
                ? $"{pawn.ArmorValue} + {a.Localize("AdminPanel.Detail.Helmet")}"
                : pawn.ArmorValue.ToString();
        });

        yield return new AdminPanelDetail("kda", "AdminPanel.Detail.Kda", inGame, a =>
            a.Target.GetPlayerController()?.GetActionTrackingService()?.GetMatchStats() is { } stats ? $"{stats.Kills} / {stats.Deaths} / {stats.Assists}" : "-");

        yield return new AdminPanelDetail("score", "AdminPanel.Detail.Score", inGame, a => a.Target.GetPlayerController()?.Score.ToString() ?? "-");
        yield return new AdminPanelDetail("mvp", "AdminPanel.Detail.Mvp", inGame, a => a.Target.GetPlayerController()?.MvpCount.ToString() ?? "-");

        yield return new AdminPanelDetail("money", "AdminPanel.Detail.Money", inGame, a =>
            a.Target.GetPlayerController()?.GetInGameMoneyService() is { } money ? $"${money.Account}" : "-");

        // Authority
        yield return new AdminPanelDetail("groups", "AdminPanel.Detail.Groups", authority, a =>
        {
            if (a.Target.IsFakeClient || TnmsPlugin.AdminManager.GetPermissionInformation(a.Target.SteamId) is not { Groups.Count: > 0 } info)
                return "-";

            return string.Join(", ", info.Groups.Select(g => g.GroupName).Order(StringComparer.OrdinalIgnoreCase));
        });

        yield return new AdminPanelDetail("immunity", "AdminPanel.Detail.Immunity", authority, a =>
            a.Target.IsFakeClient ? "-" : TnmsPlugin.AdminManager.GetPlayerImmunity(a.Target.SteamId).ToString());
    }
}
