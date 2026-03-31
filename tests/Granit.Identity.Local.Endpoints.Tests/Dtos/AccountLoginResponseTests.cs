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

    [Fact]
    public void RequiresTwoFactor_CanBeSetExplicitly()
    {
        AccountLoginResponse response = new(Succeeded: false, RequiresTwoFactor: true);

        response.Succeeded.ShouldBeFalse();
        response.RequiresTwoFactor.ShouldBeTrue();
        response.IsLockedOut.ShouldBeFalse();
        response.IsNotAllowed.ShouldBeFalse();
    }

    [Fact]
    public void IsLockedOut_CanBeSetExplicitly()
    {
        AccountLoginResponse response = new(Succeeded: false, IsLockedOut: true);

        response.Succeeded.ShouldBeFalse();
        response.RequiresTwoFactor.ShouldBeFalse();
        response.IsLockedOut.ShouldBeTrue();
        response.IsNotAllowed.ShouldBeFalse();
    }

    [Fact]
    public void IsNotAllowed_CanBeSetExplicitly()
    {
        AccountLoginResponse response = new(Succeeded: false, IsNotAllowed: true);

        response.Succeeded.ShouldBeFalse();
        response.RequiresTwoFactor.ShouldBeFalse();
        response.IsLockedOut.ShouldBeFalse();
        response.IsNotAllowed.ShouldBeTrue();
    }

    [Fact]
    public void AllProperties_CanBeSetExplicitly()
    {
        AccountLoginResponse response = new(
            Succeeded: true,
            RequiresTwoFactor: true,
            IsLockedOut: true,
            IsNotAllowed: true);

        response.Succeeded.ShouldBeTrue();
        response.RequiresTwoFactor.ShouldBeTrue();
        response.IsLockedOut.ShouldBeTrue();
        response.IsNotAllowed.ShouldBeTrue();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        AccountLoginResponse response1 = new(Succeeded: true);
        AccountLoginResponse response2 = new(Succeeded: true);

        response1.ShouldBe(response2);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        AccountLoginResponse response1 = new(Succeeded: true);
        AccountLoginResponse response2 = new(Succeeded: false);

        response1.ShouldNotBe(response2);
    }
}
