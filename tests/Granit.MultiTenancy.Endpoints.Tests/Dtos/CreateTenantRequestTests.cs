using Granit.MultiTenancy.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Dtos;

public sealed class CreateTenantRequestTests
{
    [Fact]
    public void Constructor_MapsAllFields()
    {
        CreateTenantRequest request = new("Acme Corp", "acme-corp", "admin@acme.com", "BE");

        request.Name.ShouldBe("Acme Corp");
        request.Identifier.ShouldBe("acme-corp");
        request.ContactEmail.ShouldBe("admin@acme.com");
        request.Jurisdiction.ShouldBe("BE");
    }

    [Fact]
    public void Constructor_NullContactEmail_IsValid()
    {
        CreateTenantRequest request = new("Acme", "acme", null, null);

        request.ContactEmail.ShouldBeNull();
    }
}
