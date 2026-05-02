using System.Net;
using System.Net.Http.Json;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataExport;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Domain;
using Granit.Privacy.Options;
using Granit.Privacy.OptOut;
using Granit.Privacy.ProcessingPurposes;
using Granit.Privacy.Regulations;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Integration;

public sealed class PrivacyEndpointsIntegrationTests : IAsyncLifetime
{
    private PrivacyEndpointsTestServer _server = null!;

    public async ValueTask InitializeAsync() =>
        _server = await PrivacyEndpointsTestServer.CreateAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() =>
        await _server.DisposeAsync().ConfigureAwait(false);

    // -------------------------------------------------------------------------
    // Regulation endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetRegulation_ReturnsProfile()
    {
        PrivacyRegulationProfile profile = new()
        {
            Regulation = PrivacyRegulation.EuGdpr,
            DisplayName = "EU General Data Protection Regulation",
            JurisdictionCode = "EU",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases = [LegalBasis.Consent, LegalBasis.Contract],
            SubjectAccessRequestDays = 30,
            DefaultDeletionGracePeriodDays = 14,
            MaxDeletionGracePeriodDays = 30,
            CookieConsentModel = ConsentModel.OptIn,
        };
        _server.RegulationResolver
            .ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/privacy/regulation", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PrivacyRegulationProfileResponse? result = await response.Content
            .ReadFromJsonAsync<PrivacyRegulationProfileResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Regulation.ShouldBe("EU_GDPR");
        result.DisplayName.ShouldBe("EU General Data Protection Regulation");
        result.JurisdictionCode.ShouldBe("EU");
        result.ConsentModel.ShouldBe("OptIn");
        result.SubjectAccessRequestDays.ShouldBe(30);
    }

