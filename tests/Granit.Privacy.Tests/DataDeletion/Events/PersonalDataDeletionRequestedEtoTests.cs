using Granit.Privacy.DataDeletion.Events;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataDeletion.Events;

public sealed class PersonalDataDeletionRequestedEtoTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;

        var sut = new PersonalDataDeletionRequestedEto(
            requestId,
            userId,
            "dpo@example.com",
            requestedAt,
            "GDPR Art. 17 request",
            "EU_GDPR");

        sut.RequestId.ShouldBe(requestId);
        sut.UserId.ShouldBe(userId);
        sut.RequestedBy.ShouldBe("dpo@example.com");
        sut.RequestedAt.ShouldBe(requestedAt);
        sut.Reason.ShouldBe("GDPR Art. 17 request");
        sut.Regulation.ShouldBe("EU_GDPR");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;

        var a = new PersonalDataDeletionRequestedEto(requestId, userId, "admin", requestedAt, "reason", "EU_GDPR");
        var b = new PersonalDataDeletionRequestedEto(requestId, userId, "admin", requestedAt, "reason", "EU_GDPR");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;

        PersonalDataDeletionRequestedEto a = new(Guid.NewGuid(), Guid.NewGuid(), "a", requestedAt, "r1", "EU_GDPR");
        PersonalDataDeletionRequestedEto b = new(Guid.NewGuid(), Guid.NewGuid(), "b", requestedAt, "r2", "BR_LGPD");

        a.ShouldNotBe(b);
    }
}
