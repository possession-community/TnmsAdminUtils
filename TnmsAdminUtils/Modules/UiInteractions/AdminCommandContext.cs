using System.Globalization;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsPluginFoundation.Extensions.Client;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <param name="Value">The value written to the command line</param>
/// <param name="Label">How the choice is shown (selectors carry their player count)</param>
public sealed record AdminMenuChoiceItem(AdminMenuValue Value, string Label);

/// <summary>
/// What the menu flow and the panel form share for one admin: the commands the admin can see, the choices of an
/// argument, and turning the chosen values into a preview / command line.
/// </summary>
public sealed class AdminCommandContext(TnmsAdminUtils plugin, AdminMenuService service, IGameClient admin)
{
    public const int TextInputTimeoutSeconds = 60;

    private static readonly (string Target, string LabelKey)[] Selectors =
    [
        ("@all", "AdminMenu.Selector.All"),
        ("@ct", "AdminMenu.Selector.Ct"),
        ("@t", "AdminMenu.Selector.T"),
        ("@spec", "AdminMenu.Selector.Spec"),
    ];

    public IGameClient Admin => admin;

    /// <summary>
    /// The admin's commands of one list, from <see cref="AdminCommandCache"/>. With a selector chosen as the target,
    /// Players keeps only the commands that accept selectors.
    /// </summary>
    public IReadOnlyList<AdminMenuEntry> VisibleEntries(AdminMenuList list, AdminMenuValue? chosenTarget = null)
    {
        var entries = service.Commands.Get(admin.SteamId, list);

        return list.IsPlayers && chosenTarget is { IsSelector: true }
            ? entries.Where(e => e.PrimaryTarget!.AllowSelectors).ToList()
            : entries;
    }

    /// <summary>
    /// Categories the admin has commands in, built-in ones first.
    /// </summary>
    public IReadOnlyList<AdminMenuCategoryDefinition> VisibleCategories() => service.Commands.Categories(admin.SteamId);

    /// <summary>
    /// Favorites in the order they were added, skipping ids that are not registered or not permitted right now.
    /// </summary>
    public List<AdminMenuEntry> FavoriteEntries(AdminFavorites favorites)
        => favorites.Ids.Select(id => service.Commands.Find(admin.SteamId, id)).OfType<AdminMenuEntry>().ToList();

    public static AdminMenuList ListOf(AdminMenuEntry entry) => new(entry.Category);

    /// <summary>
    /// Selectors (with their current player count) and the players the admin can target.
    /// </summary>
    public List<AdminMenuChoiceItem> TargetChoices(bool allowSelectors)
    {
        var items = new List<AdminMenuChoiceItem>();
        var authority = TnmsPlugin.AdminManager;

        if (allowSelectors)
        {
            foreach (var (target, labelKey) in Selectors)
            {
                var count = TnmsPlugin.TargetingManager.GetByTarget(admin, target).Count();
                var value = new AdminMenuValue(target, L(labelKey), IsSelector: true);
                items.Add(new AdminMenuChoiceItem(value, $"{value.Display} ({count})"));
            }
        }

        foreach (var client in plugin.SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (client.IsHltv)
                continue;

            if (!client.IsFakeClient && !authority.PlayerCanTarget(admin.SteamId, client.SteamId))
                continue;

            var value = PlayerValue(client);
            items.Add(new AdminMenuChoiceItem(value, client.Name));
        }

        return items;
    }

    /// <summary>
    /// Humans by SteamID64, bots by their literal name.
    /// </summary>
    public static AdminMenuValue PlayerValue(IGameClient client)
        => new(client.IsFakeClient ? $"\"#{client.Name}\"" : ((ulong)client.SteamId).ToString(), client.Name);

    /// <summary>
    /// Fixed values of a preset, choice or value argument (presets come from the menu config).
    /// </summary>
    public List<AdminMenuChoiceItem> ValueChoices(AdminMenuArgument argument)
    {
        switch (argument)
        {
            case PresetArgument preset:
                return service.Config.GetPreset(preset.PresetKey).Select(v => new AdminMenuChoiceItem(new AdminMenuValue(v, v), v)).ToList();

            case ValueArgument value:
                return value.Suggestions.Select(v => new AdminMenuChoiceItem(ValueOf(value, v), v)).ToList();

            case ChoiceArgument choice:
                return choice.Choices.Select(option =>
                {
                    var label = option.Label is null ? option.Value : Text(option.Label);
                    return new AdminMenuChoiceItem(new AdminMenuValue(option.Value, label), label);
                }).ToList();

            default:
                return [];
        }
    }

    /// <summary>
    /// The value of an optional argument left at the command's own default.
    /// </summary>
    public AdminMenuValue Skipped(AdminMenuArgument argument) => new(argument.DefaultRaw ?? string.Empty, L("AdminMenu.Default"), IsSkipped: true);

