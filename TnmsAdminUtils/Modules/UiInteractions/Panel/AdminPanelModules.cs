using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using Sharp.Shared;

namespace TnmsAdminUtils.Modules.UiInteractions.Panel;

/// <param name="Name">Assembly file name, as <c>ms modules</c> lists it</param>
/// <param name="DisplayName">The module's DisplayName, or null when unknown</param>
/// <param name="Author">The module's DisplayAuthor, or null when unknown</param>
/// <param name="State">ModSharp's load state (Running, Failed, ...), or null when unknown</param>
/// <param name="Type">The IModSharpModule implementation while the module is loaded. Do not keep it (it pins the
/// module's load context)</param>
public sealed record AdminPanelModule(string Name, string? DisplayName, string? Author, string? State, Type? Type)
{
    public Assembly? Assembly => Type?.Assembly;

    /// <summary>Major.Minor.Build, like <c>ms modules</c>.</summary>
    public string Version => Assembly?.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "-";
}

/// <summary>
/// The loaded ModSharp modules for the Dev page. There is no API listing them: the list is read from ModSharp's module
/// manager by reflection (what <c>ms modules</c> prints), and when its internals change, from the load contexts
/// (every module has its own, holding the assembly with the IModSharpModule implementation).
/// </summary>
public static class AdminPanelModules
{
    // The IModSharpModule implementation of each load context, found once per load (a reload is a new context).
    private static readonly ConditionalWeakTable<AssemblyLoadContext, Type?> ModuleTypes = new();

    public static IReadOnlyList<AdminPanelModule> List(ISharedSystem shared)
        => (FromModuleManager(shared) ?? FromLoadContexts())
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// SharpModuleManager._modules (List of ModSharpModule: Name, DisplayName, DisplayAuthor, State, Instance).
    /// Null when that shape is not there.
    /// </summary>
    private static List<AdminPanelModule>? FromModuleManager(ISharedSystem shared)
    {
        try
        {
            var manager = shared.GetSharpModuleManager();

            if (manager.GetType().GetField("_modules", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(manager) is not IEnumerable entries)
                return null;

            List<AdminPanelModule> modules = [];

            foreach (var entry in entries)
            {
                var type = entry.GetType();
                object? Get(string property) => type.GetProperty(property)?.GetValue(entry);

                if (Get("Name") is not string name)
                    return null;

                var instance = Get("Instance") as IModSharpModule;
                modules.Add(new AdminPanelModule(name, Get("DisplayName") as string, Get("DisplayAuthor") as string, Get("State")?.ToString(), instance?.GetType()));
            }

            return modules;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<AdminPanelModule> FromLoadContexts()
    {
        List<AdminPanelModule> modules = [];

        foreach (var context in AssemblyLoadContext.All)
        {
            if (context == AssemblyLoadContext.Default || ModuleTypes.GetValue(context, FindModuleType) is not { } type)
                continue;

            var author = type.Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
            modules.Add(new AdminPanelModule(type.Assembly.GetName().Name ?? "?", null, author, null, type));
        }

        return modules;
    }

    private static Type? FindModuleType(AssemblyLoadContext context)
    {
        foreach (var assembly in context.Assemblies)
        {
            try
            {
                if (assembly.GetTypes().FirstOrDefault(t => !t.IsAbstract && typeof(IModSharpModule).IsAssignableFrom(t)) is { } type)
                    return type;
            }
            catch (ReflectionTypeLoadException)
            {
                // A library with a missing dependency; not a module.
            }
        }

        return null;
    }

    /// <summary>
    /// Rows of the module's detail: label key and value.
    /// </summary>
    public static IEnumerable<(string LabelKey, string Value)> Info(AdminPanelModule module)
    {
        var assembly = module.Assembly;

        string Attribute<T>(Func<T, string?> value) where T : Attribute
            => assembly?.GetCustomAttribute<T>() is { } attribute && value(attribute) is { Length: > 0 } text ? text : "-";

        yield return ("AdminPanel.Dev.Module.Name", module.Name);
        yield return ("AdminPanel.Dev.Module.DisplayName", module.DisplayName ?? "-");
        yield return ("AdminPanel.Dev.Module.Author", module.Author ?? "-");
        yield return ("AdminPanel.Dev.Module.State", module.State ?? "-");
        yield return ("AdminPanel.Dev.Module.Version", assembly is null ? "-" : AdminPanelDevInfo.AssemblyVersion(assembly));

        yield return ("AdminPanel.Dev.Module.Class", module.Type?.FullName ?? "-");

        yield return ("AdminPanel.Dev.Module.Framework", Attribute<TargetFrameworkAttribute>(a => a.FrameworkDisplayName is { Length: > 0 } name ? name : a.FrameworkName));
        yield return ("AdminPanel.Dev.Module.Configuration", Attribute<AssemblyConfigurationAttribute>(a => a.Configuration));
        yield return ("AdminPanel.Dev.Module.Description", Attribute<AssemblyDescriptionAttribute>(a => a.Description));

        yield return ("AdminPanel.Dev.Module.Repository", assembly?.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RepositoryUrl")?.Value is { Length: > 0 } url ? url : "-");
    }

    /// <summary>
    /// The other assemblies of the module's load context (its own dependencies; shared ones live in the default
    /// context), by name.
    /// </summary>
    public static IReadOnlyList<(string Name, string Version)> Dependencies(AdminPanelModule module)
    {
        if (module.Assembly is not { } assembly || AssemblyLoadContext.GetLoadContext(assembly) is not { } context)
            return [];

        return context.Assemblies
            .Where(a => a != assembly)
            .Select(a => a.GetName())
            .Select(n => (n.Name ?? "?", n.Version?.ToString() ?? "-"))
            .OrderBy(d => d.Item1, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
