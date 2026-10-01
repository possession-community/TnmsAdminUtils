using System.Globalization;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// Text shown in the admin menu: a key of TnmsAdminUtils' lang files, or translations written inline in a menu TOML
/// (culture name to text). Inline text lets a TOML from another plugin carry its own labels, since TnmsAdminUtils
/// only reads its own lang files.
/// </summary>
public sealed class AdminMenuText
{
    public string? Key { get; }

    private readonly IReadOnlyDictionary<string, string>? _inline;

    private AdminMenuText(string? key, IReadOnlyDictionary<string, string>? inline)
    {
        Key = key;
        _inline = inline;
    }

    public static AdminMenuText FromKey(string key) => new(key, null);

    public static AdminMenuText FromInline(IReadOnlyDictionary<string, string> texts)
        => new(null, new Dictionary<string, string>(texts, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Inline text for the culture: the exact name ("pt-BR"), its language ("pt"), English, then the first one.
    /// Null for a key text.
    /// </summary>
    public string? Inline(CultureInfo culture)
    {
        if (_inline is null || _inline.Count == 0)
            return null;

        if (_inline.TryGetValue(culture.Name, out var text) || _inline.TryGetValue(culture.TwoLetterISOLanguageName, out text) || _inline.TryGetValue("en", out text))
            return text;

        return _inline.Values.First();
    }
}
