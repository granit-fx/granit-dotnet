using Granit.Auditing.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Extensions;

public sealed class AuditChildResolverExtensionsTests
{
    [Fact]
    public async Task ResolveAllAsync_NoResolvers_ReturnsEmpty()
    {
        IReadOnlyCollection<AuditChildScope> result = await Array
            .Empty<IAuditChildResolver>()
            .ResolveAllAsync("Page", "page-1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAllAsync_SingleResolver_ReturnsItsScopes()
    {
        FakeResolver r = new(
            ("Page", "page-1"),
            [new AuditChildScope("PageVersion", ["v1", "v2"])]);

        IReadOnlyCollection<AuditChildScope> result = await new[] { (IAuditChildResolver)r }
            .ResolveAllAsync("Page", "page-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        AuditChildScope scope = result.Single();
        scope.ChildEntityType.ShouldBe("PageVersion");
        scope.ChildEntityIds.ShouldBe(new[] { "v1", "v2" }, ignoreOrder: true);
    }

    [Fact]
    public async Task ResolveAllAsync_MultipleResolversSameChildType_UnionsIdsWithoutDuplicates()
    {
        FakeResolver r1 = new(("Page", "page-1"), [new AuditChildScope("PageVersion", ["v1", "v2"])]);
        FakeResolver r2 = new(("Page", "page-1"), [new AuditChildScope("PageVersion", ["v2", "v3"])]);

        IReadOnlyCollection<AuditChildScope> result = await new IAuditChildResolver[] { r1, r2 }
            .ResolveAllAsync("Page", "page-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result.Single().ChildEntityIds.ShouldBe(new[] { "v1", "v2", "v3" }, ignoreOrder: true);
    }

    [Fact]
    public async Task ResolveAllAsync_MultipleResolversDifferentChildTypes_KeepsBothScopes()
    {
        FakeResolver r1 = new(("Page", "page-1"), [new AuditChildScope("PageVersion", ["v1"])]);
        FakeResolver r2 = new(("Page", "page-1"), [new AuditChildScope("PageTranslation", ["t-fr", "t-en"])]);

        IReadOnlyCollection<AuditChildScope> result = await new IAuditChildResolver[] { r1, r2 }
            .ResolveAllAsync("Page", "page-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.Single(s => s.ChildEntityType == "PageVersion").ChildEntityIds.ShouldBe(new[] { "v1" });
        result.Single(s => s.ChildEntityType == "PageTranslation").ChildEntityIds.ShouldBe(new[] { "t-fr", "t-en" }, ignoreOrder: true);
    }

    [Fact]
    public async Task ResolveAllAsync_EmptyChildIdsCollection_IsDropped()
    {
        // A resolver returning a scope with no ids contributes nothing — keeps
        // the downstream batch query from issuing empty IN-lists per scope.
        FakeResolver r = new(("Page", "page-1"), [new AuditChildScope("PageVersion", [])]);

        IReadOnlyCollection<AuditChildScope> result = await new[] { (IAuditChildResolver)r }
            .ResolveAllAsync("Page", "page-1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAllAsync_NullResolvers_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            ((IEnumerable<IAuditChildResolver>)null!).ResolveAllAsync("Page", "page-1"));

    [Fact]
    public async Task ResolveAllAsync_EmptyParentType_Throws() =>
        await Should.ThrowAsync<ArgumentException>(() =>
            Array.Empty<IAuditChildResolver>().ResolveAllAsync("", "page-1"));

    [Fact]
    public async Task ResolveAllAsync_EmptyParentId_Throws() =>
        await Should.ThrowAsync<ArgumentException>(() =>
            Array.Empty<IAuditChildResolver>().ResolveAllAsync("Page", ""));

    [Fact]
    public void ToTargets_NoScopes_ReturnsParentOnly()
    {
        AuditEntityRef parent = new("Page", "page-1");

        IReadOnlyCollection<AuditEntityRef> result = Array.Empty<AuditChildScope>().ToTargets(parent);

        result.ShouldBe(new[] { parent });
    }

    [Fact]
    public void ToTargets_WithScopes_PrependsParentAndFlattensChildren()
    {
        AuditEntityRef parent = new("Page", "page-1");
        AuditChildScope[] scopes =
        [
            new("PageVersion", ["v1", "v2"]),
            new("PageTranslation", ["t-fr"]),
        ];

        IReadOnlyCollection<AuditEntityRef> result = scopes.ToTargets(parent);

        result.ShouldBe(new[]
        {
            parent,
            new AuditEntityRef("PageVersion", "v1"),
            new AuditEntityRef("PageVersion", "v2"),
            new AuditEntityRef("PageTranslation", "t-fr"),
        });
    }

    private sealed class FakeResolver(
        (string ParentType, string ParentId) expected,
        IReadOnlyCollection<AuditChildScope> scopes) : IAuditChildResolver
    {
        public Task<IReadOnlyCollection<AuditChildScope>> ResolveAsync(
            string parentEntityType, string parentEntityId, CancellationToken cancellationToken = default)
        {
            (parentEntityType, parentEntityId).ShouldBe(expected);
            return Task.FromResult(scopes);
        }
    }
}
