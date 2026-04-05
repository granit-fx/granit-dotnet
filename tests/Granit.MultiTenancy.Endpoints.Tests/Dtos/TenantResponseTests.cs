using Granit.MultiTenancy.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Dtos;

public sealed class TenantResponseTests
{
    [Fact]
    public void Constructor_MapsAllFields()
    {
        var id = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        TenantResponse response = new(id, "Acme Corp", "acme-corp", "admin@acme.com", true, "BE", createdAt);

        response.Id.ShouldBe(id);
        response.Name.ShouldBe("Acme Corp");
        response.Identifier.ShouldBe("acme-corp");
        response.ContactEmail.ShouldBe("admin@acme.com");
        response.IsActive.ShouldBeTrue();
        response.Jurisdiction.ShouldBe("BE");
        response.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public void Constructor_NullContactEmail_IsValid()
    {
        TenantResponse response = new(Guid.NewGuid(), "Acme", "acme", null, true, null, DateTimeOffset.UtcNow);

        response.ContactEmail.ShouldBeNull();
    }
}
