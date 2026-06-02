using Granit.MultiTenancy.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Dtos;

public sealed class UpdateTenantRequestTests
{
    private const string AnyStamp = "00000000-0000-0000-0000-000000000000";

    [Fact]
    public void Constructor_MapsAllFields()
    {
        UpdateTenantRequest request = new("Updated Name", "updated@acme.com", "FR", AnyStamp);

        request.Name.ShouldBe("Updated Name");
        request.ContactEmail.ShouldBe("updated@acme.com");
        request.Jurisdiction.ShouldBe("FR");
    }

    [Fact]
    public void Constructor_NullContactEmail_IsValid()
    {
        UpdateTenantRequest request = new("Acme", null, null, AnyStamp);

        request.ContactEmail.ShouldBeNull();
    }
}
