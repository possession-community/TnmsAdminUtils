using TnmsPluginFoundation;
using Wuling.Abstract.Tianshi.Registry;

namespace TnmsAdminUtils.Modules.UiInteractions.Panel;

/// <param name="Selected">The field has this value now</param>
public sealed record AdminFormChoice(string Label, bool Selected, Action Pick);

/// <summary>
/// The panel's form for one command: the argument values, the field whose choice grid is open and the field waiting
/// for chat text. Fields can be filled in any order; unset optional fields use the command's default.
/// </summary>
public sealed class AdminCommandForm
{
    private readonly AdminCommandContext _context;
    private readonly IPlayerEntry _player;
    private readonly AdminMenuValue?[] _values;
    private long _waitExpiresAt;

    public AdminMenuEntry Entry { get; }

    public IReadOnlyList<AdminMenuValue?> Values => _values;

    /// <summary>
    /// Field whose choice grid is open, or -1.
    /// </summary>
    public int OpenField { get; private set; } = -1;

    public int PickerPage { get; set; }

    /// <summary>
    /// Field waiting for the admin's next chat message, or -1.
    /// </summary>
    public int WaitField { get; private set; } = -1;

    /// <param name="target">Pre-filled primary target (the player picked in the user list)</param>
    public AdminCommandForm(AdminCommandContext context, IPlayerEntry player, AdminMenuEntry entry, AdminMenuValue? target)
    {
        _context = context;
        _player = player;
        Entry = entry;
        _values = new AdminMenuValue?[entry.Arguments.Count];

        if (target != null && entry.PrimaryTargetIndex >= 0)
            _values[entry.PrimaryTargetIndex] = target;
    }

    public bool IsReady => Entry.Arguments.Select((a, i) => a.IsOptional || _values[i] != null).All(set => set);

    /// <summary>
    /// Values for the command line: unset optional fields as their default, unset required ones as null.
    /// </summary>
    public IReadOnlyList<AdminMenuValue?> PreviewValues
        => Entry.Arguments.Select((a, i) => _values[i] ?? (a.IsOptional ? _context.Skipped(a) : null)).ToList();

    /// <summary>
    /// Toggles a field: choice fields open / close their grid, a required text field starts waiting for chat.
    /// </summary>
    public void Click(int index)
    {
        if (index >= _values.Length)
            return;

        var wasWaiting = WaitField == index;
        WaitField = -1;

        if (OpenField == index)
        {
            OpenField = -1;
            return;
        }

        if (Entry.Arguments[index] is TextArgument or TextListArgument && !Entry.Arguments[index].IsOptional)
        {
            OpenField = -1;

            if (!wasWaiting)
                BeginWait(index);

            return;
        }

        OpenField = index;
        PickerPage = 0;
    }

    /// <summary>
    /// Choices of the open field: the default (optional fields), the values, and chat input where it applies.
    /// </summary>
    public List<AdminFormChoice> Choices()
    {
        if (OpenField < 0)
            return [];

        var index = OpenField;
        var argument = Entry.Arguments[index];
        var current = _values[index];
        var choices = new List<AdminFormChoice>();

        if (argument.IsOptional)
            choices.Add(new AdminFormChoice(_context.L("AdminMenu.Skip"), current is null, () => Set(index, null)));

        var values = argument is TargetArgument target ? _context.TargetChoices(target.AllowSelectors) : _context.ValueChoices(argument);

        foreach (var choice in values)
            choices.Add(new AdminFormChoice(choice.Label, current?.Raw == choice.Value.Raw, () => Set(index, choice.Value)));

        if (argument is PresetArgument or TextArgument)
            choices.Add(new AdminFormChoice(_context.L("AdminMenu.TypeInChat"), false, () => BeginWait(index)));

        return choices;
    }

    /// <summary>
    /// Ends a wait that ran out. Returns true when the form changed.
    /// </summary>
    public bool ExpireWait()
    {
        if (WaitField < 0 || Environment.TickCount64 <= _waitExpiresAt)
            return false;

        WaitField = -1;
        return true;
    }

    /// <summary>
    /// Uses a chat message as the value of the waiting field. Returns true when the message was consumed.
    /// </summary>
    public bool TryAcceptText(string message)
    {
        if (WaitField < 0 || ExpireWait())
            return false;

        var text = message.Trim();

        if (text.Length == 0)
            return false;

        // Another plugin's menu may be waiting for its keys.
        if (text.Length == 1 && char.IsDigit(text[0]) && TnmsPlugin.Wuling.Menu.GetActiveMenu(_player) != null)
            return false;

        var index = WaitField;

        if (text.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            WaitField = -1;
            _context.PrintToChat("AdminMenu.Text.Cancelled");
            return true;
        }

        if (!_context.TryParseText(Entry.Arguments[index], text, out var value))
        {
            _waitExpiresAt = Expiry();
            return true;
        }

        WaitField = -1;
        _values[index] = value;
        return true;
    }

    /// <summary>
    /// Every value, with unset optional fields at their default. Only valid when <see cref="IsReady"/>.
    /// </summary>
    public List<AdminMenuValue> FinalValues() => PreviewValues.Select(v => v!).ToList();

    private void Set(int index, AdminMenuValue? value)
    {
        _values[index] = value;
        OpenField = -1;
    }

    private void BeginWait(int index)
    {
        OpenField = -1;
        WaitField = index;
        _waitExpiresAt = Expiry();

        var title = $"{_context.CommandLabel(Entry)}: {_context.L(Entry.Arguments[index].TitleKey)}";
        _context.PrintToChat("AdminMenu.Text.Prompt", title, AdminCommandContext.TextInputTimeoutSeconds);
    }

    private static long Expiry() => Environment.TickCount64 + AdminCommandContext.TextInputTimeoutSeconds * 1000L;
}
