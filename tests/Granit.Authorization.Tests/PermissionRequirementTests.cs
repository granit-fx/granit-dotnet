using Granit.Authorization.Authorization;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionRequirementTests
{
    [Fact]
    public void ImplementsIAuthorizationRequirement()
    {
        PermissionRequirement requirement = new("Invoices.Read");

        requirement.ShouldBeAssignableTo<IAuthorizationRequirement>();
    }
}
