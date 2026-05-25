using System.Reflection;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Every Granit assembly that ships at least one public <see cref="NotificationType{TData}"/>
/// subclass must also register an <see cref="INotificationDefinitionProvider"/> that emits a
/// matching <see cref="NotificationDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// The notifications fan-out handler reads its routing decisions
/// (channels, opt-out posture, DnD bypass) exclusively from
/// <see cref="INotificationDefinitionStore"/>. Without a registered definition,
/// fanout silently collapses to InApp-only and treats every type as opt-out-able —
/// breaking the non-opt-out posture intended for security/GDPR alerts. This test
/// closes the gap that #2219 patched by hand across nine modules.
/// </para>
/// <para>
/// Scope is intentionally limited to <em>public</em> non-nested subclasses with a
/// singleton <c>Instance</c> field. Dynamic / private-nested types
/// (e.g. <c>EntityStateChangedNotificationType</c> in
/// <c>Granit.Notifications.EntityFrameworkCore</c>) are parameterised at runtime
/// and not registered up-front.
/// </para>
/// </remarks>
public sealed class NotificationDefinitionProviderCoverageTests
{
    [Fact]
    public void Every_assembly_declaring_notification_types_registers_a_matching_provider()
    {
        List<Assembly> assemblies = LoadGranitAssemblies();
        Type notificationTypeBase = typeof(NotificationType<>);

        List<string> failures = [];

        foreach (Assembly assembly in assemblies)
        {
            string[] declaredNames = EnumerateDeclaredNotificationTypeNames(assembly, notificationTypeBase);
            if (declaredNames.Length == 0)
            {
                continue;
            }

            HashSet<string> registeredNames = CollectRegisteredDefinitionNames(assembly);

            string[] missing = declaredNames
                .Where(n => !registeredNames.Contains(n))
                .ToArray();

            if (missing.Length > 0)
            {
                failures.Add(
                    $"Assembly '{assembly.GetName().Name}' declares {missing.Length} NotificationType(s) " +
                    $"without a matching NotificationDefinition: [{string.Join(", ", missing)}]. " +
                    $"Add an INotificationDefinitionProvider in the assembly's Internal/ folder and register it " +
                    $"via context.Services.AddSingleton<INotificationDefinitionProvider, …>() — pattern: " +
                    $"IdentityNotificationDefinitionProvider in Granit.Identity.Local.Notifications.");
            }
        }

        failures.ShouldBeEmpty();
    }

    private static string[] EnumerateDeclaredNotificationTypeNames(Assembly assembly, Type notificationTypeBase)
    {
        List<string> names = [];

        foreach (Type type in SafeGetTypes(assembly))
        {
            if (!type.IsClass || type.IsAbstract || type.IsNested || !type.IsPublic)
            {
                continue;
            }

            if (!InheritsFromOpenGeneric(type, notificationTypeBase))
            {
                continue;
            }

            FieldInfo? instanceField = type.GetField(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);

            if (instanceField is null)
            {
                continue;
            }

            if (instanceField.GetValue(null) is not object instance)
            {
                continue;
            }

            PropertyInfo? nameProp = type.GetProperty(
                "Name",
                BindingFlags.Public | BindingFlags.Instance);

            if (nameProp?.GetValue(instance) is string n && n.Length > 0)
            {
                names.Add(n);
            }
        }

        return [.. names];
    }

    private static HashSet<string> CollectRegisteredDefinitionNames(Assembly assembly)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        Type providerInterface = typeof(INotificationDefinitionProvider);

        foreach (Type type in SafeGetTypes(assembly))
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
            {
                continue;
            }

            if (!providerInterface.IsAssignableFrom(type))
            {
                continue;
            }

            ConstructorInfo? ctor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                Type.EmptyTypes);

            if (ctor is null)
            {
                continue;
            }

            var provider = (INotificationDefinitionProvider)ctor.Invoke(null);
            CollectingContext ctx = new();
            provider.Define(ctx);
            foreach (NotificationDefinition def in ctx.Definitions)
            {
                names.Add(def.Name);
            }
        }

        return names;
    }

    private static List<Assembly> LoadGranitAssemblies()
    {
        string outputDir = Path.GetDirectoryName(
            typeof(NotificationDefinitionProviderCoverageTests).Assembly.Location)!;

        List<Assembly> loaded = [];
        foreach (string path in Directory.GetFiles(outputDir, "Granit.*.dll"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.Contains("Tests", StringComparison.Ordinal)
                || name.EndsWith(".resources", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                loaded.Add(Assembly.LoadFrom(path));
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
            {
                // Skip non-managed or unloadable DLLs (matches the pattern used in EntityDefinitionScan).
            }
        }

        return loaded;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }

    private static bool InheritsFromOpenGeneric(Type candidate, Type openGenericBase)
    {
        Type? cursor = candidate.BaseType;
        while (cursor is not null && cursor != typeof(object))
        {
            if (cursor.IsGenericType && cursor.GetGenericTypeDefinition() == openGenericBase)
            {
                return true;
            }
            cursor = cursor.BaseType;
        }
        return false;
    }

    private sealed class CollectingContext : INotificationDefinitionContext
    {
        public List<NotificationDefinition> Definitions { get; } = [];

        public void Add(NotificationDefinition definition) =>
            Definitions.Add(definition);
    }
}
