using System.Reflection;
using Granit.Notifications;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that every concrete <see cref="NotificationType{TData}"/> whose
/// <c>DefaultChannels</c> include a text-rendered channel (currently <see cref="NotificationChannels.Email"/>)
/// ships an embedded <c>Templates/{Name}.html</c> resource in the same assembly.
/// <para>
/// The check validates the deliverable directly (HTML present in the DLL) rather than
/// a proxy such as the existence of a per-package <c>EmbeddedTemplatesTests</c> class.
/// NetArchTest does not reliably see test assemblies, and forcing a per-package test
/// class is fragile — scanning every <c>Granit.*.Notifications</c> runtime assembly
/// catches a missing template the moment the build artefact is produced.
/// </para>
/// </summary>
public sealed class NotificationTemplateCoverageTests
{
    /// <summary>
    /// Channels rendered by the templating pipeline (`Granit.Notifications.Email` &amp; co).
    /// In-app / SignalR / SSE channels are NOT in this set — they don't render HTML.
    /// </summary>
    private static readonly HashSet<string> TextRenderedChannels =
        new(StringComparer.Ordinal)
        {
            NotificationChannels.Email,
        };

    /// <summary>
    /// Discovers, across every loaded <c>Granit.*.Notifications</c> assembly, the
    /// concrete <see cref="NotificationType{TData}"/> subclasses whose
    /// <c>DefaultChannels</c> contain at least one text-rendered channel and emits one
    /// row per (assembly, notification name, expected resource name).
    /// </summary>
    public static TheoryData<string, string, string> NotificationsRequiringTemplate()
    {
        var data = new TheoryData<string, string, string>();

        foreach (Assembly assembly in EnumerateNotificationAssemblies())
        {
            string assemblyName = assembly.GetName().Name!;

            foreach (Type type in SafeGetTypes(assembly))
            {
                if (!IsConcreteNotificationType(type))
                {
                    continue;
                }

                NotificationTypeProbe? probe = TryReadNotificationType(type);
                if (probe is null)
                {
                    continue;
                }

                if (!probe.DefaultChannels.Any(TextRenderedChannels.Contains))
                {
                    continue;
                }

                string expectedResource = $"{assemblyName}.Templates.{probe.Name}.html";
                data.Add(assemblyName, probe.Name, expectedResource);
            }
        }

        return data;
    }

    /// <summary>
    /// For each in-scope <see cref="NotificationType{TData}"/>, asserts that the neutral
    /// (English) HTML template is embedded in the same assembly. The test fails if the
    /// template is missing, renamed, or excluded by an incorrect <c>EmbeddedResource</c>
    /// glob in the <c>.csproj</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(NotificationsRequiringTemplate))]
    public void Notification_with_text_rendered_channel_must_ship_neutral_template(
        string assemblyName,
        string notificationName,
        string expectedResource)
    {
        var assembly = Assembly.Load(new AssemblyName(assemblyName));

        string[] resources = assembly.GetManifestResourceNames();

        resources.ShouldContain(
            expectedResource,
            customMessage:
                $"NotificationType '{notificationName}' in {assemblyName} declares a text-rendered "
                + $"channel (e.g. Email) but no '{expectedResource}' is embedded in the assembly. "
                + "Add 'Templates/{Name}.html' to the package, ensure the .csproj has "
                + "<EmbeddedResource Include=\"Templates\\**\\*.html\" WithCulture=\"false\" />, "
                + "or remove the text-rendered channel from DefaultChannels.");
    }

    /// <summary>
    /// Guard rail: at least one notification type must surface in the theory data so a
    /// silent reflection regression (e.g. base type renamed) is detected. We assert a
    /// realistic floor, not the exact count, so adding a new notification type doesn't
    /// require touching this test.
    /// </summary>
    [Fact]
    public void Theory_data_should_discover_at_least_one_notification_type()
    {
        TheoryData<string, string, string> data = NotificationsRequiringTemplate();
        data.Count.ShouldBeGreaterThan(
            0,
            "Reflection failed to discover any NotificationType<> subclass with a text-rendered "
            + "channel. Either no Granit.*.Notifications assembly was loaded, or NotificationType<> "
            + "was renamed.");
    }

