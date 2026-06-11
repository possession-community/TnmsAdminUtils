using Sharp.Shared.Objects;

namespace TnmsAdminUtils.Utils;

/// <summary>
/// Helpers for targeting results returned by TnmsPluginFoundation's TargetValidator.
/// </summary>
public static class TargetUtil
{
    /// <summary>
    /// Returns a display name for the resolved targets:
    /// the player's name when exactly one target was found, otherwise "N players".
    /// </summary>
    public static string GetTargetName(this List<IGameClient> targets) =>
        targets.Count == 1 ? targets[0].Name : $"{targets.Count} players";
}
