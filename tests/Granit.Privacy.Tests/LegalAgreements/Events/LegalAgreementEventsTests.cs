using Granit.Privacy.LegalAgreements.Events;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.LegalAgreements.Events;

public sealed class LegalAgreementEventsTests
{
    // ──── LegalAgreementAcceptedEto ────

    [Fact]
    public void LegalAgreementAcceptedEto_Constructor_SetsAllProperties()
    {
        var userId = Guid.NewGuid();
        DateTimeOffset acceptedAt = DateTimeOffset.UtcNow;

        LegalAgreementAcceptedEto sut = new(userId, "privacy-policy", "2.0.0", acceptedAt);

        sut.UserId.ShouldBe(userId);
        sut.DocumentId.ShouldBe("privacy-policy");
        sut.Version.ShouldBe("2.0.0");
        sut.AcceptedAt.ShouldBe(acceptedAt);
    }

    [Fact]
    public void LegalAgreementAcceptedEto_Equality_SameValues_AreEqual()
    {
        var userId = Guid.NewGuid();
        DateTimeOffset acceptedAt = DateTimeOffset.UtcNow;

        LegalAgreementAcceptedEto a = new(userId, "privacy-policy", "2.0.0", acceptedAt);
        LegalAgreementAcceptedEto b = new(userId, "privacy-policy", "2.0.0", acceptedAt);

        a.ShouldBe(b);
    }

    [Fact]
    public void LegalAgreementAcceptedEto_Equality_DifferentValues_AreNotEqual()
    {
        DateTimeOffset acceptedAt = DateTimeOffset.UtcNow;

        LegalAgreementAcceptedEto a = new(Guid.NewGuid(), "privacy-policy", "1.0.0", acceptedAt);
        LegalAgreementAcceptedEto b = new(Guid.NewGuid(), "terms", "2.0.0", acceptedAt);

        a.ShouldNotBe(b);
    }

    // ──── LegalAgreementObsoleteEto ────

    [Fact]
    public void LegalAgreementObsoleteEto_Constructor_SetsAllProperties()
    {
        LegalAgreementObsoleteEto sut = new("privacy-policy", "1.0.0", "2.0.0");

        sut.DocumentId.ShouldBe("privacy-policy");
        sut.OldVersion.ShouldBe("1.0.0");
        sut.NewVersion.ShouldBe("2.0.0");
    }

    [Fact]
    public void LegalAgreementObsoleteEto_Equality_SameValues_AreEqual()
    {
        LegalAgreementObsoleteEto a = new("privacy-policy", "1.0.0", "2.0.0");
        LegalAgreementObsoleteEto b = new("privacy-policy", "1.0.0", "2.0.0");

        a.ShouldBe(b);
    }

    [Fact]
    public void LegalAgreementObsoleteEto_Equality_DifferentValues_AreNotEqual()
    {
        LegalAgreementObsoleteEto a = new("privacy-policy", "1.0.0", "2.0.0");
        LegalAgreementObsoleteEto b = new("terms", "1.0.0", "3.0.0");

        a.ShouldNotBe(b);
    }
}
