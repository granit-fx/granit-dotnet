using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Microsoft.AspNetCore.DataProtection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

public sealed class DataProtectionSessionReviewTokenServiceTests
{
    private static DataProtectionSessionReviewTokenService CreateService() =>
        new(new EphemeralDataProtectionProvider(),
            Microsoft.Extensions.Options.Options.Create(new SessionReviewOptions()));

    [Fact]
    public void IssueThenValidate_RoundtripsPayload()
    {
        DataProtectionSessionReviewTokenService sut = CreateService();

        string token = sut.Issue("user-1", "s1", "dev-1", "US");
        SessionReviewTokenPayload? payload = sut.Validate(token);

        payload.ShouldNotBeNull();
        payload.UserId.ShouldBe("user-1");
        payload.SessionId.ShouldBe("s1");
        payload.DeviceId.ShouldBe("dev-1");
        payload.Country.ShouldBe("US");
    }

    [Fact]
    public void IssueThenValidate_PreservesNullDeviceAndCountry()
    {
        DataProtectionSessionReviewTokenService sut = CreateService();

        SessionReviewTokenPayload? payload = sut.Validate(sut.Issue("user-1", "s1", deviceId: null, country: null));

        payload.ShouldNotBeNull();
        payload.DeviceId.ShouldBeNull();
        payload.Country.ShouldBeNull();
    }

    [Fact]
    public void Validate_GarbageToken_ReturnsNull() =>
        CreateService().Validate("not-a-real-token").ShouldBeNull();

    [Fact]
    public void Validate_TokenFromDifferentKeyRing_ReturnsNull()
    {
        // A token minted under one key ring must not validate under another (tamper/forgery resistance).
        string token = CreateService().Issue("user-1", "s1", "dev-1", "US");

        CreateService().Validate(token).ShouldBeNull();
    }
}
