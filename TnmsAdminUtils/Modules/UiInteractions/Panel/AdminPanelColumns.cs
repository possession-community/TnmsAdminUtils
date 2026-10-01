using System.Buffers.Binary;
using System.Net;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsPluginFoundation.Extensions.Client;

namespace TnmsAdminUtils.Modules.UiInteractions.Panel;

public enum AdminPanelColumnWidth
{
    XSmall,
    Small,
    Medium,
    Large,
    XLarge,
}

/// <param name="Key">Unique key; registering the same key replaces the column</param>
/// <param name="HeaderKey">Translation key of the header</param>
/// <param name="Value">Cell text for (target, viewer). Called every refresh, keep it cheap</param>
/// <param name="SortKey">Sort value for (target, viewer); null makes the column unsortable. A null value sorts last in
/// both directions. Strings compare case-insensitively</param>
public sealed record AdminPanelColumn(
    string Key,
    string HeaderKey,
    AdminPanelColumnWidth Width,
    Func<IGameClient, IGameClient, string> Value,
    Func<IGameClient, IGameClient, IComparable?>? SortKey = null);

/// <summary>
/// Columns of the panel's user list, in display order. The layout has <see cref="MaxColumns"/> column slots.
/// </summary>
public sealed class AdminPanelColumns
{
    public const int MaxColumns = 10;

    private readonly List<AdminPanelColumn> _columns = [];

    public IReadOnlyList<AdminPanelColumn> Columns => _columns;

    /// <returns>False when all column slots are used</returns>
    public bool Register(AdminPanelColumn column)
    {
        var index = _columns.FindIndex(c => c.Key == column.Key);

        if (index >= 0)
        {
            _columns[index] = column;
            return true;
        }

        if (_columns.Count >= MaxColumns)
            return false;

        _columns.Add(column);
        return true;
    }

    public void Unregister(string key) => _columns.RemoveAll(c => c.Key == key);

    // TODO: K/D, score, money and other per-player stats.
    public static IEnumerable<AdminPanelColumn> BuiltIn()
    {
        yield return new AdminPanelColumn("slot", "AdminPanel.Column.Slot", AdminPanelColumnWidth.XSmall, (target, _) => target.Slot.AsPrimitive().ToString(),
            (target, _) => target.Slot.AsPrimitive());

        yield return new AdminPanelColumn("userid", "AdminPanel.Column.UserId", AdminPanelColumnWidth.XSmall, (target, _) => target.UserId.AsPrimitive().ToString(),
            (target, _) => target.UserId.AsPrimitive());

        yield return new AdminPanelColumn("name", "AdminPanel.Column.Name", AdminPanelColumnWidth.Large, (target, _) => target.Name,
            (target, _) => target.Name);

        yield return new AdminPanelColumn("team", "AdminPanel.Column.Team", AdminPanelColumnWidth.XSmall, (target, _) => target.GetPlayerController()?.Team switch
        {
            CStrikeTeam.CT => "CT",
            CStrikeTeam.TE => "T",
            CStrikeTeam.Spectator => "SPEC",
            _ => "-",
        }, (target, _) => TeamOrder(target));

        yield return new AdminPanelColumn("hp", "AdminPanel.Column.Hp", AdminPanelColumnWidth.XSmall,
            (target, _) => target.GetPlayerPawn() is { IsAlive: true } pawn ? pawn.Health.ToString() : "-",
            (target, _) => target.GetPlayerPawn() is { IsAlive: true } pawn ? pawn.Health : null);

        yield return new AdminPanelColumn("steamid", "AdminPanel.Column.SteamId", AdminPanelColumnWidth.Large,
            (target, _) => target.IsFakeClient ? "BOT" : ((ulong)target.SteamId).ToString(),
            (target, _) => target.IsFakeClient ? null : (ulong)target.SteamId);

        yield return new AdminPanelColumn("ip", "AdminPanel.Column.Ip", AdminPanelColumnWidth.Large, (target, viewer) =>
        {
            if (target.IsFakeClient || target.Address is not { } address)
                return "-";

            return CanSeeIp(viewer) ? Host(address) : "***";
        }, (target, viewer) =>
        {
            // Hidden IPs must not leak through the order either.
            if (target.IsFakeClient || target.Address is not { } address || !CanSeeIp(viewer))
                return null;

            if (!IPAddress.TryParse(Host(address).Trim('[', ']'), out var ip))
                return null;

            Span<byte> bytes = stackalloc byte[16];
            ip.MapToIPv6().TryWriteBytes(bytes, out _);
            return new UInt128(BinaryPrimitives.ReadUInt64BigEndian(bytes), BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]));
        });

        yield return new AdminPanelColumn("ping", "AdminPanel.Column.Ping", AdminPanelColumnWidth.XSmall,
            (target, _) => target.IsFakeClient ? "-" : target.GetPlayerController()?.GetNetVar<uint>("m_iPing").ToString() ?? "-");

        yield return new AdminPanelColumn("time", "AdminPanel.Column.Time", AdminPanelColumnWidth.Small,
            (target, _) => target.IsFakeClient ? "-" : FormatDuration(target.TimeConnected),
            (target, _) => target.IsFakeClient ? null : target.TimeConnected);
    }

    /// <summary>
    /// CT, T, spectators, then unassigned. Also the default order of the user list.
    /// </summary>
    public static int TeamOrder(IGameClient client) => client.GetPlayerController()?.Team switch
    {
        CStrikeTeam.CT => 0,
        CStrikeTeam.TE => 1,
        CStrikeTeam.Spectator => 2,
        _ => 3,
    };

    /// <summary>
    /// Compares two sort values of the same column; strings ignore case.
    /// </summary>
    public static int CompareSortKeys(IComparable? a, IComparable? b)
    {
        if (a is null || b is null)
            return (a is null).CompareTo(b is null);

        return a is string x && b is string y ? string.Compare(x, y, StringComparison.OrdinalIgnoreCase) : a.CompareTo(b);
    }

    private static bool CanSeeIp(IGameClient viewer) => TnmsPlugin.AdminManager.PlayerHasPermission(viewer.SteamId, AdminPanelService.IpPermission);

    private static string Host(string address)
    {
        var port = address.LastIndexOf(':');
        return port > 0 ? address[..port] : address;
    }

    public static string FormatDuration(float seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time:mm\\:ss}" : time.ToString("m\\:ss");
    }
}
