using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sharp.Modules.LocalizerManager.Shared;
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

    private ILocalizerManager? _localizerManager;

    protected override void TnmsOnPluginLoad(bool hotReload)
    {
        AddTnmsCommandsUnderNamespace("TnmsAdminUtils", true);
        Logger.LogInformation("TnmsAdminUtils is initialized");
    }

    protected override void TnmsAllPluginsLoaded(bool hotReload)
    {
        _localizerManager = SharedSystem.GetSharpModuleManager()
            .GetOptionalSharpModuleInterface<ILocalizerManager>(ILocalizerManager.Identity)?.Instance;

        if (_localizerManager is not null)
        {
            _localizerManager.LoadLocaleFile("tnmsadminutils");
        }
        else
        {
            Logger.LogWarning("LocalizerManager not found. Using default English messages.");
        }
    }

    /// <summary>
    /// Localizes the key for the given client and prepends the plugin prefix.
    /// Uses Sharp.Modules.LocalizerManager when available, falls back to the key itself otherwise.
    /// </summary>
    public string LocalizeWithPluginPrefix(IGameClient? client, string localizationKey, params object?[] args)
    {
        if (_localizerManager is null)
        {
            var fallback = args.Length > 0 ? string.Format(localizationKey, args) : localizationKey;
            return $" {PluginPrefix} {fallback}";
        }

        if (client is null)
        {
            var serverPrefix = _localizerManager.Format(System.Globalization.CultureInfo.CurrentCulture, PluginPrefix);
            var serverMessage = _localizerManager.Format(System.Globalization.CultureInfo.CurrentCulture, localizationKey, args);
            return $" {serverPrefix} {serverMessage}";
        }

        var locale = _localizerManager.For(client);
        var prefix = locale.Text(PluginPrefix);
        var message = locale.Text(localizationKey, args);
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
