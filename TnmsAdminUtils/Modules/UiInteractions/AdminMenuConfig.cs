using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// menu.json in the module directory. Missing keys fall back to the defaults below.
/// </summary>
public sealed class AdminMenuConfig
{
    public const string FileName = "menu.json";

    private static readonly Dictionary<string, List<string>> DefaultPresets = new()
    {
        ["slap"] = ["0", "1", "5", "10", "50", "100"],
        ["hp"] = ["1", "50", "100", "200", "500"],
        ["money"] = ["0", "800", "4000", "10000", "16000"],
        ["setkev"] = ["0", "50", "100"],
        ["drop"] = ["0", "1", "2", "3", "4"],
        ["gravity"] = ["0.25", "0.5", "1", "2"],
        ["speed"] = ["0.5", "1", "1.5", "2", "3"],
        ["freeze"] = ["3", "5", "10", "30"],
        ["blind"] = ["3", "5", "10"],
        ["shake"] = ["3", "5", "10"],
        ["give"] = ["ak47", "m4a1_silencer", "m4a1", "awp", "ssg08", "p90", "deagle", "usp_silencer", "glock", "knife", "hegrenade", "flashbang", "smokegrenade", "molotov", "healthshot"],
        ["addtime"] = ["60", "300", "-60", "-300"],
        ["settime"] = ["60", "180", "300", "600"],
        ["terminateround"] = ["0", "3", "5", "10"],
        ["toast"] = ["3s", "5s", "10s", "20s"],
    };

    public Dictionary<string, List<string>> Presets { get; set; } = new();

    public IReadOnlyList<string> GetPreset(string key)
    {
        if (Presets.TryGetValue(key, out var values) && values.Count > 0)
            return values;

        return DefaultPresets.TryGetValue(key, out var defaults) ? defaults : [];
    }

    public static AdminMenuConfig Load(string moduleDirectory, ILogger logger)
    {
        var path = Path.Combine(moduleDirectory, FileName);

        if (!File.Exists(path))
        {
            var config = new AdminMenuConfig { Presets = DefaultPresets.ToDictionary(p => p.Key, p => p.Value.ToList()) };

            try
            {
                File.WriteAllText(path, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to write the default {File}", FileName);
            }

            return config;
        }

        try
        {
            return JsonSerializer.Deserialize<AdminMenuConfig>(File.ReadAllText(path)) ?? new AdminMenuConfig();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to read {File}, using the default presets", FileName);
            return new AdminMenuConfig();
        }
    }
}
