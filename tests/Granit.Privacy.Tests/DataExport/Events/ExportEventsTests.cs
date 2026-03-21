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

        PersonalDataRequestedEto sut = new(requestId, userId, requestedAt);

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

        PersonalDataRequestedEto a = new(requestId, userId, requestedAt);
        PersonalDataRequestedEto b = new(requestId, userId, requestedAt);

        a.ShouldBe(b);
    }

    [Fact]
    public void PersonalDataRequestedEto_Equality_DifferentValues_AreNotEqual()
    {
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;

        PersonalDataRequestedEto a = new(Guid.NewGuid(), Guid.NewGuid(), requestedAt);
        PersonalDataRequestedEto b = new(Guid.NewGuid(), Guid.NewGuid(), requestedAt);

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

        ExportCompletedEto sut = new(requestId, userId, "gdpr-export/123", true, missingProviders);

        sut.RequestId.ShouldBe(requestId);
        sut.UserId.ShouldBe(userId);
        sut.ArchiveBlobReferenceId.ShouldBe("gdpr-export/123");
        sut.IsPartial.ShouldBeTrue();
        sut.MissingProviders.Count.ShouldBe(2);
        sut.MissingProviders.ShouldContain("billing");
    }

    [Fact]
    public void ExportCompletedEto_FullExport_HasEmptyMissingProviders()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        ExportCompletedEto sut = new(requestId, userId, "gdpr-export/abc", false, []);

        sut.IsPartial.ShouldBeFalse();
        sut.MissingProviders.ShouldBeEmpty();
    }

    [Fact]
    public void ExportCompletedEto_Equality_SameValues_AreEqual()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        List<string> missing = ["x"];

        ExportCompletedEto a = new(requestId, userId, "ref", true, missing);
        ExportCompletedEto b = new(requestId, userId, "ref", true, missing);

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
