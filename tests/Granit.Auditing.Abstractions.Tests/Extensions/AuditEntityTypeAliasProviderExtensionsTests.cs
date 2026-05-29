using Granit.Auditing.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Abstractions.Tests.Extensions;

public sealed class AuditEntityTypeAliasProviderExtensionsTests
{
    private static readonly IAuditEntityTypeAliasProvider UserAliases =
        new StaticAuditEntityTypeAliasProvider(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["User"] = new HashSet<string>(StringComparer.Ordinal) { "LocalIdentity", "FederatedIdentity" },
            });

    [Fact]
    public void Resolve_IncludesLogicalNameAndEveryAlias()
    {
        IReadOnlySet<string> resolved = new[] { UserAliases }.Resolve("User");

        resolved.ShouldBe(["User", "LocalIdentity", "FederatedIdentity"], ignoreOrder: true);
    }

    [Fact]
    public void Resolve_UnknownType_ReturnsOnlyTheLogicalName()
    {
        IReadOnlySet<string> resolved = new[] { UserAliases }.Resolve("Order");

        resolved.ShouldBe(["Order"]);
    }

    [Fact]
    public void Resolve_NoProviders_ReturnsOnlyTheLogicalName()
    {
        IReadOnlySet<string> resolved = Array.Empty<IAuditEntityTypeAliasProvider>().Resolve("User");

        resolved.ShouldBe(["User"]);
    }
}
