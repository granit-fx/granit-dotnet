using Granit.DataProtection;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class AddressVerificationTests
{
    [Fact]
    public void Unverified_IsTheInitialState()
    {
        AddressVerification.Unverified.Status.ShouldBe(AddressVerificationStatus.Unverified);
        AddressVerification.Unverified.Source.ShouldBe(AddressVerificationSource.None);
        AddressVerification.Unverified.VerifiedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_CapturesVerdictAndProvenance()
    {
        DateTimeOffset at = DateTimeOffset.UnixEpoch;

        var verification = AddressVerification.Create(
            AddressVerificationStatus.DeliveryConfirmed,
            AddressVerificationSource.Delivery,
            at,
            verifiedBy: "logistics",
            evidence: "SHP-1");

        verification.Status.ShouldBe(AddressVerificationStatus.DeliveryConfirmed);
        verification.Source.ShouldBe(AddressVerificationSource.Delivery);
        verification.VerifiedAt.ShouldBe(at);
        verification.VerifiedBy.ShouldBe("logistics");
        verification.Evidence.ShouldBe("SHP-1");
    }

    [Fact]
    public void Equality_IsStructural()
    {
        var a = AddressVerification.Create(
            AddressVerificationStatus.ManuallyConfirmed, AddressVerificationSource.Manual, DateTimeOffset.UnixEpoch, "op");
        var b = AddressVerification.Create(
            AddressVerificationStatus.ManuallyConfirmed, AddressVerificationSource.Manual, DateTimeOffset.UnixEpoch, "op");

        a.ShouldBe(b);
    }

    // Decision E: provenance (VerifiedBy) and evidence references are PII — see AddressGeocodingTests for rationale.
    [Theory]
    [InlineData(nameof(AddressVerification.VerifiedBy))]
    [InlineData(nameof(AddressVerification.Evidence))]
    public void SensitiveFields_AreClassifiedConfidential(string propertyName)
    {
        SensitivePropertyRegistry registry = new([typeof(AddressVerification).Assembly]);

        registry.TryGet(propertyName, out SensitivePropertyEntry entry).ShouldBeTrue();
        entry.Level.ShouldBe(Sensitivity.Confidential);
    }
}
