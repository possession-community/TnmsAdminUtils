using System.Collections;
using System.Globalization;
using CsToml;
using CsToml.Error;
using Microsoft.Extensions.Logging;

namespace TnmsAdminUtils.Modules.UiInteractions;

/// <summary>
/// Every <c>*.toml</c> under <c>configs/menus/</c> of the module directory, merged:
/// <c>[admin.menu.presets]</c> (values of the built-in commands), <c>[admin.menu.category.&lt;key&gt;]</c> (extra
/// command lists) and <c>[admin.menu.operations.&lt;key&gt;]</c> (extra menu entries).
/// Files are read in path order; a broken file, category or operation is skipped with an error in the log and the
/// rest still loads.
/// </summary>
public sealed class AdminMenuConfig
{
    public static readonly string DirectoryName = Path.Combine("configs", "menus");

    private static readonly Dictionary<string, List<string>> DefaultPresets = new()
    {
        ["slap"] = ["0", "1", "5", "10", "50", "100"],
        ["hp"] = ["1", "50", "100", "200", "500"],
        ["money"] = ["0", "800", "4000", "10000", "16000"],
        ["setkev"] = ["0", "50", "100"],
        ["drop"] = ["0", "1", "2", "3", "4"],
        ["gravity"] = ["0.25", "0.5", "1", "2"],
        ["speed"] = ["0.5", "1", "1.5", "2", "3"],
        ["freeze"] = ["3", "5", "10", "30"],
        ["blind"] = ["3", "5", "10"],
        ["shake"] = ["3", "5", "10"],
        ["give"] = ["ak47", "m4a1_silencer", "m4a1", "awp", "ssg08", "p90", "deagle", "usp_silencer", "glock", "knife", "hegrenade", "flashbang", "smokegrenade", "molotov", "healthshot"],
        ["addtime"] = ["60", "300", "-60", "-300"],
        ["settime"] = ["60", "180", "300", "600"],
        ["terminateround"] = ["0", "3", "5", "10"],
        ["toast"] = ["3s", "5s", "10s", "20s"],
    };

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Presets { get; private init; } = new Dictionary<string, IReadOnlyList<string>>();
    public IReadOnlyList<AdminMenuCategoryDefinition> Categories { get; private init; } = [];
    public IReadOnlyList<AdminMenuEntry> Operations { get; private init; } = [];

    public IReadOnlyList<string> GetPreset(string key)
    {
        if (Presets.TryGetValue(key, out var values) && values.Count > 0)
            return values;

        return DefaultPresets.TryGetValue(key, out var defaults) ? defaults : [];
    }

    /// <param name="isCodeEntry">True for a menu id taken by a command of the plugin; operations may not replace one</param>
    public static AdminMenuConfig Load(string moduleDirectory, Func<string, bool> isCodeEntry, ILogger logger)
    {
        if (File.Exists(Path.Combine(moduleDirectory, "menu.json")))
            logger.LogWarning("menu.json is no longer read. Move its presets to [admin.menu.presets] in {Directory}", DirectoryName);

        var directory = Path.Combine(moduleDirectory, DirectoryName);

        if (!Directory.Exists(directory))
        {
            logger.LogInformation("{Directory} does not exist, using the default presets", DirectoryName);
            return new AdminMenuConfig();
        }

        var files = Directory.GetFiles(directory, "*.toml", SearchOption.AllDirectories)
            .Select(path => (Path: path, Name: Path.GetRelativePath(directory, path)))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        var menus = new List<(MenuReader Reader, IDictionary Menu)>();

        foreach (var (path, name) in files)
        {
            var reader = new MenuReader(name, logger);

            if (reader.ReadMenu(path) is { } menu)
                menus.Add((reader, menu));
        }

        // Presets and categories of every file first, so an operation can use a category of another file.
        var presets = new Dictionary<string, IReadOnlyList<string>>();
        var categories = new List<AdminMenuCategoryDefinition>();

        foreach (var (reader, menu) in menus)
        {
            reader.ReadPresets(menu, presets);
            reader.ReadCategories(menu, categories);
        }

        var categoryKeys = categories.Select(c => c.Key).Concat(AdminMenuCategory.BuiltIn.Select(c => c.Key)).ToHashSet();
        var operations = new List<AdminMenuEntry>();

        foreach (var (reader, menu) in menus)
            reader.ReadOperations(menu, categoryKeys, isCodeEntry, operations);

        logger.LogInformation("Loaded {Files} menu file(s): {Categories} categories, {Operations} operations", menus.Count, categories.Count, operations.Count);

        return new AdminMenuConfig { Presets = presets, Categories = categories, Operations = operations };
    }

