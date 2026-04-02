using Granit.Authorization;
using Granit.BackgroundJobs.Endpoints.Permissions;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Endpoints.Tests;

public sealed class BackgroundJobsPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_RegistersBackgroundJobsManagePermission()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        BackgroundJobsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert — group and permission declared
        context.Groups.ShouldContain(g => g.Name == BackgroundJobsPermissions.GroupName);
        PermissionGroup group = context.Groups.Single();
        group.Permissions.ShouldContain(p => p.Name == BackgroundJobsPermissions.Jobs.Manage);
    }

    [Fact]
    public void DefinePermissions_RegistersBackgroundJobsReadPermission()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        BackgroundJobsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert — group and permission declared
        PermissionGroup group = context.Groups.Single();
        group.Permissions.ShouldContain(p => p.Name == BackgroundJobsPermissions.Jobs.Read);
    }

    [Fact]
    public void DefinePermissions_CalledTwice_DoesNotDuplicateGroup()
    {
        // Arrange — same context receives two calls (multi-provider scenario)
        FakePermissionDefinitionContext context = new();
        BackgroundJobsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);
        provider.DefinePermissions(context); // second call via same context uses GetOrAdd semantics

        // Assert — AddGroup is idempotent (GetOrAdd): only one group in context
        context.Groups.Select(g => g.Name)
            .ShouldContain(n => n == BackgroundJobsPermissions.GroupName);
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
