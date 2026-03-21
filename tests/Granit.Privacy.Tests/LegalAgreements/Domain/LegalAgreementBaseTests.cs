using Granit.Privacy.LegalAgreements.Domain;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.LegalAgreements.Domain;

public sealed class LegalAgreementBaseTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        TestLegalAgreement sut = new();

        sut.UserId.ShouldBe(Guid.Empty);
        sut.DocumentId.ShouldBe(string.Empty);
        sut.Version.ShouldBe(string.Empty);
        sut.AcceptedAt.ShouldBe(default);
        sut.IpAddress.ShouldBeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var userId = Guid.NewGuid();
        DateTimeOffset acceptedAt = DateTimeOffset.UtcNow;

        TestLegalAgreement sut = new()
        {
            UserId = userId,
            DocumentId = "privacy-policy",
            Version = "2.0.0",
            AcceptedAt = acceptedAt,
            IpAddress = "192.168.1.xxx"
        };

        sut.UserId.ShouldBe(userId);
        sut.DocumentId.ShouldBe("privacy-policy");
        sut.Version.ShouldBe("2.0.0");
        sut.AcceptedAt.ShouldBe(acceptedAt);
        sut.IpAddress.ShouldBe("192.168.1.xxx");
    }

    [Fact]
    public void IpAddress_CanBeNull_ForAnonymousConsent()
    {
        TestLegalAgreement sut = new()
        {
            UserId = Guid.NewGuid(),
            DocumentId = "terms",
            Version = "1.0.0",
            AcceptedAt = DateTimeOffset.UtcNow,
            IpAddress = null
        };

        sut.IpAddress.ShouldBeNull();
    }

    private sealed class TestLegalAgreement : LegalAgreementBase;
}