    /// <summary>
    /// Reads one file. CsToml turns the document into nested dictionaries (tables) and object arrays, which are
    /// checked here key by key so that one mistake costs only the table it is in.
    /// </summary>
    private sealed class MenuReader(string file, ILogger logger)
    {
        private static readonly string[] MenuKeys = ["presets", "category", "operations"];
        private static readonly string[] CategoryKeys = ["Name", "NameKey", "RequiredPermission", "Icon", "IconSize"];
        private static readonly string[] OperationKeys = ["Category", "Command", "Name", "NameKey", "Description", "DescriptionKey", "RequiredPermission", "args"];
        private static readonly string[] ArgumentKeys = ["Type", "Name", "NameKey", "Optional", "Default", "AllowSelectors", "SuggestValues", "AllowCustomInput", "Min", "Max", "Quote", "MinCount", "Choices"];
        private static readonly string[] ChoiceKeys = ["Value", "Name", "NameKey"];

        public IDictionary? ReadMenu(string path)
        {
            IDictionary root;

            try
            {
                var document = CsTomlSerializer.Deserialize<TomlDocument>(File.ReadAllBytes(path));
                root = (IDictionary)document.ToDictionary<object, object>();
            }
            catch (CsTomlSerializeException e)
            {
                Error("(file)", string.Join("; ", e.ParseExceptions?.Select(p => $"line {p.LineNumber}: {p.InnerException?.Message ?? p.Message}") ?? [e.Message]));
                return null;
            }
            catch (Exception e)
            {
                Error("(file)", e.Message);
                return null;
            }

            if (root["admin"] is not IDictionary admin || admin["menu"] is not IDictionary menu)
            {
                logger.LogWarning("{File}: no [admin.menu] tables, nothing to read", file);
                return null;
            }

            WarnUnknownKeys("admin.menu", menu, MenuKeys);
            return menu;
        }

        public void ReadPresets(IDictionary menu, Dictionary<string, IReadOnlyList<string>> presets)
        {
            if (!TryTable(menu, "presets", "admin.menu.presets", out var table))
                return;

            foreach (DictionaryEntry item in table)
            {
                var key = (string)item.Key;
                var where = $"admin.menu.presets.{key}";

                if (presets.ContainsKey(key))
                {
                    Error(where, "already defined in an earlier file");
                    continue;
                }

                if (TryValues(item.Value, where, out var values))
                    presets[key] = values;
            }
        }

        public void ReadCategories(IDictionary menu, List<AdminMenuCategoryDefinition> categories)
        {
            if (!TryTable(menu, "category", "admin.menu.category", out var table))
                return;

            foreach (DictionaryEntry item in table)
            {
                var key = (string)item.Key;
                var where = $"admin.menu.category.{key}";

                if (item.Value is not IDictionary category)
                {
                    Error(where, "must be a table");
                    continue;
                }

                if (AdminMenuCategory.IsBuiltIn(key))
                {
                    Error(where, "is a built-in category and cannot be redefined");
                    continue;
                }

                if (categories.Any(c => c.Key == key))
                {
                    Error(where, "already defined in an earlier file");
                    continue;
                }

                WarnUnknownKeys(where, category, CategoryKeys);

                if (!TryText(category, "Name", where, out var name) || !TryString(category, "RequiredPermission", where, out var permission)
                    || !TryString(category, "Icon", where, out var icon) || !TryString(category, "IconSize", where, out var iconSize))
                    continue;

                if (iconSize is not (null or "s" or "m" or "l"))
                {
                    Error(where, $"IconSize must be \"s\", \"m\" or \"l\" (got \"{iconSize}\")");
                    continue;
                }

                categories.Add(new AdminMenuCategoryDefinition(key, name ?? AdminMenuText.FromInline(new Dictionary<string, string> { ["en"] = key }), permission,
                    string.IsNullOrWhiteSpace(icon) ? null : icon.Trim(), iconSize));
            }
        }

