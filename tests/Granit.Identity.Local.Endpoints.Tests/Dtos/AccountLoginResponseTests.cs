using Granit.Identity.Local.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests.Dtos;

public sealed class AccountLoginResponseTests
{
    [Fact]
    public void Succeeded_True_HasCorrectDefaults()
    {
        AccountLoginResponse response = new(Succeeded: true);

        response.Succeeded.ShouldBeTrue();
        response.RequiresTwoFactor.ShouldBeFalse();
        response.IsLockedOut.ShouldBeFalse();
        response.IsNotAllowed.ShouldBeFalse();
    }

    [Fact]
    public void Succeeded_False_HasCorrectDefaults()
    {
        AccountLoginResponse response = new(Succeeded: false);

        response.Succeeded.ShouldBeFalse();
        response.RequiresTwoFactor.ShouldBeFalse();
        response.IsLockedOut.ShouldBeFalse();
        response.IsNotAllowed.ShouldBeFalse();
    }
}