    /// <summary>
    /// Enumerates every <c>Granit.*.Notifications</c> assembly available next to the test
    /// project's output. We scan <see cref="Assembly.Location"/>'s directory rather than
    /// <c>GetReferencedAssemblies()</c> because the C# compiler strips
    /// <c>ProjectReference</c>s that aren't used in code — every <c>*.Notifications</c>
    /// package would be invisible otherwise. The <c>.csproj</c> declares a
    /// <c>ProjectReference</c> to every framework package (one of the responsibilities of
    /// <c>Granit.ArchitectureTests</c>), so the compiled DLLs are guaranteed to land here.
    /// </summary>
    private static IEnumerable<Assembly> EnumerateNotificationAssemblies()
    {
        string? directory = Path.GetDirectoryName(
            typeof(NotificationTemplateCoverageTests).Assembly.Location);
        if (string.IsNullOrEmpty(directory))
        {
            yield break;
        }

        foreach (string dllPath in Directory.EnumerateFiles(
                     directory,
                     "Granit.*.Notifications.dll",
                     SearchOption.TopDirectoryOnly))
        {
            string simpleName = Path.GetFileNameWithoutExtension(dllPath);

            // Skip the umbrella `Granit.Notifications` package — it doesn't ship
            // notification types, only the dispatch infrastructure. (Filename glob
            // already excludes it, but be explicit for sub-packages like
            // `Granit.Notifications.Email` that aren't notification catalogues.)
            if (simpleName == "Granit.Notifications"
                || !simpleName.EndsWith(".Notifications", StringComparison.Ordinal))
            {
                continue;
            }

            Assembly? loaded = TryLoad(simpleName);
            if (loaded is not null)
            {
                yield return loaded;
            }
        }
    }

    private static Assembly? TryLoad(string simpleName)
    {
        try
        {
            return Assembly.Load(new AssemblyName(simpleName));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static t => t is not null)!;
        }
    }

    private static bool IsConcreteNotificationType(Type type)
    {
        if (type is null || type.IsAbstract || type.IsGenericTypeDefinition || type.IsInterface)
        {
            return false;
        }

        Type? current = type.BaseType;
        while (current is not null && current != typeof(object))
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(NotificationType<>))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Reads <c>Name</c> and <c>DefaultChannels</c> from a notification type. Prefers the
    /// conventional <c>public static readonly Instance</c> singleton; falls back to
    /// <see cref="Activator.CreateInstance(Type)"/> when the type exposes a public
    /// parameterless constructor (also conventional). Returns <c>null</c> when neither
    /// approach succeeds — those edge cases are exempted, not a hard failure.
    /// </summary>
    private static NotificationTypeProbe? TryReadNotificationType(Type type)
    {
        object? instance = ReadSingletonInstance(type) ?? ActivateDefault(type);
        if (instance is null)
        {
            return null;
        }

        string? name = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(instance) as string;
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (type.GetProperty("DefaultChannels", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(instance) is not IEnumerable<string> channels)
        {
            return null;
        }

        return new NotificationTypeProbe(name, [.. channels]);
    }

    private static object? ReadSingletonInstance(Type type)
    {
        FieldInfo? field = type.GetField(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        if (field is not null && type.IsAssignableFrom(field.FieldType))
        {
            return field.GetValue(null);
        }

        PropertyInfo? property = type.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        if (property is not null && type.IsAssignableFrom(property.PropertyType))
        {
            return property.GetValue(null);
        }

        return null;
    }

    private static object? ActivateDefault(Type type)
    {
        try
        {
            return type.GetConstructor(Type.EmptyTypes) is null
                ? null
                : Activator.CreateInstance(type);
        }
        catch (TargetInvocationException)
        {
            return null;
        }
        catch (MemberAccessException)
        {
            return null;
        }
    }

    private sealed record NotificationTypeProbe(string Name, IReadOnlyList<string> DefaultChannels);
}
