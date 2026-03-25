using Granit.AuditLog.Endpoints.Permissions;
using Granit.Authorization.Abstractions;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Endpoints.Tests.Permissions;

public sealed class AuditLogPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_RegistersAuditLogGroup()
    {
        FakePermissionDefinitionContext context = new();
        AuditLogPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        context.Groups.ShouldContain(g => g.Name == AuditLogPermissions.GroupName);
    }

    [Fact]
    public void DefinePermissions_RegistersEntriesReadPermission()
    {
        FakePermissionDefinitionContext context = new();
        AuditLogPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        PermissionGroup group = context.Groups.Single();
        group.Permissions.ShouldContain(p => p.Name == AuditLogPermissions.Entries.Read);
    }

    [Fact]
    public void DefinePermissions_RegistersExactlyOnePermission()
    {
        FakePermissionDefinitionContext context = new();
        AuditLogPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        PermissionGroup group = context.Groups.Single();
        group.Permissions.Count.ShouldBe(1);
    }

    [Fact]
    public void DefinePermissions_CalledTwice_DoesNotDuplicateGroup()
    {
        FakePermissionDefinitionContext context = new();
        AuditLogPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);
        provider.DefinePermissions(context);

        context.Groups.Select(g => g.Name)
            .ShouldContain(n => n == AuditLogPermissions.GroupName);
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
