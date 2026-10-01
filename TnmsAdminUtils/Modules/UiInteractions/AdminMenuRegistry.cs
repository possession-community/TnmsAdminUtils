namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// Commands that can be run from the admin menu and the lists they are shown in. Entries registered by code come
/// first in registration order, then the operations of the menu TOMLs, which are replaced as a whole on reload.
/// </summary>
public sealed class AdminMenuRegistry
{
    private readonly List<AdminMenuEntry> _codeEntries = [];
    private IReadOnlyList<AdminMenuEntry> _configEntries = [];
    private IReadOnlyList<AdminMenuEntry>? _entries;

    public IReadOnlyList<AdminMenuEntry> Entries => _entries ??= Merge();

    /// <summary>
    /// The built-in lists, then the ones defined in the menu TOMLs, in file order.
    /// </summary>
    public IReadOnlyList<AdminMenuCategoryDefinition> Categories { get; private set; } = AdminMenuCategory.BuiltIn;

    /// <summary>
    /// Raised after an entry is registered or removed, and after the menu TOMLs are applied.
    /// </summary>
    public event Action? Changed;

    public void Register(AdminMenuEntry entry)
    {
        _codeEntries.RemoveAll(e => e.MenuId == entry.MenuId);
        _codeEntries.Add(entry);
        OnChanged();
    }

    public void Unregister(string commandName)
    {
        if (_codeEntries.RemoveAll(e => e.CommandName == commandName) > 0)
            OnChanged();
    }

    public bool IsCodeEntry(string menuId) => _codeEntries.Any(e => e.MenuId == menuId);

    public void SetConfig(IReadOnlyList<AdminMenuCategoryDefinition> categories, IReadOnlyList<AdminMenuEntry> entries)
    {
        _configEntries = entries;
        Categories = [..AdminMenuCategory.BuiltIn, ..categories];
        OnChanged();
    }

    // The loader already refuses operations named like an entry of code; this only covers code registered later.
    private List<AdminMenuEntry> Merge()
        => [.._codeEntries, .._configEntries.Where(e => !IsCodeEntry(e.MenuId))];

    private void OnChanged()
    {
        _entries = null;
        Changed?.Invoke();
    }
}
