using System.Net;
using System.Net.Http.Json;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Endpoints.Dtos;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Integration;

public sealed class PrivacyExportOnBehalfOfEndpointTests
{
    private static readonly Guid OtherSubjectId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task PostOnBehalfOf_PublishesSagaEventWithSubjectAsUserId()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        HttpResponseMessage response = await server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/exports/on-behalf-of",
            new PrivacyExportOnBehalfOfRequest(SubjectUserId: OtherSubjectId),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await server.EventBus.Received(1).PublishAsync(
            Arg.Is<PersonalDataRequestedEto>(e => e.UserId == OtherSubjectId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostOnBehalfOf_AuditCarriesCallerAndSubjectDistinctly()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        HttpResponseMessage response = await server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/exports/on-behalf-of",
            new PrivacyExportOnBehalfOfRequest(SubjectUserId: OtherSubjectId),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await server.AuditWriter.Received(1).WriteExportRequestedAsync(
            Arg.Is<PrivacyExportRequestedAudit>(a =>
                a.CallerUserId == PrivacyEndpointsTestServer.TestUserId
                && a.SubjectUserId == OtherSubjectId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostOnBehalfOf_RecordsTrackerEntryAgainstSubjectUserId()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        HttpResponseMessage response = await server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/exports/on-behalf-of",
            new PrivacyExportOnBehalfOfRequest(SubjectUserId: OtherSubjectId),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await server.ExportWriter.Received(1).RecordRequestAsync(
            Arg.Any<Guid>(),
            OtherSubjectId,
            PrivacyEndpointsTestServer.TestUserId,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostOnBehalfOf_SubjectAbsentFromTenant_Returns404_AndDoesNotFireSaga()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        // Cross-tenant probe: validator says the subject does not exist inside
        // the operator's tenant. The endpoint must answer with 404 (NOT 403) so
        // existence cannot be probed across tenant boundaries, and the saga
        // event + audit row must NOT fire.
        server.SubjectValidator
            .SubjectExistsInCurrentTenantAsync(OtherSubjectId, Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/exports/on-behalf-of",
            new PrivacyExportOnBehalfOfRequest(SubjectUserId: OtherSubjectId),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await server.EventBus.DidNotReceive().PublishAsync(
            Arg.Any<PersonalDataRequestedEto>(),
            Arg.Any<CancellationToken>());
        await server.AuditWriter.DidNotReceive().WriteExportRequestedAsync(
            Arg.Any<PrivacyExportRequestedAudit>(),
            Arg.Any<CancellationToken>());
        await server.ExportWriter.DidNotReceive().RecordRequestAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostOnBehalfOf_EmptySubjectId_Returns422ValidationProblem()
    {
        // FluentValidationAutoEndpointFilter surfaces validation failures as 422
        // UnprocessableEntity — handled before the handler runs, so no saga fires.
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        HttpResponseMessage response = await server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/exports/on-behalf-of",
            new PrivacyExportOnBehalfOfRequest(SubjectUserId: Guid.Empty),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await server.EventBus.DidNotReceive().PublishAsync(
            Arg.Any<PersonalDataRequestedEto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostOnBehalfOf_Anonymous_Returns401()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        HttpResponseMessage response = await server.AnonymousClient.PostAsJsonAsync(
            "/privacy/exports/on-behalf-of",
            new PrivacyExportOnBehalfOfRequest(SubjectUserId: OtherSubjectId),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
