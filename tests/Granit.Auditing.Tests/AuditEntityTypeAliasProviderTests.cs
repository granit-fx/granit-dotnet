using Granit.Auditing.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests;

public sealed class AuditEntityTypeAliasProviderTests
{
    [Fact]
    public void Resolve_NoProviders_ReturnsLogicalNameOnly()
    {
        IReadOnlySet<string> result = Array.Empty<IAuditEntityTypeAliasProvider>().Resolve("User");

        result.ShouldBe(new[] { "User" }, ignoreOrder: true);
    }

    [Fact]
    public void Resolve_SingleProvider_IncludesAliasesAndLogicalName()
    {
        StaticAuditEntityTypeAliasProvider provider = new(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["User"] = new HashSet<string>(StringComparer.Ordinal) { "LocalIdentity", "FederatedIdentity" },
            });

        IReadOnlySet<string> result = new[] { (IAuditEntityTypeAliasProvider)provider }.Resolve("User");

        result.ShouldBe(new[] { "User", "LocalIdentity", "FederatedIdentity" }, ignoreOrder: true);
    }

    [Fact]
    public void Resolve_MultipleProviders_UnionsTheirAliases()
    {
        StaticAuditEntityTypeAliasProvider provider1 = new(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["User"] = new HashSet<string>(StringComparer.Ordinal) { "LocalIdentity" },
            });
        StaticAuditEntityTypeAliasProvider provider2 = new(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["User"] = new HashSet<string>(StringComparer.Ordinal) { "FederatedIdentity" },
            });

        IReadOnlySet<string> result = new IAuditEntityTypeAliasProvider[] { provider1, provider2 }.Resolve("User");

        result.ShouldBe(new[] { "User", "LocalIdentity", "FederatedIdentity" }, ignoreOrder: true);
    }

    [Fact]
    public void Resolve_UnknownLogicalType_ReturnsLogicalNameOnly()
    {
        StaticAuditEntityTypeAliasProvider provider = new(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["User"] = new HashSet<string>(StringComparer.Ordinal) { "LocalIdentity" },
            });

        IReadOnlySet<string> result = new[] { (IAuditEntityTypeAliasProvider)provider }.Resolve("Invoice");

        result.ShouldBe(new[] { "Invoice" }, ignoreOrder: true);
    }

    [Fact]
    public void Resolve_IsDirectional_PhysicalLookupDoesNotIncludeLogical()
    {
        // Forensic invariant: GetByEntityAsync("LocalIdentity", id) must NOT
        // bleed back into "User" rows — only auth-only writes are returned.
        StaticAuditEntityTypeAliasProvider provider = new(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["User"] = new HashSet<string>(StringComparer.Ordinal) { "LocalIdentity" },
            });

        IReadOnlySet<string> result = new[] { (IAuditEntityTypeAliasProvider)provider }.Resolve("LocalIdentity");

        result.ShouldBe(new[] { "LocalIdentity" }, ignoreOrder: true);
    }

    [Fact]
    public void Resolve_NullProviders_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            ((IEnumerable<IAuditEntityTypeAliasProvider>)null!).Resolve("User"));

    [Fact]
    public void Resolve_EmptyLogicalType_Throws() =>
        Should.Throw<ArgumentException>(() =>
            Array.Empty<IAuditEntityTypeAliasProvider>().Resolve(""));
}