        public void ReadOperations(IDictionary menu, HashSet<string> categoryKeys, Func<string, bool> isCodeEntry, List<AdminMenuEntry> operations)
        {
            if (!TryTable(menu, "operations", "admin.menu.operations", out var table))
                return;

            foreach (DictionaryEntry item in table)
            {
                var key = (string)item.Key;
                var where = $"admin.menu.operations.{key}";

                if (item.Value is not IDictionary operation)
                {
                    Error(where, "must be a table");
                    continue;
                }

                if (isCodeEntry(key))
                {
                    Error(where, "is the id of a built-in command and cannot be replaced");
                    continue;
                }

                if (operations.Any(o => o.MenuId == key))
                {
                    Error(where, "already defined in an earlier file");
                    continue;
                }

                WarnUnknownKeys(where, operation, OperationKeys);

                if (ReadOperation(key, operation, where, categoryKeys) is { } entry)
                    operations.Add(entry);
            }
        }

        private AdminMenuEntry? ReadOperation(string key, IDictionary operation, string where, HashSet<string> categoryKeys)
        {
            if (!TryString(operation, "Category", where, out var category) || !TryString(operation, "Command", where, out var command)
                || !TryString(operation, "RequiredPermission", where, out var permission)
                || !TryText(operation, "Name", where, out var name) || !TryText(operation, "Description", where, out var description))
                return null;

            if (category is null)
                return Fail<AdminMenuEntry>(where, "Category is required");

            if (!categoryKeys.Contains(category))
                return Fail<AdminMenuEntry>(where, $"Category \"{category}\" is not defined");

            if (string.IsNullOrWhiteSpace(command) || command.Any(char.IsWhiteSpace) || command.StartsWith('!'))
                return Fail<AdminMenuEntry>(where, "Command is required: the command name without ms_ or !, e.g. \"slay\"");

            // Without a permission the operation would be open to every admin who can open the menu.
            if (string.IsNullOrWhiteSpace(permission))
                return Fail<AdminMenuEntry>(where, "RequiredPermission is required");

            var entry = AdminMenuEntry.Create(command, permission).Id(key).InCategory(category);

            if (name != null)
                entry.Named(name);

            if (description != null)
                entry.Described(description);

            if (operation["args"] is { } args)
            {
                if (args is not object[] list || list.Any(a => a is not IDictionary))
                    return Fail<AdminMenuEntry>(where, "args must be an array of tables ([[...args]])");

                if (list.Length > AdminMenuEntry.MaxArguments)
                    return Fail<AdminMenuEntry>(where, $"at most {AdminMenuEntry.MaxArguments} args");

                for (var i = 0; i < list.Length; i++)
                {
                    if (ReadArgument((IDictionary)list[i], $"{where}.args[{i}]") is not { } argument)
                        return null;

                    entry.Argument(argument);
                }
            }

            // A skipped argument before a filled one is written as its Default, so it needs one.
            for (var i = 0; i < entry.Arguments.Count - 1; i++)
            {
                if (entry.Arguments[i] is { IsOptional: true, DefaultRaw: null })
                    return Fail<AdminMenuEntry>($"{where}.args[{i}]", "an optional argument needs a Default unless it is the last one");
            }

            return entry;
        }

