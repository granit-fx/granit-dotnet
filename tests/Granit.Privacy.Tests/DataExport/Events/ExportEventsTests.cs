using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport.Events;

public sealed class ExportEventsTests
{
    // ──── PersonalDataRequestedEto ────

    [Fact]
    public void PersonalDataRequestedEto_Constructor_SetsAllProperties()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;

        PersonalDataRequestedEto sut = new(requestId, userId, requestedAt, "EU_GDPR");

        sut.RequestId.ShouldBe(requestId);
        sut.UserId.ShouldBe(userId);
        sut.RequestedAt.ShouldBe(requestedAt);
    }

    [Fact]
    public void PersonalDataRequestedEto_Equality_SameValues_AreEqual()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;

        PersonalDataRequestedEto a = new(requestId, userId, requestedAt, "EU_GDPR");
        PersonalDataRequestedEto b = new(requestId, userId, requestedAt, "EU_GDPR");

        a.ShouldBe(b);
    }

    [Fact]
    public void PersonalDataRequestedEto_Equality_DifferentValues_AreNotEqual()
    {
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;

        PersonalDataRequestedEto a = new(Guid.NewGuid(), Guid.NewGuid(), requestedAt, "EU_GDPR");
        PersonalDataRequestedEto b = new(Guid.NewGuid(), Guid.NewGuid(), requestedAt, "EU_GDPR");

        a.ShouldNotBe(b);
    }

    // ──── PersonalDataPreparedEto ────

    [Fact]
    public void PersonalDataPreparedEto_Constructor_SetsAllProperties()
    {
        var requestId = Guid.NewGuid();

        PersonalDataPreparedEto sut = new(requestId, "patients", "blob-ref-1", "application/json");

        sut.RequestId.ShouldBe(requestId);
        sut.ProviderName.ShouldBe("patients");
        sut.BlobReferenceId.ShouldBe("blob-ref-1");
        sut.ContentType.ShouldBe("application/json");
    }

    [Fact]
    public void PersonalDataPreparedEto_Equality_SameValues_AreEqual()
    {
        var requestId = Guid.NewGuid();

        PersonalDataPreparedEto a = new(requestId, "p", "blob-1", "application/json");
        PersonalDataPreparedEto b = new(requestId, "p", "blob-1", "application/json");

        a.ShouldBe(b);
    }

    // ──── ExportCompletedEto ────

    [Fact]
    public void ExportCompletedEto_Constructor_SetsAllProperties()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        List<string> missingProviders = ["billing", "appointments"];

        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        ExportCompletedEto sut = new(requestId, userId, "gdpr-export/123", true, missingProviders, [], "EU_GDPR", requestedAt);

        sut.RequestId.ShouldBe(requestId);
        sut.UserId.ShouldBe(userId);
        sut.ArchiveBlobReferenceId.ShouldBe("gdpr-export/123");
        sut.IsPartial.ShouldBeTrue();
        sut.MissingProviders.Count.ShouldBe(2);
        sut.MissingProviders.ShouldContain("billing");
        sut.Fragments.ShouldBeEmpty();
        sut.Regulation.ShouldBe("EU_GDPR");
        sut.RequestedAt.ShouldBe(requestedAt);
    }

    [Fact]
    public void ExportCompletedEto_FullExport_HasEmptyMissingProviders()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        List<ReceivedFragment> fragments = [new("identity", "blob-1", "application/json")];

        ExportCompletedEto sut = new(requestId, userId, "gdpr-export/abc", false, [], fragments, "EU_GDPR", DateTimeOffset.UtcNow);

        sut.IsPartial.ShouldBeFalse();
        sut.MissingProviders.ShouldBeEmpty();
        sut.Fragments.Count.ShouldBe(1);
    }

    [Fact]
    public void ExportCompletedEto_Equality_SameValues_AreEqual()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        List<string> missing = ["x"];

        List<ReceivedFragment> fragments = [];
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        ExportCompletedEto a = new(requestId, userId, "ref", true, missing, fragments, "EU_GDPR", requestedAt);
        ExportCompletedEto b = new(requestId, userId, "ref", true, missing, fragments, "EU_GDPR", requestedAt);

        a.ShouldBe(b);
    }

    // ──── ExportTimedOutEvent ────

    [Fact]
    public void ExportTimedOutEvent_Constructor_SetsRequestId()
    {
        var requestId = Guid.NewGuid();

        ExportTimedOutEvent sut = new(requestId);

        sut.RequestId.ShouldBe(requestId);
    }

    [Fact]
    public void ExportTimedOutEvent_Equality_SameValues_AreEqual()
    {
        var requestId = Guid.NewGuid();

        ExportTimedOutEvent a = new(requestId);
        ExportTimedOutEvent b = new(requestId);

        a.ShouldBe(b);
    }

    [Fact]
    public void ExportTimedOutEvent_Equality_DifferentValues_AreNotEqual()
    {
        ExportTimedOutEvent a = new(Guid.NewGuid());
        ExportTimedOutEvent b = new(Guid.NewGuid());

        a.ShouldNotBe(b);
    }
}
