using Granit.Authorization;
using Granit.Diagnostics.Endpoints.Permissions;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Endpoints.Tests;

public sealed class DiagnosticsPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_RegistersDiagnosticsGroup()
    {
        FakePermissionDefinitionContext context = new();
        DiagnosticsPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        context.Groups.ShouldContain(g => g.Name == DiagnosticsPermissions.GroupName);
    }

    [Fact]
    public void DefinePermissions_RegistersMonitoringReadPermission()
    {
        FakePermissionDefinitionContext context = new();
        DiagnosticsPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        PermissionGroup group = context.Groups.Single();
        group.Permissions.ShouldContain(p => p.Name == DiagnosticsPermissions.Monitoring.Read);
    }

    [Fact]
    public void DefinePermissions_CalledTwice_DoesNotDuplicateGroup()
    {
        FakePermissionDefinitionContext context = new();
        DiagnosticsPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);
        provider.DefinePermissions(context);

        context.Groups.Select(g => g.Name)
            .ShouldContain(n => n == DiagnosticsPermissions.GroupName);
    }

    // ── Test double ────────────────────────────────────────────────────────────

    private sealed class FakePermissionDefinitionContext : IPermissionDefinitionContext
    {
        private readonly Dictionary<string, PermissionGroup> _groups = new(StringComparer.Ordinal);

        public IReadOnlyCollection<PermissionGroup> Groups => _groups.Values;

        public PermissionGroup AddGroup(string name, LocalizableString? displayName = null)
        {
            if (_groups.TryGetValue(name, out PermissionGroup? existing))
            {
                return existing;
            }

            PermissionGroup group = new(name, displayName);
            _groups[name] = group;
            return group;
        }
    }
}
