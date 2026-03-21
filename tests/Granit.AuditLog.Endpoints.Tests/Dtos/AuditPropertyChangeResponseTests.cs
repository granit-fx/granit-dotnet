using Granit.AuditLog.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Endpoints.Tests.Dtos;

public sealed class AuditPropertyChangeResponseTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        AuditPropertyChangeResponse response = new("Email", "old@test.com", "new@test.com");

        response.PropertyName.ShouldBe("Email");
        response.OriginalValue.ShouldBe("old@test.com");
        response.NewValue.ShouldBe("new@test.com");
    }

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
