using Granit.Domain;
using Granit.Parties.Domain;
using Shouldly;
using Xunit;

namespace Granit.Parties.Tests.Architecture;

/// <summary>
/// Convention tests for the <see cref="Party"/> aggregate. Pinned because the dual-use
/// design (host scope + tenant scope, both backed by the same row) hinges on the
/// <see cref="IMultiTenant"/> contract — losing it would silently break the multi-tenant
/// query filter.
/// </summary>
public sealed class PartyConventionTests
{
    [Fact]
    public void Contact_ImplementsIMultiTenant() =>
        typeof(IMultiTenant).IsAssignableFrom(typeof(Party))
            .ShouldBeTrue("Party must remain IMultiTenant for the dual-use scope filter");

    [Fact]
    public void Contact_TenantIdIsNullable() =>
        typeof(Party).GetProperty(nameof(IMultiTenant.TenantId))!.PropertyType
            .ShouldBe(typeof(Guid?), "host-scoped parties require TenantId = null");
}
