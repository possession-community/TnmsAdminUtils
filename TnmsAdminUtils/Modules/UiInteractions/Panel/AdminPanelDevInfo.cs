using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Sharp.Shared;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;
using TnmsPluginFoundation;
using Wuling.Abstract.Tianshi.Liuli;

namespace TnmsAdminUtils.Modules.UiInteractions.Panel;

/// <param name="Viewer">The admin looking at the page</param>
/// <param name="Stats">Server measurements of the last second</param>
/// <param name="Localize">Translates a key into the viewer's language</param>
public sealed record AdminPanelDevArgs(IGameClient Viewer, AdminPanelServerStats Stats, Func<string, string> Localize);

/// <summary>
/// Headed groups of the Dev page. Performance / Uptime / Resources / Connections are on the Server sub page,
/// Runtime / Components on Versions.
/// </summary>
public enum AdminPanelDevGroup
{
    Performance,
    Uptime,
    Resources,
    Connections,
    /// <summary>ModSharp, the game, .NET and the OS.</summary>
    Runtime,
    /// <summary>TnmsAdminUtils and the libraries it runs on.</summary>
    Components,
}

/// <param name="Key">Unique key; registering the same key replaces the row</param>
/// <param name="LabelKey">Translation key of the row's label</param>
/// <param name="Group">The group the row is listed in</param>
/// <param name="Value">Row text. Called every refresh, keep it cheap</param>
public sealed record AdminPanelDevRow(string Key, string LabelKey, AdminPanelDevGroup Group, Func<AdminPanelDevArgs, string> Value);

/// <summary>
/// Rows of the panel's Dev page, in display order within their group (up to <see cref="Capacity"/> each).
/// </summary>
public sealed class AdminPanelDevInfo
{
    public const int Capacity = 8;

    private readonly List<AdminPanelDevRow> _rows = [];

    public IReadOnlyList<AdminPanelDevRow> Rows => _rows;

    /// <returns>False when all rows of the row's group are used</returns>
    public bool Register(AdminPanelDevRow row)
    {
        var index = _rows.FindIndex(r => r.Key == row.Key);

        if (index >= 0)
        {
            _rows[index] = row;
            return true;
        }

        if (_rows.Count(r => r.Group == row.Group) >= Capacity)
            return false;

        _rows.Add(row);
        return true;
    }

    public void Unregister(string key) => _rows.RemoveAll(r => r.Key == key);

    public IReadOnlyList<AdminPanelDevRow> Of(AdminPanelDevGroup group) => _rows.Where(r => r.Group == group).ToList();

