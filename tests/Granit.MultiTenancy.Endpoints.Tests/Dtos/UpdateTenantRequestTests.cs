using Granit.MultiTenancy.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Dtos;

public sealed class UpdateTenantRequestTests
{
    [Fact]
    public void Constructor_MapsAllFields()
    {
        UpdateTenantRequest request = new("Updated Name", "updated@acme.com", "FR");

        request.Name.ShouldBe("Updated Name");
        request.ContactEmail.ShouldBe("updated@acme.com");
        request.Jurisdiction.ShouldBe("FR");
    }

    [Fact]
    public void Constructor_NullContactEmail_IsValid()
    {
        UpdateTenantRequest request = new("Acme", null, null);

        request.ContactEmail.ShouldBeNull();
    }
}
