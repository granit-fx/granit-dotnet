using System.Reflection;
using Granit.Authorization;
using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Statically enforces the OData host-feed permission convention: any
/// <see cref="PermissionDefinition"/> whose name matches
/// <c>OData.Host.*</c> MUST declare
/// <see cref="MultiTenancySides.Host"/>. Convention spelled out in
/// <c>Granit.Http.ODataExposure</c> README and in the host-feed
/// strict-config validator (#1665 / PR #1672).
/// </summary>
/// <remarks>
/// <para>
/// The runtime strict-config validator on <c>MapGranitODataHostEndpoints</c>
/// catches mis-declared permissions, BUT only when an application actually
/// mounts the host-feed. This archi test catches the same misconfiguration
/// at CI time, regardless of whether any host wires the feed — useful
/// because a typo in <c>MultiTenancySides.Both</c> would otherwise sit
/// silently in a permission provider until a host pulls the trigger and
/// fails at startup.
/// </para>
/// <para>
/// The test loads every <c>Granit.*.dll</c> in the test output directory,
/// finds non-abstract <see cref="IPermissionDefinitionProvider"/>
/// implementations, instantiates them via their parameterless constructor,
/// invokes <see cref="IPermissionDefinitionProvider.DefinePermissions(IPermissionDefinitionContext)"/>,
/// and asserts every captured definition matching the prefix carries
/// the right side.
/// </para>
/// </remarks>
public sealed class ODataPermissionConventionTests
{
    [Fact]
    public void OData_Host_permissions_must_declare_MultiTenancySides_Host()
    {
        IReadOnlyList<PermissionDefinition> hostFeedPermissions = ScanPermissions()
            .Where(p => p.Name.StartsWith("OData.Host.", StringComparison.Ordinal))
            .ToList();

        IEnumerable<string> violators = hostFeedPermissions
            .Where(p => p.MultiTenancySides != MultiTenancySides.Host)
            .Select(p => $"{p.Name} ({p.MultiTenancySides})")
            .OrderBy(s => s, StringComparer.Ordinal);

        violators.ShouldBeEmpty(
            "OData host-feed permissions (`OData.Host.*`) must declare `MultiTenancySides.Host`. "
            + "`MultiTenancySides.Tenant` and `MultiTenancySides.Both` are rejected — they would let a "
            + "tenant-side role inheritance unlock cross-tenant BI access. "
            + $"Violators: {string.Join(", ", violators)}");
    }

    [Fact]
    public void Tenant_feed_permissions_must_not_declare_MultiTenancySides_Host()
    {
        // Mirror of the above: an `OData.{Module}.*` permission (no `Host`
        // segment) is meant for the tenant-feed and SHOULD NOT carry
        // MultiTenancySides.Host — that would prevent tenant users from
        // ever being granted it. Common typo: copy-paste a host-feed
        // declaration but forget to drop the .Host segment in the name.
        IReadOnlyList<PermissionDefinition> tenantFeedPermissions = ScanPermissions()
            .Where(p =>
                p.Name.StartsWith("OData.", StringComparison.Ordinal)
                && !p.Name.StartsWith("OData.Host.", StringComparison.Ordinal))
            .ToList();

        IEnumerable<string> violators = tenantFeedPermissions
            .Where(p => p.MultiTenancySides == MultiTenancySides.Host)
            .Select(p => $"{p.Name} ({p.MultiTenancySides})")
            .OrderBy(s => s, StringComparer.Ordinal);

        violators.ShouldBeEmpty(
            "OData tenant-feed permissions (`OData.{Module}.*`, no `Host` segment) must not be declared "
            + "with `MultiTenancySides.Host` — tenant users could never be granted them. "
            + "Either rename to `OData.Host.{Module}.*` (host-feed semantics), or change the side to "
            + "`MultiTenancySides.Both` / `MultiTenancySides.Tenant`. "
            + $"Violators: {string.Join(", ", violators)}");
    }

    /// <summary>
    /// Loads framework assemblies, instantiates every concrete
    /// <see cref="IPermissionDefinitionProvider"/>, captures the
    /// declarations into an in-memory context, and returns every
    /// <see cref="PermissionDefinition"/> registered. Skips providers
    /// that lack a parameterless constructor (typically those that
    /// inject DI services for runtime resolution — they are not
    /// statically inspectable here, and the runtime strict-config
    /// validator catches them at boot anyway).
    /// </summary>
    private static IReadOnlyList<PermissionDefinition> ScanPermissions()
    {
        string outputDir = Path.GetDirectoryName(typeof(ODataPermissionConventionTests).Assembly.Location)!;

        Assembly[] assemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
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

        RecordingContext context = new();

        foreach (Assembly assembly in assemblies)
        {
            IEnumerable<Type> providerTypes;
            try
            {
                providerTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                providerTypes = ex.Types.Where(t => t is not null)!;
            }

            foreach (Type providerType in providerTypes
                .Where(t => t is { IsClass: true, IsAbstract: false }
                    && typeof(IPermissionDefinitionProvider).IsAssignableFrom(t)))
            {
                ConstructorInfo? ctor = providerType.GetConstructor(Type.EmptyTypes);
                if (ctor is null)
                {
                    // DI-constructed provider — the runtime validator catches
                    // it at boot. Static inspection skips it.
                    continue;
                }

                var provider = (IPermissionDefinitionProvider)ctor.Invoke(null);
                provider.DefinePermissions(context);
            }
        }

        return context.AllPermissions;
    }

    /// <summary>
    /// Minimal <see cref="IPermissionDefinitionContext"/> that lets each
    /// provider populate real <see cref="PermissionGroup"/>s; tests then
    /// flatten <see cref="PermissionGroup.Permissions"/> across every
    /// captured group to drive the assertions.
    /// </summary>
    private sealed class RecordingContext : IPermissionDefinitionContext
    {
        private readonly Dictionary<string, PermissionGroup> _groups = new(StringComparer.Ordinal);

        public IReadOnlyList<PermissionDefinition> AllPermissions =>
            [.. _groups.Values.SelectMany(g => g.Permissions)];

        public PermissionGroup AddGroup(string name, Granit.Localization.LocalizableString? displayName = null)
        {
            if (_groups.TryGetValue(name, out PermissionGroup? existing))
            {
                return existing;
            }

            PermissionGroup created = new(name, displayName);
            _groups[name] = created;
            return created;
        }
    }
}
