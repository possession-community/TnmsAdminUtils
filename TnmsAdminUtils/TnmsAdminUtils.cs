using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Objects;
using TnmsAdminUtils.Modules.ClientManagement;
using TnmsAdminUtils.Modules.InGameManagement;
using TnmsAdminUtils.Modules.ServerManagement;
using TnmsPluginFoundation;

namespace TnmsAdminUtils;

public class TnmsAdminUtils(
    ISharedSystem sharedSystem,
    string dllPath,
    string sharpPath,
    Version? version,
    IConfiguration coreConfiguration,
    bool hotReload)
    : TnmsPlugin(sharedSystem, dllPath, sharpPath, version, coreConfiguration, hotReload)
{
    public override string DisplayName => "TnmsAdminUtils";
    public override string DisplayAuthor => "faketuna";
    public override string BaseCfgDirectoryPath => "";
    public override string ConVarConfigPath => "TnmsAdminUtils/convars.cfg";
    public override string PluginPrefix => "Plugin.Prefix";
    public override bool UseTranslationKeyInPluginPrefix => true;

    protected override void TnmsOnPluginLoad(bool hotReload)
    {
        AddTnmsCommandsUnderNamespace("TnmsAdminUtils", true);
        Logger.LogInformation("TnmsAdminUtils is initialized");
    }

    /// <summary>
    /// Localizes the key for the given client and prepends the plugin prefix.
    /// Uses the foundation's Wuling-based localizer (lang/&lt;culture&gt;.json);
    /// a null client resolves with the server's default culture.
    /// </summary>
    public string LocalizeWithPluginPrefix(IGameClient? client, string localizationKey, params object?[] args)
    {
        var prefix = GetPluginPrefix(client);
        var message = LocalizeStringForPlayer(client, localizationKey, (object[])args);
        return $" {prefix} {message}";
    }

    /// <summary>
    /// Localizes the key for the given client and prepends the plugin prefix.
    /// </summary>
    public string LocalizeWithPluginPrefix(IGameClient? client, string localizationKey)
    {
        return LocalizeWithPluginPrefix(client, localizationKey, Array.Empty<object?>());
    }
}