    // -------------------------------------------------------------------------
    // Processing purposes endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPurposes_ReturnsPurposeList()
    {
        _server.PurposeRegistry.GetAll().Returns(
        [
            new ProcessingPurposeDefinition(
                "marketing", "Marketing", "Marketing emails",
                "Consent", true, "contact-data"),
        ]);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/privacy/purposes", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<PrivacyProcessingPurposeResponse>? result = await response.Content
            .ReadFromJsonAsync<List<PrivacyProcessingPurposeResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].PurposeId.ShouldBe("marketing");
        result[0].RequiresExplicitConsent.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Export endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostExport_Authenticated_Returns202WithRequestId()
    {
        var requestId = Guid.NewGuid();
        _server.GuidGenerator.Create().Returns(requestId);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            "/privacy/exports", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        PrivacyExportRequestResponse? result = await response.Content
            .ReadFromJsonAsync<PrivacyExportRequestResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(requestId);
        result.RequestedAt.ShouldBe(PrivacyEndpointsTestServer.FixedNow);

        await _server.ExportWriter.Received(1).RecordRequestAsync(
            requestId, PrivacyEndpointsTestServer.TestUserId,
            PrivacyEndpointsTestServer.FixedNow,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExportStatus_ExistingRequest_Returns200()
    {
        var requestId = Guid.NewGuid();
        ExportRequestStatus status = new(
            requestId,
            PrivacyEndpointsTestServer.TestUserId,
            ExportRequestState.Pending,
            PrivacyEndpointsTestServer.FixedNow,
            null, null, []);

        _server.ExportReader
            .GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(status);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            $"/privacy/exports/{requestId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PrivacyExportStatusResponse? result = await response.Content
            .ReadFromJsonAsync<PrivacyExportStatusResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(requestId);
        result.State.ShouldBe("Pending");
    }

    [Fact]
    public async Task GetExportStatus_NonExistent_Returns404()
    {
        var requestId = Guid.NewGuid();
        _server.ExportReader
            .GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns((ExportRequestStatus?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            $"/privacy/exports/{requestId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExportStatus_OwnedByOtherUser_Returns404()
    {
        var requestId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        ExportRequestStatus status = new(
            requestId, otherUserId, ExportRequestState.Pending,
            PrivacyEndpointsTestServer.FixedNow, null, null, []);

        _server.ExportReader
            .GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(status);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            $"/privacy/exports/{requestId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyExports_ReturnsUserExportList()
    {
        var requestId = Guid.NewGuid();
        ExportRequestStatus status = new(
            requestId,
            PrivacyEndpointsTestServer.TestUserId,
            ExportRequestState.Completed,
            PrivacyEndpointsTestServer.FixedNow,
            PrivacyEndpointsTestServer.FixedNow.AddHours(1),
            "archive-ref-123",
            []);

        _server.ExportReader
            .GetByUserAsync(PrivacyEndpointsTestServer.TestUserId, Arg.Any<CancellationToken>())
            .Returns(new[] { status });

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/privacy/exports", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<PrivacyExportStatusResponse>? result = await response.Content
            .ReadFromJsonAsync<List<PrivacyExportStatusResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].State.ShouldBe("Completed");
        result[0].ArchiveBlobReferenceId!.Value.ShouldBe("archive-ref-123");
    }

    // -------------------------------------------------------------------------
    // Deletion endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostDeletion_Immediate_Returns202()
    {
        var requestId = Guid.NewGuid();
        _server.GuidGenerator.Create().Returns(requestId);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/deletions",
            new PrivacyDeletionRequest("Account closure", false),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await _server.DeletionWriter.Received(1).RecordImmediateDeletionAsync(
            requestId, PrivacyEndpointsTestServer.TestUserId, "Account closure",
            PrivacyEndpointsTestServer.FixedNow,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostDeletion_Deferred_Returns202WithScheduledDate()
    {
        var requestId = Guid.NewGuid();
        _server.GuidGenerator.Create().Returns(requestId);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/deletions",
            new PrivacyDeletionRequest("Withdrawal of consent", true),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        PrivacyDeletionRequestResponse? result = await response.Content
            .ReadFromJsonAsync<PrivacyDeletionRequestResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(requestId);

        // Default grace period is 30 days (GranitPrivacyOptions defaults)
        int expectedGraceDays = new GranitPrivacyOptions().DefaultGracePeriodDays;
        result.ScheduledDeletionAt.ShouldBe(PrivacyEndpointsTestServer.FixedNow.AddDays(expectedGraceDays));
    }

    [Fact]
    public async Task PostDeletion_ExistingDeferredRequest_Returns409()
    {
        DeletionRequestStatus existing = new(
            Guid.NewGuid(),
            PrivacyEndpointsTestServer.TestUserId,
            DeletionRequestState.Deferred,
            "Previous request",
            PrivacyEndpointsTestServer.FixedNow.AddDays(-5),
            PrivacyEndpointsTestServer.FixedNow.AddDays(25),
            null, null, null, null);

        _server.DeletionReader
            .GetByUserAsync(PrivacyEndpointsTestServer.TestUserId, Arg.Any<CancellationToken>())
            .Returns(new[] { existing });

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/deletions",
            new PrivacyDeletionRequest("New request", false),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostDeletion_EmptyReason_Returns422()
    {
        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/deletions",
            new PrivacyDeletionRequest("", false),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CancelDeletion_DeferredRequest_Returns200()
    {
        var requestId = Guid.NewGuid();
        DeletionRequestStatus status = new(
            requestId,
            PrivacyEndpointsTestServer.TestUserId,
            DeletionRequestState.Deferred,
            "Withdrawal of consent",
            PrivacyEndpointsTestServer.FixedNow,
            PrivacyEndpointsTestServer.FixedNow.AddDays(30),
            null, null, null, null);

        _server.DeletionReader
            .GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(status);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            $"/privacy/deletions/{requestId}/cancel", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelDeletion_NonExistent_Returns404()
    {
        var requestId = Guid.NewGuid();
        _server.DeletionReader
            .GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns((DeletionRequestStatus?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            $"/privacy/deletions/{requestId}/cancel", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelDeletion_AlreadyExecuted_Returns409()
    {
        var requestId = Guid.NewGuid();
        DeletionRequestStatus status = new(
            requestId,
            PrivacyEndpointsTestServer.TestUserId,
            DeletionRequestState.Executed,
            "Withdrawal of consent",
            PrivacyEndpointsTestServer.FixedNow.AddDays(-10),
            PrivacyEndpointsTestServer.FixedNow.AddDays(-5),
            null,
            PrivacyEndpointsTestServer.FixedNow.AddDays(-5),
            null, null);

        _server.DeletionReader
            .GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(status);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            $"/privacy/deletions/{requestId}/cancel", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetDeletionStatus_ExistingRequest_Returns200()
    {
        var requestId = Guid.NewGuid();
        DeletionRequestStatus status = new(
            requestId,
            PrivacyEndpointsTestServer.TestUserId,
            DeletionRequestState.Deferred,
            "Account closure",
            PrivacyEndpointsTestServer.FixedNow,
            PrivacyEndpointsTestServer.FixedNow.AddDays(30),
            null, null, null, null);

        _server.DeletionReader
            .GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(status);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            $"/privacy/deletions/{requestId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PrivacyDeletionStatusResponse? result = await response.Content
            .ReadFromJsonAsync<PrivacyDeletionStatusResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(requestId);
        result.State.ShouldBe("Deferred");
        result.Reason.ShouldBe("Account closure");
    }

    [Fact]
    public async Task GetDeletionStatus_NonExistent_Returns404()
    {
        var requestId = Guid.NewGuid();
        _server.DeletionReader
            .GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns((DeletionRequestStatus?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            $"/privacy/deletions/{requestId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyDeletions_ReturnsList()
    {
        var requestId = Guid.NewGuid();
        DeletionRequestStatus status = new(
            requestId,
            PrivacyEndpointsTestServer.TestUserId,
            DeletionRequestState.Cancelled,
            "Changed my mind",
            PrivacyEndpointsTestServer.FixedNow.AddDays(-10),
            PrivacyEndpointsTestServer.FixedNow.AddDays(20),
            PrivacyEndpointsTestServer.FixedNow.AddDays(-5),
            null, null, null);

        _server.DeletionReader
            .GetByUserAsync(PrivacyEndpointsTestServer.TestUserId, Arg.Any<CancellationToken>())
            .Returns(new[] { status });

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/privacy/deletions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<PrivacyDeletionStatusResponse>? result = await response.Content
            .ReadFromJsonAsync<List<PrivacyDeletionStatusResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].State.ShouldBe("Cancelled");
    }

    // -------------------------------------------------------------------------
    // Agreement endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocuments_ReturnsDocumentList()
    {
        _server.DocumentRegistry.GetAll().Returns(
        [
            new LegalDocumentDefinition("privacy-policy", "2.1.0", "Privacy Policy"),
            new LegalDocumentDefinition("terms-of-service", "1.0.0", "Terms of Service"),
        ]);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/privacy/agreements/documents", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<PrivacyLegalDocumentResponse>? result = await response.Content
            .ReadFromJsonAsync<List<PrivacyLegalDocumentResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].DocumentId.ShouldBe("privacy-policy");
        result[0].CurrentVersion.ShouldBe("2.1.0");
    }

    [Fact]
    public async Task AcceptAgreement_ValidRequest_Returns201()
    {
        LegalDocumentDefinition document = new("privacy-policy", "2.1.0", "Privacy Policy");
        _server.DocumentRegistry.GetDefinition("privacy-policy").Returns(document);
        _server.AgreementChecker
            .HasAcceptedLatestAsync(PrivacyEndpointsTestServer.TestUserId, "privacy-policy", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/agreements/accept",
            new PrivacyAcceptAgreementRequest("privacy-policy", "2.1.0"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        await _server.AgreementStoreWriter.Received(1).RecordConsentAsync(
            PrivacyEndpointsTestServer.TestUserId,
            "privacy-policy", "2.1.0",
            Arg.Any<string?>(),
            PrivacyEndpointsTestServer.FixedNow,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptAgreement_AlreadyAccepted_Returns409()
    {
        LegalDocumentDefinition document = new("privacy-policy", "2.1.0", "Privacy Policy");
        _server.DocumentRegistry.GetDefinition("privacy-policy").Returns(document);
        _server.AgreementChecker
            .HasAcceptedLatestAsync(PrivacyEndpointsTestServer.TestUserId, "privacy-policy", Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/agreements/accept",
            new PrivacyAcceptAgreementRequest("privacy-policy", "2.1.0"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AcceptAgreement_VersionMismatch_Returns422()
    {
        LegalDocumentDefinition document = new("privacy-policy", "2.1.0", "Privacy Policy");
        _server.DocumentRegistry.GetDefinition("privacy-policy").Returns(document);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/agreements/accept",
            new PrivacyAcceptAgreementRequest("privacy-policy", "1.0.0"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AcceptAgreement_UnknownDocument_Returns404()
    {
        _server.DocumentRegistry.GetDefinition("unknown-doc").Returns((LegalDocumentDefinition?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/agreements/accept",
            new PrivacyAcceptAgreementRequest("unknown-doc", "1.0.0"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptAgreement_EmptyDocumentId_Returns422()
    {
        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/agreements/accept",
            new PrivacyAcceptAgreementRequest("", "1.0.0"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetConsentStatus_ReturnsStatusPerDocument()
    {
        LegalDocumentDefinition document = new("privacy-policy", "2.1.0", "Privacy Policy");
        _server.DocumentRegistry.GetAll().Returns(new[] { document });
        _server.AgreementChecker
            .HasAcceptedLatestAsync(PrivacyEndpointsTestServer.TestUserId, "privacy-policy", Arg.Any<CancellationToken>())
            .Returns(true);

        TestLegalAgreement agreement = new()
        {
            Id = Guid.NewGuid(),
            UserId = PrivacyEndpointsTestServer.TestUserId,
            DocumentId = "privacy-policy",
            Version = "2.1.0",
            AcceptedAt = PrivacyEndpointsTestServer.FixedNow.AddDays(-5),
        };
        _server.AgreementStoreReader
            .FindLatestAsync(PrivacyEndpointsTestServer.TestUserId, "privacy-policy", Arg.Any<CancellationToken>())
            .Returns(agreement);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/privacy/agreements/status", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<PrivacyConsentStatusResponse>? result = await response.Content
            .ReadFromJsonAsync<List<PrivacyConsentStatusResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].DocumentId.ShouldBe("privacy-policy");
        result[0].HasAcceptedLatest.ShouldBeTrue();
        result[0].LastAcceptedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAgreementHistory_ReturnsHistoryList()
    {
        LegalDocumentDefinition document = new("privacy-policy", "2.1.0", "Privacy Policy");
        _server.DocumentRegistry.GetDefinition("privacy-policy").Returns(document);

        TestLegalAgreement agreement = new()
        {
            Id = Guid.NewGuid(),
            UserId = PrivacyEndpointsTestServer.TestUserId,
            DocumentId = "privacy-policy",
            Version = "2.1.0",
            AcceptedAt = PrivacyEndpointsTestServer.FixedNow,
        };
        _server.AgreementChecker
            .GetUserAgreementsAsync(PrivacyEndpointsTestServer.TestUserId, Arg.Any<CancellationToken>())
            .Returns(new LegalAgreementBase[] { agreement });

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/privacy/agreements/history", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<PrivacyUserAgreementResponse>? result = await response.Content
            .ReadFromJsonAsync<List<PrivacyUserAgreementResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].DocumentId.ShouldBe("privacy-policy");
        result[0].Version.ShouldBe("2.1.0");
        result[0].IsLatest.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Opt-out endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostOptOut_Authenticated_Returns201()
    {
        _server.OptOutReader
            .IsOptedOutAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            "/privacy/opt-out", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        PrivacyOptOutStatusResponse? result = await response.Content
            .ReadFromJsonAsync<PrivacyOptOutStatusResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsOptedOut.ShouldBeTrue();
    }

    [Fact]
    public async Task GetOptOutStatus_NotOptedOut_ReturnsFalse()
    {
        _server.OptOutReader
            .IsOptedOutAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/privacy/opt-out/status", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PrivacyOptOutStatusResponse? result = await response.Content
            .ReadFromJsonAsync<PrivacyOptOutStatusResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsOptedOut.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Concrete LegalAgreementBase for test scenarios
    // -------------------------------------------------------------------------

    private sealed class TestLegalAgreement : LegalAgreementBase;
}
