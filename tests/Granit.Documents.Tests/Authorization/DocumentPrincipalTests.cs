using Granit.Documents.Authorization;
using Shouldly;
using Xunit;

namespace Granit.Documents.Tests.Authorization;

public sealed class DocumentPrincipalTests
{
    [Fact]
    public void AllGranteeIds_DeduplicatesAcrossUserRolesGroups()
    {
        var user = Guid.NewGuid();
        var sharedRole = Guid.NewGuid();
        var otherRole = Guid.NewGuid();
        var group = Guid.NewGuid();

        var principal = new DocumentPrincipal(
            user,
            RoleIds: [sharedRole, otherRole, sharedRole],
            GroupIds: [group, sharedRole]);

        principal.AllGranteeIds.ShouldBe([user, sharedRole, otherRole, group], ignoreOrder: true);
    }

    [Fact]
    public void AllGranteeIds_FiltersGuidEmpty()
    {
        var principal = new DocumentPrincipal(
            UserId: Guid.Empty,
            RoleIds: [Guid.Empty, Guid.NewGuid()],
            GroupIds: []);

        principal.AllGranteeIds.Count.ShouldBe(1);
        principal.AllGranteeIds.ShouldNotContain(Guid.Empty);
    }

    [Fact]
    public void AllGranteeIds_CachedAcrossInvocations()
    {
        var principal = new DocumentPrincipal(Guid.NewGuid(), [Guid.NewGuid()], []);

        IReadOnlyList<Guid> first = principal.AllGranteeIds;
        IReadOnlyList<Guid> second = principal.AllGranteeIds;

        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void ForUser_HasNoRolesNoGroups()
    {
        var user = Guid.NewGuid();
        var principal = DocumentPrincipal.ForUser(user);

        principal.UserId.ShouldBe(user);
        principal.RoleIds.ShouldBeEmpty();
        principal.GroupIds.ShouldBeEmpty();
        principal.AllGranteeIds.ShouldBe([user]);
    }
}
