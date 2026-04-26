using Granit.Contacts.Domain;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Contacts.Tests.Architecture;

/// <summary>
/// Convention tests for the <see cref="Contact"/> aggregate. Pinned because the dual-use
/// design (host scope + tenant scope, both backed by the same row) hinges on the
/// <see cref="IMultiTenant"/> contract — losing it would silently break the multi-tenant
/// query filter.
/// </summary>
public sealed class ContactConventionTests
{
    [Fact]
    public void Contact_ImplementsIMultiTenant() =>
        typeof(IMultiTenant).IsAssignableFrom(typeof(Contact))
            .ShouldBeTrue("Contact must remain IMultiTenant for the dual-use scope filter");

    [Fact]
    public void Contact_TenantIdIsNullable() =>
        typeof(Contact).GetProperty(nameof(IMultiTenant.TenantId))!.PropertyType
            .ShouldBe(typeof(Guid?), "host-scoped contacts require TenantId = null");
}