        private AdminMenuArgument? ReadArgument(IDictionary table, string where)
        {
            WarnUnknownKeys(where, table, ArgumentKeys);

            if (!TryString(table, "Type", where, out var type) || !TryText(table, "Name", where, out var title)
                || !TryBool(table, "Optional", where, out var optional) || !TryScalar(table, "Default", where, out var defaultRaw))
                return null;

            AdminMenuText Title(string stepKey) => title ?? AdminMenuText.FromKey(stepKey);

            AdminMenuArgument? argument;

            var typeName = type?.ToLowerInvariant();

            switch (typeName)
            {
                case "target":
                    if (!TryBool(table, "AllowSelectors", where, out var allowSelectors))
                        return null;

                    argument = new TargetArgument(Title("AdminMenu.Step.Target"), allowSelectors ?? true);
                    break;

                case "int":
                case "float":
                case "string":
                    argument = ReadValue(table, where, typeName switch
                    {
                        "int" => AdminMenuValueKind.Int,
                        "float" => AdminMenuValueKind.Float,
                        _ => AdminMenuValueKind.String,
                    }, Title);
                    break;

                case "string_list":
                    if (!TryNumber(table, "MinCount", where, out var minCount))
                        return null;

                    if (minCount is < 1 || minCount % 1 != 0)
                        return Fail<AdminMenuArgument>(where, "MinCount must be a whole number of 1 or more");

                    argument = new TextListArgument(Title("AdminMenu.Step.Text"), (int)(minCount ?? 1));
                    break;

                case "bool":
                    argument = new ChoiceArgument(Title("AdminMenu.Step.State"), AdminMenuEntry.ToggleChoices);
                    break;

                case "team":
                    argument = new ChoiceArgument(Title("AdminMenu.Step.Team"), AdminMenuEntry.TeamChoices);
                    break;

                case "choice":
                    argument = ReadChoice(table, where, Title("AdminMenu.Step.Value"));
                    break;

                default:
                    return Fail<AdminMenuArgument>(where, $"Type must be one of target, int, float, string, string_list, bool, team, choice (got \"{type}\")");
            }

            return argument is null ? null : argument with { IsOptional = optional ?? false, DefaultRaw = defaultRaw };
        }

        private ValueArgument? ReadValue(IDictionary table, string where, AdminMenuValueKind kind, Func<string, AdminMenuText> title)
        {
            if (!TryBool(table, "AllowCustomInput", where, out var allowCustomInput) || !TryNumber(table, "Min", where, out var min)
                || !TryNumber(table, "Max", where, out var max) || !TryBool(table, "Quote", where, out var quote))
                return null;

            IReadOnlyList<string> suggestions = [];

            if (table["SuggestValues"] is { } raw && !TryValues(raw, $"{where}.SuggestValues", out suggestions))
                return null;

            if (suggestions.Count == 0 && allowCustomInput == false)
                return Fail<ValueArgument>(where, "nothing to choose: SuggestValues is empty and AllowCustomInput is false");

            var argument = new ValueArgument(title(kind == AdminMenuValueKind.String ? "AdminMenu.Step.Text" : "AdminMenu.Step.Value"), kind, suggestions, allowCustomInput ?? true)
            {
                Min = min,
                Max = max,
                Quote = quote ?? false,
            };

            if (kind != AdminMenuValueKind.String && suggestions.FirstOrDefault(s => argument.Check(s) != null) is { } bad)
                return Fail<ValueArgument>(where, $"SuggestValues \"{bad}\" is not a valid {kind.ToString().ToLowerInvariant()} in range");

            return argument;
        }

        private ChoiceArgument? ReadChoice(IDictionary table, string where, AdminMenuText title)
        {
            if (table["Choices"] is not object[] { Length: > 0 } items || items.Any(i => i is not IDictionary))
                return Fail<ChoiceArgument>(where, "Choices must be a non-empty array of tables: [{ Value = \"...\", Name = { en = \"...\" } }]");

            var choices = new List<AdminMenuChoice>();

            for (var i = 0; i < items.Length; i++)
            {
                var item = (IDictionary)items[i];
                var itemWhere = $"{where}.Choices[{i}]";

                WarnUnknownKeys(itemWhere, item, ChoiceKeys);

                if (!TryScalar(item, "Value", itemWhere, out var value) || !TryText(item, "Name", itemWhere, out var label))
                    return null;

                if (value is null)
                    return Fail<ChoiceArgument>(itemWhere, "Value is required");

                choices.Add(new AdminMenuChoice(value, label));
            }

            return new ChoiceArgument(title, choices);
        }

