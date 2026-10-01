namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// Commands that can be run from the admin menu, in registration order.
/// </summary>
public sealed class AdminMenuRegistry
{
    private readonly List<AdminMenuEntry> _entries = [];

    public IReadOnlyList<AdminMenuEntry> Entries => _entries;

    public void Register(AdminMenuEntry entry)
    {
        _entries.RemoveAll(e => e.CommandName == entry.CommandName);
        _entries.Add(entry);
    }

    public void Unregister(string commandName)
    {
        _entries.RemoveAll(e => e.CommandName == commandName);
    }
}
