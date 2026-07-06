using Granit.Auditing.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Dtos;

public sealed class AuditPropertyChangeResponseTests
{
    [Fact]
    public void WithNullValues_IsValid()
    {
        AuditPropertyChangeResponse response = new("Name", null, "New");

        response.OriginalValue.ShouldBeNull();
        response.NewValue.ShouldBe("New");
    }

    [Fact]
    public void WithSensitiveMask_IsValid()
    {
        AuditPropertyChangeResponse response = new("Password", "***", "***");

        response.OriginalValue.ShouldBe("***");
        response.NewValue.ShouldBe("***");
    }
}
