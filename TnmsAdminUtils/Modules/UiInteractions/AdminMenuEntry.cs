using System.Globalization;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <param name="Title">Heading of the argument while it is chosen</param>
public abstract record AdminMenuArgument(AdminMenuText Title)
{
    /// <summary>
    /// The menu offers "default" for this argument. A skipped trailing argument is left out of the command line.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Written in place of a skipped argument when a later argument has a value.
    /// </summary>
    public string? DefaultRaw { get; init; }

    /// <summary>
    /// The argument has values to pick from. Without any, it is typed in chat only.
    /// </summary>
    public virtual bool HasChoices => true;

    /// <summary>
    /// The value can be typed in chat.
    /// </summary>
    public virtual bool AcceptsText => false;
}

/// <summary>
/// A player argument. The first target of an entry is the primary one (the player picked in the Players tree).
/// </summary>
/// <summary>
/// Players and (when allowed) selectors to choose from; any target string can be typed in chat too, e.g. a selector
/// another plugin registered.
/// </summary>
public sealed record TargetArgument(AdminMenuText Title, bool AllowSelectors) : AdminMenuArgument(Title)
{
    public override bool AcceptsText => true;
}

/// <summary>
/// Values are read from the menu config's presets by <see cref="PresetKey"/>; any value can be typed in chat too.
/// </summary>
public sealed record PresetArgument(AdminMenuText Title, string PresetKey) : AdminMenuArgument(Title)
{
    public override bool AcceptsText => true;
}

public sealed record ChoiceArgument(AdminMenuText Title, IReadOnlyList<AdminMenuChoice> Choices) : AdminMenuArgument(Title);

/// <summary>
/// Free text typed in chat. Appended to the command line as is, or as one quoted argument when <see cref="Quote"/> is set.
/// </summary>
public sealed record TextArgument(AdminMenuText Title, bool Quote) : AdminMenuArgument(Title)
{
    public override bool HasChoices => false;
    public override bool AcceptsText => true;
}

/// <summary>
/// Comma separated texts typed in chat at once, each passed as a quoted argument.
/// </summary>
public sealed record TextListArgument(AdminMenuText Title, int MinCount) : AdminMenuArgument(Title)
{
    public override bool HasChoices => false;
    public override bool AcceptsText => true;
}

public enum AdminMenuValueKind
{
    Int,
    Float,
    String,
}

/// <summary>
/// A value with suggestions, defined in a menu TOML. Typed numbers are checked against <see cref="Kind"/>,
/// <see cref="Min"/> and <see cref="Max"/>.
/// </summary>
public sealed record ValueArgument(AdminMenuText Title, AdminMenuValueKind Kind, IReadOnlyList<string> Suggestions, bool AllowCustomInput)
    : AdminMenuArgument(Title)
{
    public double? Min { get; init; }
    public double? Max { get; init; }

    /// <summary>
    /// Typed text is passed as one quoted argument.
    /// </summary>
    public bool Quote { get; init; }

    public override bool HasChoices => Suggestions.Count > 0;
    public override bool AcceptsText => AllowCustomInput;

    /// <summary>
    /// Null when the text is a valid value, otherwise the translation key of the reason (argument: the range).
    /// </summary>
    public string? Check(string text)
    {
        double number;

        switch (Kind)
        {
            case AdminMenuValueKind.Int when long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer):
                number = integer;
                break;
            case AdminMenuValueKind.Int:
                return "AdminMenu.Value.NotInt";
            case AdminMenuValueKind.Float when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number):
                break;
            case AdminMenuValueKind.Float:
                return "AdminMenu.Value.NotNumber";
            default:
                return null;
        }

        return number < Min || number > Max ? "AdminMenu.Value.OutOfRange" : null;
    }

    /// <summary>
    /// The allowed range for messages, e.g. "1 ~ 100", "1 ~", "~ 100".
    /// </summary>
    public string RangeText => $"{Min?.ToString(CultureInfo.InvariantCulture)} ~ {Max?.ToString(CultureInfo.InvariantCulture)}".Trim();
}

/// <summary>
/// Keys of the built-in command lists. Menu TOMLs may put operations in them but may not redefine them.
/// </summary>
public static class AdminMenuCategory
{
    public const string General = "general";
    public const string Server = "server";
    /// <summary>
    /// Chat / screen messages (say, csay, toast, ...).
    /// </summary>
    public const string Notification = "notification";

    public static readonly IReadOnlyList<AdminMenuCategoryDefinition> BuiltIn =
    [
        new(General, AdminMenuText.FromKey("AdminMenu.Root.Commands"), null, "player"),
        new(Server, AdminMenuText.FromKey("AdminMenu.Root.Server"), null, "settings"),
        new(Notification, AdminMenuText.FromKey("AdminMenu.Root.Notification"), null, "alert"),
    ];

    public static bool IsBuiltIn(string key) => BuiltIn.Any(c => c.Key == key);
}

/// <summary>
/// A command list of the menu / panel.
/// </summary>
/// <param name="Permission">Needed to see the list, or null to show it to anyone who can run one of its commands</param>
/// <param name="Icon">The panel sidebar's icon: a CS2 UI icon name (AdminPanelIcons) or a glyph (no emoji); null for the fallback</param>
/// <param name="IconSize">Size correction of the icon: "s", "m" or "l"; null for the panel's own choice</param>
public sealed record AdminMenuCategoryDefinition(string Key, AdminMenuText Name, string? Permission, string? Icon = null, string? IconSize = null);

