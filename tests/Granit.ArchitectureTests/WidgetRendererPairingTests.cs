using System.Reflection;
using Granit.Dashboards;
using Granit.Dashboards.Rendering;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces ADR-039 §7: every concrete <see cref="WidgetDefinition"/> shipped
/// by the framework MUST have a matching <see cref="IWidgetInstanceRenderer"/>
/// in the same build. Adding a new widget kind without a renderer would let
/// the dashboard render endpoint surface every instance of it as
/// <see cref="WidgetSnapshotStatus.Error"/> at runtime — caught at build time
/// here instead.
/// </summary>
/// <remarks>
/// <para>
/// The pairing rule uses the naming convention:
/// <c>{Prefix}WidgetDefinition</c> ↔ <c>{Prefix}WidgetInstanceRenderer</c>.
/// All eight framework-shipped kinds (Kpi / Chart / Table / Pivot / Map +
/// Markdown / Image / Text) follow it; the convention is now load-bearing.
/// </para>
/// <para>
/// The check looks at TYPES present in the loaded framework assemblies, not
/// at DI registrations. A renderer type that exists but is not registered
/// would still pass this rule — DI registration is verified by the existing
/// renderer pipeline tests (e.g. <c>DashboardRendererTests</c>). The two
/// checks are complementary: this rule catches "forgot to write the
/// renderer", DI tests catch "wrote the renderer but forgot to register it".
/// </para>
/// </remarks>
public sealed class WidgetRendererPairingTests
{
    private const string DefinitionSuffix = "WidgetDefinition";
    private const string RendererSuffix = "WidgetInstanceRenderer";

    [Fact]
    public void Every_WidgetDefinition_should_have_a_matching_WidgetInstanceRenderer()
    {
        Assembly[] assemblies = LoadFrameworkAssemblies();

        IReadOnlyList<Type> definitions = ScanConcreteSubtypes<WidgetDefinition>(assemblies)
            .Where(t => t.Name.EndsWith(DefinitionSuffix, StringComparison.Ordinal))
            .ToList();

        var rendererPrefixes = ScanConcreteSubtypes<IWidgetInstanceRenderer>(assemblies)
            .Where(t => t.Name.EndsWith(RendererSuffix, StringComparison.Ordinal))
            .Select(t => t.Name[..^RendererSuffix.Length])
            .ToHashSet(StringComparer.Ordinal);

        List<string> missing = [];
        foreach (Type definition in definitions)
        {
            string prefix = definition.Name[..^DefinitionSuffix.Length];
            if (!rendererPrefixes.Contains(prefix))
            {
                missing.Add($"{definition.FullName} has no matching {prefix}{RendererSuffix}");
            }
        }

        missing.ShouldBeEmpty(
            $"ADR-039 §7: every {DefinitionSuffix} must ship with a {RendererSuffix} " +
            $"following the {{Prefix}}{DefinitionSuffix} ↔ {{Prefix}}{RendererSuffix} naming convention. " +
            "Either add the missing renderer or drop the widget kind." + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void Every_WidgetInstanceRenderer_should_have_a_matching_WidgetDefinition()
    {
        // Inverse rule — catches an orphaned renderer (definition was renamed
        // or removed but the renderer survived). Less catastrophic than the
        // forward rule (the orphan just never fires), but still wasteful and
        // a good signal of dead code.
        Assembly[] assemblies = LoadFrameworkAssemblies();

        IReadOnlyList<Type> renderers = ScanConcreteSubtypes<IWidgetInstanceRenderer>(assemblies)
            .Where(t => t.Name.EndsWith(RendererSuffix, StringComparison.Ordinal))
            .ToList();

        var definitionPrefixes = ScanConcreteSubtypes<WidgetDefinition>(assemblies)
            .Where(t => t.Name.EndsWith(DefinitionSuffix, StringComparison.Ordinal))
            .Select(t => t.Name[..^DefinitionSuffix.Length])
            .ToHashSet(StringComparer.Ordinal);

        List<string> orphans = [];
        foreach (Type renderer in renderers)
        {
            string prefix = renderer.Name[..^RendererSuffix.Length];
            if (!definitionPrefixes.Contains(prefix))
            {
                orphans.Add($"{renderer.FullName} has no matching {prefix}{DefinitionSuffix}");
            }
        }

        orphans.ShouldBeEmpty(
            "Orphaned renderers (no matching WidgetDefinition) — likely dead code from " +
            "a kind rename or removal." + Environment.NewLine +
            string.Join(Environment.NewLine, orphans.Order(StringComparer.Ordinal)));
    }

    private static Assembly[] LoadFrameworkAssemblies()
    {
        string outputDir = Path.GetDirectoryName(typeof(WidgetRendererPairingTests).Assembly.Location)!;

        return Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return !name.Contains("Tests", StringComparison.Ordinal)
                    && !name.EndsWith(".resources", StringComparison.Ordinal);
            })
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch (Exception ex) when (ex is BadImageFormatException or FileLoadException) { return null; }
            })
            .Where(a => a is not null)
            .ToArray()!;
    }

    private static IEnumerable<Type> ScanConcreteSubtypes<TBase>(Assembly[] assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = [.. ex.Types.Where(t => t is not null)!]; }

            foreach (Type type in types)
            {
                if (type.IsAbstract || type.IsInterface || !type.IsClass)
                {
                    continue;
                }

                if (typeof(TBase).IsAssignableFrom(type))
                {
                    yield return type;
                }
            }
        }
    }
}
