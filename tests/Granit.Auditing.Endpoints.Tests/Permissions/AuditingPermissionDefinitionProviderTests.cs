using Granit.Auditing.Endpoints.Permissions;
using Granit.Authorization;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Permissions;

public sealed class AuditingPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_RegistersAuditingGroup()
    {
        FakePermissionDefinitionContext context = new();
        AuditingPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        context.Groups.ShouldContain(g => g.Name == AuditingPermissions.GroupName);
    }

    [Fact]
    public void DefinePermissions_RegistersEntriesReadPermission()
    {
        FakePermissionDefinitionContext context = new();
        AuditingPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        PermissionGroup group = context.Groups.Single();
        group.Permissions.ShouldContain(p => p.Name == AuditingPermissions.AuditEntries.Read);
    }

    [Fact]
    public void DefinePermissions_RegistersEntriesManagePermission()
    {
        FakePermissionDefinitionContext context = new();
        AuditingPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        PermissionGroup group = context.Groups.Single();
        group.Permissions.ShouldContain(p => p.Name == AuditingPermissions.AuditEntries.Manage);
    }

    [Fact]
    public void DefinePermissions_RegistersExactlyTwoPermissions()
    {
        FakePermissionDefinitionContext context = new();
        AuditingPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        PermissionGroup group = context.Groups.Single();
        group.Permissions.Count.ShouldBe(2);
    }

    [Fact]
    public void DefinePermissions_CalledTwice_DoesNotDuplicateGroup()
    {
        FakePermissionDefinitionContext context = new();
        AuditingPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);
        provider.DefinePermissions(context);

        context.Groups.Select(g => g.Name)
            .ShouldContain(n => n == AuditingPermissions.GroupName);
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