        private bool TryTable(IDictionary parent, string key, string where, out IDictionary table)
        {
            table = null!;

            switch (parent[key])
            {
                case null:
                    return false;
                case IDictionary found:
                    table = found;
                    return true;
                default:
                    Error(where, "must be a table");
                    return false;
            }
        }

        /// <summary>
        /// <c>&lt;key&gt; = "text"</c> (every language), <c>&lt;key&gt; = { en = "...", ja = "..." }</c>, or
        /// <c>&lt;key&gt;Key = "lang.key"</c> for a key of TnmsAdminUtils' lang files. Null when none is given.
        /// </summary>
        private bool TryText(IDictionary table, string key, string where, out AdminMenuText? text)
        {
            text = null;
            var inline = table[key];
            var langKey = table[key + "Key"];

            if (inline != null && langKey != null)
            {
                Error(where, $"give either {key} or {key}Key, not both");
                return false;
            }

            switch (inline)
            {
                case string single:
                    text = AdminMenuText.FromInline(new Dictionary<string, string> { ["en"] = single });
                    return true;

                case IDictionary translations:
                    var texts = new Dictionary<string, string>();

                    foreach (DictionaryEntry translation in translations)
                    {
                        if (translation.Value is not string value)
                        {
                            Error(where, $"{key}.{translation.Key} must be a string");
                            return false;
                        }

                        texts[(string)translation.Key] = value;
                    }

                    if (texts.Count == 0)
                    {
                        Error(where, $"{key} has no translations");
                        return false;
                    }

                    text = AdminMenuText.FromInline(texts);
                    return true;

                case null:
                    break;

                default:
                    Error(where, $"{key} must be a string or a table of translations ({{ en = \"...\", ja = \"...\" }})");
                    return false;
            }

            if (langKey is null)
                return true;

            if (langKey is not string keyText || keyText.Length == 0)
            {
                Error(where, $"{key}Key must be a string");
                return false;
            }

            text = AdminMenuText.FromKey(keyText);
            return true;
        }

        private bool TryString(IDictionary table, string key, string where, out string? value)
        {
            value = table[key] as string;

            if (table[key] is null or string)
                return true;

            Error(where, $"{key} must be a string");
            return false;
        }

        private bool TryBool(IDictionary table, string key, string where, out bool? value)
        {
            value = table[key] as bool?;

            if (table[key] is null or bool)
                return true;

            Error(where, $"{key} must be true or false");
            return false;
        }

        private bool TryNumber(IDictionary table, string key, string where, out double? value)
        {
            value = table[key] switch
            {
                long l => l,
                double d => d,
                _ => null,
            };

            if (table[key] is null or long or double)
                return true;

            Error(where, $"{key} must be a number");
            return false;
        }

        /// <summary>
        /// A string, or a number written as one, since everything ends up on a command line.
        /// </summary>
        private bool TryScalar(IDictionary table, string key, string where, out string? value)
        {
            value = ScalarText(table[key]);

            if (table[key] is null || value != null)
                return true;

            Error(where, $"{key} must be a string or a number");
            return false;
        }

        private bool TryValues(object? raw, string where, out IReadOnlyList<string> values)
        {
            values = [];

            if (raw is not object[] items)
            {
                Error(where, "must be an array of strings or numbers");
                return false;
            }

            var texts = items.Select(ScalarText).ToList();

            if (texts.Any(t => t is null))
            {
                Error(where, "must be an array of strings or numbers");
                return false;
            }

            values = texts!;
            return true;
        }

        private static string? ScalarText(object? value) => value switch
        {
            string s => s,
            long l => l.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };

        private void WarnUnknownKeys(string where, IDictionary table, string[] known)
        {
            foreach (var key in table.Keys.Cast<string>().Where(k => !known.Contains(k)))
                logger.LogWarning("{File}: {Where}: unknown key \"{Key}\" is ignored", file, where, key);
        }

        private T? Fail<T>(string where, string message) where T : class
        {
            Error(where, message);
            return null;
        }

        private void Error(string where, string message)
            => logger.LogError("{File}: {Where}: {Message}, skipped", file, where, message);
    }
}