    /// <summary>
    /// Turns a chat message into the value of a text argument. Returns false (after telling the admin) when the text
    /// does not fit, e.g. too few vote options or a number out of range.
    /// </summary>
    public bool TryParseText(AdminMenuArgument argument, string text, out AdminMenuValue value)
    {
        text = new string(text.Where(c => c is not (';' or '\r' or '\n')).ToArray());

        if (argument is ValueArgument typed)
        {
            if (typed.Check(text) is { } reason)
            {
                PrintToChat(reason, typed.RangeText);
                value = null!;
                return false;
            }

            value = ValueOf(typed, text);
            return true;
        }

        if (argument is TextListArgument list)
        {
            var items = text.Split([',', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (items.Length < list.MinCount)
            {
                PrintToChat("AdminMenu.List.NeedMore", list.MinCount);
                value = null!;
                return false;
            }

            value = new AdminMenuValue(string.Join(' ', items.Select(AdminMenuValue.Quote)), string.Join(" / ", items));
            return true;
        }

        value = argument is TextArgument { Quote: true }
            ? new AdminMenuValue(AdminMenuValue.Quote(text), text)
            : new AdminMenuValue(text, text);
        return true;
    }

    /// <summary>
    /// The exact command <see cref="Execute"/> runs. Values are written up to the last one that is set, so it grows as
    /// the form is filled; a missing value before that shows as &lt;field&gt;.
    /// </summary>
    public string CommandLine(AdminMenuEntry entry, IReadOnlyList<AdminMenuValue?> values)
        => string.Join(' ', values.Take(LastWritten(values))
            .Select((v, i) => v?.Raw ?? $"<{FieldLabel(entry.Arguments[i])}>")
            .Prepend("ms_" + entry.CommandName));

    /// <summary>
    /// Players the target resolves to right now, e.g. "3: alice, bob, carol".
    /// </summary>
    public string DescribeTargets(AdminMenuValue target)
    {
        const int maxNames = 8;

        var raw = target.Raw.Trim('"');
        var names = TnmsPlugin.TargetingManager.GetByTarget(admin, raw).Select(c => c.Name).ToList();

        if (names.Count == 0)
            return L("AdminMenu.Confirm.NoTargets");

        var shown = string.Join(", ", names.Take(maxNames));

        return names.Count > maxNames
            ? $"{names.Count}: {shown} {L("AdminMenu.Confirm.More", names.Count - maxNames)}"
            : $"{names.Count}: {shown}";
    }

    /// <summary>
    /// Runs the command as the admin, so permission checks, logs and broadcasts are the same as typing it.
    /// Skipped arguments at the end are left out so the command uses its own defaults.
    /// </summary>
    public void Execute(AdminMenuEntry entry, IReadOnlyList<AdminMenuValue> values)
    {
        if (admin.IsValid)
            admin.ExecuteStringCommand(CommandLine(entry, values));
    }

    public string CommandLabel(AdminMenuEntry entry) => TranslatedLabel(entry) ?? entry.CommandName;

    /// <summary>
    /// The translated label, or null when the command has none.
    /// </summary>
    public string? TranslatedLabel(AdminMenuEntry entry) => TextOrNull(entry.Name);

    public string? Usage(AdminMenuEntry entry) => entry.UsageKey is { } key ? L(key) : null;

    /// <summary>
    /// The command's description, or null when there is none.
    /// </summary>
    public string? Description(AdminMenuEntry entry) => TextOrNull(entry.Description);

    public string CategoryName(AdminMenuCategoryDefinition category) => Text(category.Name);

    /// <summary>
    /// Heading of an argument while it is chosen ("Choose a target").
    /// </summary>
    public string Title(AdminMenuArgument argument) => Text(argument.Title);

    /// <summary>
    /// Short heading of an argument in the form ("Target"): <c>AdminMenu.Field.*</c> next to its
    /// <c>AdminMenu.Step.*</c> title, falling back to the title.
    /// </summary>
    public string FieldLabel(AdminMenuArgument argument)
    {
        const string stepPrefix = "AdminMenu.Step.";

        if (argument.Title.Key is { } titleKey && titleKey.StartsWith(stepPrefix, StringComparison.Ordinal))
        {
            var key = "AdminMenu.Field." + titleKey[stepPrefix.Length..];
            var text = L(key);

            if (text != key)
                return text;
        }

        return Title(argument);
    }

    /// <summary>
    /// The text in the admin's language. A lang key without a translation comes back as the key itself.
    /// </summary>
    public string Text(AdminMenuText text) => text.Key is { } key ? L(key) : text.Inline(Culture()) ?? string.Empty;

    /// <summary>
    /// Like <see cref="Text"/>, but null for a lang key without a translation.
    /// </summary>
    public string? TextOrNull(AdminMenuText text)
    {
        var resolved = Text(text);
        return resolved.Length == 0 || resolved == text.Key ? null : resolved;
    }

    public void PrintToChat(string key, params object[] args)
        => admin.GetPlayerController()?.PrintToChat(plugin.LocalizeWithPluginPrefix(admin, key, args));

    public string L(string key) => plugin.LocalizeStringForPlayer(admin, key);

    private CultureInfo Culture() => plugin.LocalizationPlatform.GetPlayerCulture(admin.SteamId);

    private static AdminMenuValue ValueOf(ValueArgument argument, string text)
        => new(argument.Quote ? AdminMenuValue.Quote(text) : text, text);

    public string L(string key, params object[] args) => plugin.LocalizeStringForPlayer(admin, key, args);

    // Count of leading values up to the last one that is set (not missing, not left at its default).
    private static int LastWritten(IReadOnlyList<AdminMenuValue?> values)
    {
        for (var i = values.Count - 1; i >= 0; i--)
        {
            if (values[i] is { IsSkipped: false })
                return i + 1;
        }

        return 0;
    }
}
