namespace TnmsAdminUtils.Modules.UiInteractions;

public abstract record AdminMenuArgument(string TitleKey)
{
    /// <summary>
    /// The menu offers "default" for this argument. A skipped trailing argument is left out of the command line.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Written in place of a skipped argument when a later argument has a value.
    /// </summary>
    public string? DefaultRaw { get; init; }
}

/// <summary>
/// A player argument. The first target of an entry is the primary one (the player picked in the Players tree).
/// </summary>
public sealed record TargetArgument(string TitleKey, bool AllowSelectors) : AdminMenuArgument(TitleKey);

/// <summary>
/// Values are read from menu.json by <see cref="PresetKey"/>.
/// </summary>
public sealed record PresetArgument(string TitleKey, string PresetKey) : AdminMenuArgument(TitleKey);

public sealed record ChoiceArgument(string TitleKey, IReadOnlyList<AdminMenuChoice> Choices) : AdminMenuArgument(TitleKey);

/// <summary>
/// Free text typed in chat. Appended to the command line as is, or as one quoted argument when <see cref="Quote"/> is set.
/// </summary>
public sealed record TextArgument(string TitleKey, bool Quote) : AdminMenuArgument(TitleKey);

/// <summary>
/// Comma separated texts typed in chat at once, each passed as a quoted argument.
/// </summary>
public sealed record TextListArgument(string TitleKey, int MinCount) : AdminMenuArgument(TitleKey);

/// <summary>
/// Which command list of the menu / panel an entry belongs to.
/// </summary>
public enum AdminMenuCategory
{
    Normal,
    Server,
    /// <summary>
    /// Chat / screen messages (say, csay, toast, ...).
    /// </summary>
    Notification,
}

/// <param name="Value">Value passed to the command</param>
/// <param name="LabelKey">Translation key of the label, or null to show <see cref="Value"/></param>
public sealed record AdminMenuChoice(string Value, string? LabelKey = null);

/// <summary>
/// Describes how a command is run from the admin menu. Arguments are listed in command line order.
/// </summary>
public sealed class AdminMenuEntry
{
    public string CommandName { get; }
    public string Permission { get; }
    public IReadOnlyList<AdminMenuArgument> Arguments => _arguments;

    /// <summary>
    /// Stable identity of the entry, stored in favorites. Defaults to <see cref="CommandName"/>.
    /// </summary>
    public string MenuId { get; private set; }

    private readonly List<AdminMenuArgument> _arguments = [];

    private AdminMenuEntry(string commandName, string permission)
    {
        CommandName = commandName;
        Permission = permission;
        MenuId = commandName;
    }

    public static AdminMenuEntry Create(string commandName, string permission) => new(commandName, permission);

    /// <summary>
    /// Translation key of the command's usage line, shown while its arguments are being chosen.
    /// </summary>
    public string? UsageKey { get; private set; }

    private AdminMenuCategory? _category;

    /// <summary>
    /// Defaults to <see cref="AdminMenuCategory.Server"/> without a target argument, otherwise <see cref="AdminMenuCategory.Normal"/>.
    /// </summary>
    public AdminMenuCategory Category => _category ?? (PrimaryTarget is null ? AdminMenuCategory.Server : AdminMenuCategory.Normal);

    public AdminMenuEntry InCategory(AdminMenuCategory category)
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

    public int PrimaryTargetIndex => _arguments.FindIndex(a => a is TargetArgument);

    public TargetArgument? PrimaryTarget => PrimaryTargetIndex >= 0 ? (TargetArgument)_arguments[PrimaryTargetIndex] : null;

    public string LabelKey => $"AdminMenu.Command.{CommandName}";

    public AdminMenuEntry Target(bool allowSelectors = true, string titleKey = "AdminMenu.Step.Target")
        => Add(new TargetArgument(titleKey, allowSelectors));

    public AdminMenuEntry Preset(string presetKey, string titleKey = "AdminMenu.Step.Value")
        => Add(new PresetArgument(titleKey, presetKey));

    public AdminMenuEntry Choice(string titleKey, params AdminMenuChoice[] choices)
        => Add(new ChoiceArgument(titleKey, choices));

    public AdminMenuEntry Choice(string titleKey, params string[] values)
        => Choice(titleKey, values.Select(v => new AdminMenuChoice(v)).ToArray());

    public AdminMenuEntry Toggle(string titleKey = "AdminMenu.Step.State")
        => Choice(titleKey, new AdminMenuChoice("1", "AdminMenu.Choice.On"), new AdminMenuChoice("0", "AdminMenu.Choice.Off"));

    public AdminMenuEntry TeamChoice()
        => Choice("AdminMenu.Step.Team",
            new AdminMenuChoice("ct", "AdminMenu.Choice.Team.Ct"),
            new AdminMenuChoice("t", "AdminMenu.Choice.Team.T"),
            new AdminMenuChoice("spec", "AdminMenu.Choice.Team.Spec"));

    public AdminMenuEntry Text(string titleKey = "AdminMenu.Step.Text", bool quote = false)
        => Add(new TextArgument(titleKey, quote));

    public AdminMenuEntry TextList(string titleKey, int minCount)
        => Add(new TextListArgument(titleKey, minCount));

    /// <summary>
    /// Marks the last added argument as optional.
    /// </summary>
    /// <param name="defaultRaw">Value written when a later argument is filled; needed unless this is the last argument</param>
    public AdminMenuEntry Optional(string? defaultRaw = null)
    {
        _arguments[^1] = _arguments[^1] with { IsOptional = true, DefaultRaw = defaultRaw };
        return this;
    }

    private AdminMenuEntry Add(AdminMenuArgument argument)
    {
        _arguments.Add(argument);
        return this;
    }
}
