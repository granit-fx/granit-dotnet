using Granit.Auditing.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Dtos;

public sealed class AuditEntityChangeResponseTests
{
    [Fact]
    public void WithEmptyPropertyChanges_IsValid()
    {
        AuditEntityChangeResponse response = new("Invoice", "INV-001", "Created", []);

        response.PropertyChanges.ShouldBeEmpty();
    }
}
