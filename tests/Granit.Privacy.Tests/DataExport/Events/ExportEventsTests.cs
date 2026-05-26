using Granit.Domain.ValueObjects;
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

    // ──── PersonalDataPreparedEto (Takeout-style: FragmentKind + EntryPath + IntegrityTag) ────

    [Fact]
    public void PersonalDataPreparedEto_Constructor_SetsAllProperties()
    {
        var requestId = Guid.NewGuid();
        var blob = BlobReference.Create(Guid.NewGuid().ToString());

        PersonalDataPreparedEto sut = new(
            RequestId: requestId,
            ProviderName: "patients",
            FragmentKind: "staged",
            SourceContainer: "gdpr-exports",
            BlobReferenceId: blob,
            EntryPath: "patients.json",
            ContentType: "application/json",
            IntegrityTag: "v1:abcdef");

        sut.RequestId.ShouldBe(requestId);
        sut.ProviderName.ShouldBe("patients");
        sut.FragmentKind.ShouldBe("staged");
        sut.SourceContainer.ShouldBe("gdpr-exports");
        sut.BlobReferenceId.ShouldBe(blob);
        sut.EntryPath.ShouldBe("patients.json");
        sut.ContentType.ShouldBe("application/json");
        sut.IntegrityTag.ShouldBe("v1:abcdef");
    }

    [Fact]
    public void PersonalDataPreparedEto_Equality_SameValues_AreEqual()
    {
        var requestId = Guid.NewGuid();
        var blob = BlobReference.Create("blob-1");

        PersonalDataPreparedEto a = new(requestId, "p", "staged", "gdpr-exports", blob, "p.json", "application/json", "v1:t");
        PersonalDataPreparedEto b = new(requestId, "p", "staged", "gdpr-exports", blob, "p.json", "application/json", "v1:t");

        a.ShouldBe(b);
    }

    [Fact]
    public void PersonalDataPreparedEto_DifferentFragmentKind_AreNotEqual()
    {
        var requestId = Guid.NewGuid();
        var blob = BlobReference.Create("blob-1");

        PersonalDataPreparedEto staged = new(requestId, "p", "staged", "gdpr-exports", blob, "p.json", "application/json", "v1:t");
        PersonalDataPreparedEto passthrough = new(requestId, "p", "passthrough", "gdpr-exports", blob, "p.json", "application/json", "v1:t");

        staged.ShouldNotBe(passthrough);
    }

    // ──── ExportCompletedEto ────

    [Fact]
    public void ExportCompletedEto_Constructor_SetsAllProperties()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        List<string> missingProviders = ["billing", "appointments"];

        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        ExportCompletedEto sut = new(requestId, userId, BlobReference.Create("gdpr-export/123"), true, missingProviders, [], "EU_GDPR", requestedAt);

        sut.RequestId.ShouldBe(requestId);
        sut.UserId.ShouldBe(userId);
        sut.ArchiveBlobReferenceId.Value.ShouldBe("gdpr-export/123");
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
        var blob = BlobReference.Create("blob-1");

        List<ReceivedFragment> fragments =
        [
            new("identity", "staged", "gdpr-exports", blob, "identity.json", "application/json", "v1:t"),
        ];

        ExportCompletedEto sut = new(requestId, userId, BlobReference.Create("gdpr-export/abc"), false, [], fragments, "EU_GDPR", DateTimeOffset.UtcNow);

        sut.IsPartial.ShouldBeFalse();
        sut.MissingProviders.ShouldBeEmpty();
        sut.Fragments.Count.ShouldBe(1);
        sut.Fragments[0].EntryPath.ShouldBe("identity.json");
        sut.Fragments[0].FragmentKind.ShouldBe("staged");
    }

    [Fact]
    public void ExportCompletedEto_Equality_SameValues_AreEqual()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        List<string> missing = ["x"];

        List<ReceivedFragment> fragments = [];
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        ExportCompletedEto a = new(requestId, userId, BlobReference.Create("ref"), true, missing, fragments, "EU_GDPR", requestedAt);
        ExportCompletedEto b = new(requestId, userId, BlobReference.Create("ref"), true, missing, fragments, "EU_GDPR", requestedAt);

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
