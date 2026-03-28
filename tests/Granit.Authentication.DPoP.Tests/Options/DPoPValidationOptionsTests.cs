using Granit.Authentication.DPoP.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.DPoP.Tests.Options;

public sealed class DPoPValidationOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        DPoPValidationOptions.SectionName.ShouldBe("Authentication:DPoP");

    [Fact]
    public void RequireDPoP_DefaultsToFalse() =>
        new DPoPValidationOptions().RequireDPoP.ShouldBeFalse();

    [Fact]
    public void AllowedAlgorithms_DefaultsToES256AndPS256()
    {
        DPoPValidationOptions options = new();
        options.AllowedAlgorithms.ShouldContain("ES256");
        options.AllowedAlgorithms.ShouldContain("PS256");
        options.AllowedAlgorithms.Length.ShouldBe(2);
    }

    [Fact]
    public void ClockSkew_DefaultsTo30Seconds() =>
        new DPoPValidationOptions().ClockSkew.ShouldBe(TimeSpan.FromSeconds(30));

    [Fact]
    public void MaxProofLifetime_DefaultsTo5Minutes() =>
        new DPoPValidationOptions().MaxProofLifetime.ShouldBe(TimeSpan.FromMinutes(5));

    [Fact]
    public void EnableReplayProtection_DefaultsToTrue() =>
        new DPoPValidationOptions().EnableReplayProtection.ShouldBeTrue();

    [Fact]
    public void RequireNonce_DefaultsToFalse() =>
        new DPoPValidationOptions().RequireNonce.ShouldBeFalse();

    [Fact]
    public void RequireTokenBinding_DefaultsToFalse() =>
        new DPoPValidationOptions().RequireTokenBinding.ShouldBeFalse();

    [Fact]
    public void MinimumRsaKeySize_DefaultsTo2048() =>
        new DPoPValidationOptions().MinimumRsaKeySize.ShouldBe(2048);

    [Fact]
    public void AllProperties_CanBeSet()
    {
        DPoPValidationOptions options = new()
        {
            RequireDPoP = true,
            AllowedAlgorithms = ["ES256"],
            ClockSkew = TimeSpan.FromSeconds(10),
            MaxProofLifetime = TimeSpan.FromMinutes(2),
            EnableReplayProtection = false,
            RequireNonce = true,
            RequireTokenBinding = true,
            MinimumRsaKeySize = 4096,
        };

        options.RequireDPoP.ShouldBeTrue();
        options.AllowedAlgorithms.ShouldBe(["ES256"]);
        options.ClockSkew.ShouldBe(TimeSpan.FromSeconds(10));
        options.MaxProofLifetime.ShouldBe(TimeSpan.FromMinutes(2));
        options.EnableReplayProtection.ShouldBeFalse();
        options.RequireNonce.ShouldBeTrue();
        options.RequireTokenBinding.ShouldBeTrue();
        options.MinimumRsaKeySize.ShouldBe(4096);
    }
}
