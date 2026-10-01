namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// Commands that can be run from the admin menu, in registration order.
/// </summary>
public sealed class AdminMenuRegistry
{
    private readonly List<AdminMenuEntry> _entries = [];

    public IReadOnlyList<AdminMenuEntry> Entries => _entries;

    /// <summary>
    /// Raised after an entry is registered or removed.
    /// </summary>
    public event Action? Changed;

    public void Register(AdminMenuEntry entry)
    {
        _entries.RemoveAll(e => e.CommandName == entry.CommandName);
        _entries.Add(entry);
        Changed?.Invoke();
    }

    public void Unregister(string commandName)
    {
        if (_entries.RemoveAll(e => e.CommandName == commandName) > 0)
            Changed?.Invoke();
    }
}
