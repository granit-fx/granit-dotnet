using Granit.DataLookup.Registry;
using Granit.DataLookup.Sources;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.Tests.Registry;

public sealed class LookupRegistryTests
{
    [Fact]
    public void Resolve_returns_registered_source()
    {
        ILookupSource source = BuildStub(name: "tenants");
        LookupRegistry registry = new([source]);

        registry.Resolve("tenants").ShouldBe(source);
    }

    [Fact]
    public void Resolve_returns_null_for_unknown_name()
    {
        LookupRegistry registry = new([]);

        registry.Resolve("unknown").ShouldBeNull();
    }

    [Fact]
    public void Duplicate_names_throw_at_construction()
    {
        ILookupSource a = BuildStub(name: "tenants");
        ILookupSource b = BuildStub(name: "tenants");

        Should.Throw<InvalidOperationException>(() => new LookupRegistry([a, b]))
            .Message.ShouldContain("tenants");
    }

    [Fact]
    public void Manifest_is_sorted_alphabetically()
    {
        LookupRegistry registry = new(
        [
            BuildStub("zeta"),
            BuildStub("alpha"),
            BuildStub("mu"),
        ]);

        IReadOnlyList<LookupManifestEntry> manifest = registry.GetManifest();

        manifest.Select(e => e.Name).ShouldBe(["alpha", "mu", "zeta"]);
    }

    [Fact]
    public void Manifest_exposes_required_permission_and_scope_keys()
    {
        ILookupSource source = BuildStub(
            name: "meters",
            requiredPermission: "Metering.Meters.Read",
            scopeKeys: ["tenantId"]);
        LookupRegistry registry = new([source]);

        LookupManifestEntry entry = registry.GetManifest().ShouldHaveSingleItem();
        entry.Name.ShouldBe("meters");
        entry.RequiredPermission.ShouldBe("Metering.Meters.Read");
        entry.ScopeKeys.ShouldBe(["tenantId"]);
    }

    private static ILookupSource BuildStub(
        string name,
        string? requiredPermission = null,
        IReadOnlyList<string>? scopeKeys = null)
    {
        ILookupSource source = Substitute.For<ILookupSource>();
        source.Name.Returns(name);
        source.RequiredPermission.Returns(requiredPermission);
        source.ScopeKeys.Returns(scopeKeys ?? []);
        return source;
    }
}
