using Granit.Privacy.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Dtos;

public sealed class PrivacyDtoTests
{
    [Fact]
    public void PrivacyExportRequestResponse_HoldsProperties()
    {
        var requestId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        PrivacyExportRequestResponse response = new(requestId, now);

        response.RequestId.ShouldBe(requestId);
        response.RequestedAt.ShouldBe(now);
    }

    [Fact]
    public void PrivacyExportStatusResponse_HoldsAllProperties()
    {
        var requestId = Guid.NewGuid();
        DateTimeOffset requested = DateTimeOffset.UtcNow;
        DateTimeOffset completed = requested.AddMinutes(3);
        List<string> missing = ["provider-a"];

        PrivacyExportStatusResponse response = new(
            requestId, "Completed", requested, completed, "gdpr-export/123", missing);

        response.RequestId.ShouldBe(requestId);
        response.State.ShouldBe("Completed");
        response.RequestedAt.ShouldBe(requested);
        response.CompletedAt.ShouldBe(completed);
        response.ArchiveBlobReferenceId.ShouldBe("gdpr-export/123");
        response.MissingProviders.ShouldContain("provider-a");
    }

    [Fact]
    public void PrivacyLegalDocumentResponse_HoldsProperties()
    {
        PrivacyLegalDocumentResponse response = new("privacy-policy", "2.1.0", "Privacy Policy");

        response.DocumentId.ShouldBe("privacy-policy");
        response.CurrentVersion.ShouldBe("2.1.0");
        response.DisplayName.ShouldBe("Privacy Policy");
    }

    [Fact]
    public void PrivacyConsentStatusResponse_HoldsProperties()
    {
        DateTimeOffset accepted = DateTimeOffset.UtcNow;

        PrivacyConsentStatusResponse response = new("privacy-policy", "2.1.0", true, accepted);

        response.DocumentId.ShouldBe("privacy-policy");
        response.HasAcceptedLatest.ShouldBeTrue();
        response.LastAcceptedAt.ShouldBe(accepted);
    }

    [Fact]
    public void PrivacyUserAgreementResponse_HoldsProperties()
    {
        var id = Guid.NewGuid();
        DateTimeOffset accepted = DateTimeOffset.UtcNow;

        PrivacyUserAgreementResponse response = new(id, "terms", "1.0", accepted, false);

        response.Id.ShouldBe(id);
        response.DocumentId.ShouldBe("terms");
        response.Version.ShouldBe("1.0");
        response.AcceptedAt.ShouldBe(accepted);
        response.IsLatest.ShouldBeFalse();
    }

    [Fact]
    public void PrivacyDeletionRequest_HoldsReason()
    {
        PrivacyDeletionRequest request = new("Account closure");

        request.Reason.ShouldBe("Account closure");
    }

    [Fact]
    public void PrivacyAcceptAgreementRequest_HoldsProperties()
    {
        PrivacyAcceptAgreementRequest request = new("privacy-policy", "2.1.0");

        request.DocumentId.ShouldBe("privacy-policy");
        request.Version.ShouldBe("2.1.0");
    }
}