/// <param name="Value">Value passed to the command</param>
/// <param name="Label">Label of the choice, or null to show <see cref="Value"/></param>
public sealed record AdminMenuChoice(string Value, AdminMenuText? Label)
{
    /// <param name="labelKey">Translation key of the label, or null to show the value</param>
    public AdminMenuChoice(string value, string? labelKey = null) : this(value, labelKey is null ? null : AdminMenuText.FromKey(labelKey))
    {
    }
}

/// <summary>
/// Describes how a command is run from the admin menu. Arguments are listed in command line order.
/// </summary>
public sealed class AdminMenuEntry
{
    /// <summary>
    /// Arguments the panel form has fields for.
    /// </summary>
    public const int MaxArguments = 6;

    public string CommandName { get; }
    public string Permission { get; }
    public IReadOnlyList<AdminMenuArgument> Arguments => _arguments;

    /// <summary>
    /// Stable identity of the entry, stored in favorites. Defaults to <see cref="CommandName"/>; a menu TOML
    /// operation uses its table key.
    /// </summary>
    public string MenuId { get; private set; }

    /// <summary>
    /// Label in the command lists. Defaults to <c>AdminMenu.Command.&lt;command&gt;</c>.
    /// </summary>
    public AdminMenuText Name { get; private set; }

    /// <summary>
    /// Shown in the panel form. Defaults to <c>AdminMenu.Description.&lt;command&gt;</c>.
    /// </summary>
    public AdminMenuText Description { get; private set; }

    private readonly List<AdminMenuArgument> _arguments = [];

    private AdminMenuEntry(string commandName, string permission)
    {
        CommandName = commandName;
        Permission = permission;
        MenuId = commandName;
        Name = AdminMenuText.FromKey($"AdminMenu.Command.{commandName}");
        Description = AdminMenuText.FromKey($"AdminMenu.Description.{commandName}");
    }

    public static AdminMenuEntry Create(string commandName, string permission) => new(commandName, permission);

    /// <summary>
    /// Translation key of the command's usage line, shown while its arguments are being chosen.
    /// </summary>
    public string? UsageKey { get; private set; }

    private string? _category;

    /// <summary>
    /// Key of the command list. Defaults to <see cref="AdminMenuCategory.Server"/> without a target argument,
    /// otherwise <see cref="AdminMenuCategory.General"/>.
    /// </summary>
    public string Category => _category ?? (PrimaryTarget is null ? AdminMenuCategory.Server : AdminMenuCategory.General);

    public AdminMenuEntry InCategory(string category)
    {
        _category = category;
        return this;
    }

    public AdminMenuEntry Usage(string usageKey)
    {
        UsageKey = usageKey;
        return this;
    }

    /// <summary>
    /// Keeps the entry's identity when the command is renamed: pass the old command name so saved favorites still match.
    /// </summary>
    public AdminMenuEntry Id(string menuId)
    {
        MenuId = menuId;
        return this;
    }

    public AdminMenuEntry Named(AdminMenuText name)
    {
        Name = name;
        return this;
    }

    public AdminMenuEntry Described(AdminMenuText description)
    {
        Description = description;
        return this;
    }

    public int PrimaryTargetIndex => _arguments.FindIndex(a => a is TargetArgument);

    public TargetArgument? PrimaryTarget => PrimaryTargetIndex >= 0 ? (TargetArgument)_arguments[PrimaryTargetIndex] : null;

    public AdminMenuEntry Target(bool allowSelectors = true, string titleKey = "AdminMenu.Step.Target")
        => Argument(new TargetArgument(AdminMenuText.FromKey(titleKey), allowSelectors));

    public AdminMenuEntry Preset(string presetKey, string titleKey = "AdminMenu.Step.Value")
        => Argument(new PresetArgument(AdminMenuText.FromKey(titleKey), presetKey));

    public AdminMenuEntry Choice(string titleKey, params AdminMenuChoice[] choices)
        => Argument(new ChoiceArgument(AdminMenuText.FromKey(titleKey), choices));

    public AdminMenuEntry Choice(string titleKey, params string[] values)
        => Choice(titleKey, values.Select(v => new AdminMenuChoice(v)).ToArray());

    public AdminMenuEntry Toggle(string titleKey = "AdminMenu.Step.State")
        => Choice(titleKey, ToggleChoices.ToArray());

    public AdminMenuEntry TeamChoice()
        => Choice("AdminMenu.Step.Team", TeamChoices.ToArray());

    public AdminMenuEntry Text(string titleKey = "AdminMenu.Step.Text", bool quote = false)
        => Argument(new TextArgument(AdminMenuText.FromKey(titleKey), quote));

    public AdminMenuEntry TextList(string titleKey, int minCount)
        => Argument(new TextListArgument(AdminMenuText.FromKey(titleKey), minCount));

    public static readonly IReadOnlyList<AdminMenuChoice> ToggleChoices =
    [
        new("1", "AdminMenu.Choice.On"),
        new("0", "AdminMenu.Choice.Off"),
    ];

    public static readonly IReadOnlyList<AdminMenuChoice> TeamChoices =
    [
        new("ct", "AdminMenu.Choice.Team.Ct"),
        new("t", "AdminMenu.Choice.Team.T"),
        new("spec", "AdminMenu.Choice.Team.Spec"),
    ];

    /// <summary>
    /// Marks the last added argument as optional.
    /// </summary>
    /// <param name="defaultRaw">Value written when a later argument is filled; needed unless this is the last argument</param>
    public AdminMenuEntry Optional(string? defaultRaw = null)
    {
        _arguments[^1] = _arguments[^1] with { IsOptional = true, DefaultRaw = defaultRaw };
        return this;
    }

    public AdminMenuEntry Argument(AdminMenuArgument argument)
    {
        _arguments.Add(argument);
        return this;
    }
}