    public static IEnumerable<AdminPanelDevRow> BuiltIn(ISharedSystem shared)
    {
        const AdminPanelDevGroup performance = AdminPanelDevGroup.Performance;
        const AdminPanelDevGroup uptime = AdminPanelDevGroup.Uptime;
        const AdminPanelDevGroup resources = AdminPanelDevGroup.Resources;
        const AdminPanelDevGroup connections = AdminPanelDevGroup.Connections;
        const AdminPanelDevGroup runtime = AdminPanelDevGroup.Runtime;
        const AdminPanelDevGroup components = AdminPanelDevGroup.Components;

        var sharp = shared.GetModSharp();

        // Performance
        yield return new AdminPanelDevRow("tickrate", "AdminPanel.Dev.Tickrate", performance, a => a.Stats.TicksPerSecond is { } t ? $"{t:0.0}" : "-");
        yield return new AdminPanelDevRow("fps", "AdminPanel.Dev.Fps", performance, a => a.Stats.FramesPerSecond is { } f ? $"{f:0.0}" : "-");
        yield return new AdminPanelDevRow("frametime", "AdminPanel.Dev.FrameTime", performance, a => a.Stats.FramesPerSecond is > 0 and var f ? $"{1000 / f:0.00} ms" : "-");
        yield return new AdminPanelDevRow("jitter", "AdminPanel.Dev.Jitter", performance, _ => $"{sharp.GetGlobals().AbsoluteFrameStartTimeStdDev * 1000:0.00} ms");
        yield return new AdminPanelDevRow("tick", "AdminPanel.Dev.Tick", performance, _ => sharp.GetGlobals().TickCount.ToString());

        // Uptime
        yield return new AdminPanelDevRow("uptime", "AdminPanel.Dev.Uptime", uptime, a => FormatUptime(a.Stats.Uptime));
        yield return new AdminPanelDevRow("maptime", "AdminPanel.Dev.MapTime", uptime, _ => FormatUptime(TimeSpan.FromSeconds(Math.Max(0, sharp.GetGlobals().CurTime))));
        yield return new AdminPanelDevRow("clock", "AdminPanel.Dev.Clock", uptime, _ => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        // Resources
        yield return new AdminPanelDevRow("memory", "AdminPanel.Dev.Memory", resources, a => FormatBytes(a.Stats.WorkingSet));
        yield return new AdminPanelDevRow("heap", "AdminPanel.Dev.Heap", resources, _ => FormatBytes(GC.GetTotalMemory(false)));
        yield return new AdminPanelDevRow("gc", "AdminPanel.Dev.Gc", resources, _ => $"{GC.CollectionCount(0)} / {GC.CollectionCount(1)} / {GC.CollectionCount(2)}");
        yield return new AdminPanelDevRow("entities", "AdminPanel.Dev.Entities", resources, a => $"{a.Stats.Entities()} / {sharp.GetGlobals().MaxEntities}");

        // Connections
        yield return new AdminPanelDevRow("humans", "AdminPanel.Dev.Humans", connections, a => a.Stats.Humans.ToString());
        yield return new AdminPanelDevRow("bots", "AdminPanel.Dev.Bots", connections, a => a.Stats.Bots.ToString());
        yield return new AdminPanelDevRow("connecting", "AdminPanel.Dev.Connecting", connections, a => a.Stats.Connecting.ToString());
        yield return new AdminPanelDevRow("slots", "AdminPanel.Dev.Slots", connections, a => $"{a.Stats.Humans + a.Stats.Bots + a.Stats.Connecting} / {sharp.GetGlobals().MaxClients}");

        // Runtime
        var modSharp = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Sharp.Core");
        var game = GameVersion(sharp.GetGamePath());

        yield return new AdminPanelDevRow("modsharp", "AdminPanel.Dev.ModSharp", runtime, _ => modSharp is null ? "-" : AssemblyVersion(modSharp));
        yield return new AdminPanelDevRow("sharpshared", "AdminPanel.Dev.SharpShared", runtime, _ => AssemblyVersion(typeof(IModSharp).Assembly));
        yield return new AdminPanelDevRow("game", "AdminPanel.Dev.Game", runtime, _ => game);
        yield return new AdminPanelDevRow("dotnet", "AdminPanel.Dev.DotNet", runtime, _ => RuntimeInformation.FrameworkDescription);
        yield return new AdminPanelDevRow("os", "AdminPanel.Dev.Os", runtime, _ => $"{RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})");

        // Components
        yield return new AdminPanelDevRow("adminutils", "AdminPanel.Dev.AdminUtils", components, _ => AssemblyVersion(typeof(AdminPanelDevInfo).Assembly));
        yield return new AdminPanelDevRow("foundation", "AdminPanel.Dev.Foundation", components, _ => AssemblyVersion(typeof(TnmsPlugin).Assembly));
        yield return new AdminPanelDevRow("wuling", "AdminPanel.Dev.WulingAbstract", components, _ => AssemblyVersion(typeof(ILiuliSurface).Assembly));
    }

    /// <summary>
    /// The informational version with its commit hash cut to 7 characters, or the assembly version.
    /// </summary>
    internal static string AssemblyVersion(Assembly assembly)
    {
        if (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is { Length: > 0 } info)
        {
            var plus = info.IndexOf('+');
            return plus >= 0 && info.Length > plus + 8 ? info[..(plus + 8)] : info;
        }

        return assembly.GetName().Version?.ToString() ?? "-";
    }

    /// <summary>
    /// PatchVersion and the build date from csgo/steam.inf, read once.
    /// </summary>
    private static string GameVersion(string gamePath)
    {
        try
        {
            var values = File.ReadLines(Path.Combine(gamePath, "steam.inf"))
                .Select(l => l.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

            if (!values.TryGetValue("PatchVersion", out var patch))
                return "-";

            return values.TryGetValue("VersionDate", out var date) ? $"{patch} ({date})" : patch;
        }
        catch (Exception)
        {
            return "-";
        }
    }

    private static string FormatUptime(TimeSpan time)
        => time.TotalDays >= 1 ? $"{(int)time.TotalDays}d {time:hh\\:mm\\:ss}" : time.ToString("hh\\:mm\\:ss");

    private static string FormatBytes(long bytes) => $"{bytes / 1024.0 / 1024.0:0.0} MB";
}

/// <summary>
/// Measurements for the Dev page, taken by <see cref="AdminPanelService"/> once a second: tick and frame rates over
/// the measured interval, the process' memory and the connections. Entities are counted on demand, once per tick.
/// </summary>
public sealed class AdminPanelServerStats(ISharedSystem shared)
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly DateTime _started = Process.GetCurrentProcess().StartTime;

    private double _lastTime = double.NaN;
    private int _lastFrame;
    private int _lastTick;
    private int _entityTick = -1;
    private int _entities;

    /// <summary>Null until two samples are taken.</summary>
    public double? TicksPerSecond { get; private set; }

    /// <summary>Null until two samples are taken.</summary>
    public double? FramesPerSecond { get; private set; }

    public TimeSpan Uptime => DateTime.Now - _started;
    public long WorkingSet { get; private set; }
    public int Humans { get; private set; }
    public int Bots { get; private set; }
    public int Connecting { get; private set; }

    public void Sample()
    {
        var sharp = shared.GetModSharp();
        var globals = sharp.GetGlobals();
        var now = sharp.EngineTime();

        // The timer is not exactly a second apart; divide by the time that really passed.
        if (!double.IsNaN(_lastTime) && now - _lastTime > 0.1)
        {
            var elapsed = now - _lastTime;
            TicksPerSecond = (globals.TickCount - _lastTick) / elapsed;
            FramesPerSecond = (globals.FrameCount - _lastFrame) / elapsed;
        }

        _lastTime = now;
        _lastTick = globals.TickCount;
        _lastFrame = globals.FrameCount;

        _process.Refresh();
        WorkingSet = _process.WorkingSet64;

        var connected = sharp.GetIServer().GetGameClients(true).Where(c => !c.IsHltv).ToList();
        var inGame = connected.Where(c => c.IsInGame).ToList();
        Humans = inGame.Count(c => !c.IsFakeClient);
        Bots = inGame.Count(c => c.IsFakeClient);
        Connecting = connected.Count - inGame.Count;
    }

    /// <summary>
    /// Networked entities (index below MaxEntities), counted at most once per tick.
    /// </summary>
    public int Entities()
    {
        var sharp = shared.GetModSharp();
        var globals = sharp.GetGlobals();

        if (_entityTick == globals.TickCount)
            return _entities;

        var manager = shared.GetEntityManager();
        var count = 0;

        for (var i = 0; i < globals.MaxEntities; i++)
        {
            if (manager.FindEntityByIndex((EntityIndex)i) is not null)
                count++;
        }

        _entityTick = globals.TickCount;
        _entities = count;
        return count;
    }
}
