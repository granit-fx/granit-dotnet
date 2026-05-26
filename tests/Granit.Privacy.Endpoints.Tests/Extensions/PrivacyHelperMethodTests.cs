using System.Security.Claims;
using Granit.Http.Cookies;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataExport;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Extensions;
using Granit.Privacy.Regulations;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Extensions;

public sealed class PrivacyHelperMethodTests
{
    // -------------------------------------------------------------------------
    // TryGetUserId
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetUserId_AuthenticatedWithValidGuid_ReturnsTrue()
    {
        var expected = Guid.NewGuid();
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns(expected.ToString());

        bool result = PrivacyEndpointRouteBuilderExtensions.TryGetUserId(currentUser, out Guid userId);

        result.ShouldBeTrue();
        userId.ShouldBe(expected);
    }

    [Fact]
    public void TryGetUserId_NotAuthenticated_ReturnsFalse()
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.IsAuthenticated.Returns(false);
        currentUser.UserId.Returns((string?)null);

        bool result = PrivacyEndpointRouteBuilderExtensions.TryGetUserId(currentUser, out Guid userId);

        result.ShouldBeFalse();
        userId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void TryGetUserId_AuthenticatedButNullUserId_ReturnsFalse()
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns((string?)null);

        bool result = PrivacyEndpointRouteBuilderExtensions.TryGetUserId(currentUser, out Guid userId);

        result.ShouldBeFalse();
        userId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void TryGetUserId_AuthenticatedButInvalidGuid_ReturnsFalse()
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns("not-a-guid");

        bool result = PrivacyEndpointRouteBuilderExtensions.TryGetUserId(currentUser, out Guid userId);

        result.ShouldBeFalse();
        userId.ShouldBe(Guid.Empty);
    }

    // -------------------------------------------------------------------------
    // TryGetUserIdOrNull
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetUserIdOrNull_AuthenticatedWithSubClaim_ReturnsGuid()
    {
        var expected = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            [new Claim("sub", expected.ToString())],
            "Bearer");
        httpContext.User = new ClaimsPrincipal(identity);

        Guid? result = PrivacyEndpointRouteBuilderExtensions.TryGetUserIdOrNull(httpContext);

        result.ShouldBe(expected);
    }

    [Fact]
    public void TryGetUserIdOrNull_AuthenticatedWithNameIdentifierClaim_ReturnsGuid()
    {
        var expected = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, expected.ToString())],
            "Bearer");
        httpContext.User = new ClaimsPrincipal(identity);

        Guid? result = PrivacyEndpointRouteBuilderExtensions.TryGetUserIdOrNull(httpContext);

        result.ShouldBe(expected);
    }

    [Fact]
    public void TryGetUserIdOrNull_Unauthenticated_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        Guid? result = PrivacyEndpointRouteBuilderExtensions.TryGetUserIdOrNull(httpContext);

        result.ShouldBeNull();
    }

    [Fact]
    public void TryGetUserIdOrNull_SubClaimNotValidGuid_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            [new Claim("sub", "not-a-guid")],
            "Bearer");
        httpContext.User = new ClaimsPrincipal(identity);

        Guid? result = PrivacyEndpointRouteBuilderExtensions.TryGetUserIdOrNull(httpContext);

        result.ShouldBeNull();
    }

    [Fact]
    public void TryGetUserIdOrNull_NoClaims_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity([], "Bearer");
        httpContext.User = new ClaimsPrincipal(identity);

        Guid? result = PrivacyEndpointRouteBuilderExtensions.TryGetUserIdOrNull(httpContext);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // UserNotAuthenticated
    // -------------------------------------------------------------------------

    [Fact]
    public void UserNotAuthenticated_ReturnsProblemHttpResult()
    {
        ProblemHttpResult result = PrivacyEndpointRouteBuilderExtensions.UserNotAuthenticated();

        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    // -------------------------------------------------------------------------
    // ResolveTenantId
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveTenantId_TenantAvailable_ReturnsId()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(tenantId);

        Guid? result = PrivacyEndpointRouteBuilderExtensions.ResolveTenantId(currentTenant);

        result.ShouldBe(tenantId);
    }

    [Fact]
    public void ResolveTenantId_TenantNotAvailable_ReturnsNull()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(false);

        Guid? result = PrivacyEndpointRouteBuilderExtensions.ResolveTenantId(currentTenant);

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveTenantId_TenantAvailableButIdNull_ReturnsNull()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns((Guid?)null);

        Guid? result = PrivacyEndpointRouteBuilderExtensions.ResolveTenantId(currentTenant);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // RegisterOptOutCookie
    // -------------------------------------------------------------------------

    [Fact]
    public void RegisterOptOutCookie_RegistersCookieWithCorrectProperties()
    {
        ICookieRegistry registry = Substitute.For<ICookieRegistry>();

        PrivacyEndpointRouteBuilderExtensions.RegisterOptOutCookie(registry);

        registry.Received(1).Register(Arg.Is<CookieDefinition>(c =>
            c.Name == "_optout_id" &&
            c.Category == CookieCategory.StrictlyNecessary &&
            c.IsHttpOnly));
    }

    // -------------------------------------------------------------------------
    // ResolveRegulationAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResolveRegulationAsync_ResolverNull_ReturnsEuGdpr()
    {
        string result = await PrivacyEndpointRouteBuilderExtensions
            .ResolveRegulationAsync(null, CancellationToken.None);

        result.ShouldBe("EU_GDPR");
    }

    [Fact]
    public async Task ResolveRegulationAsync_ResolverReturnsProfile_ReturnsRegulationValue()
    {
        IPrivacyRegulationResolver resolver = Substitute.For<IPrivacyRegulationResolver>();
        var profile = new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.UsCcpa,
            DisplayName = "California Consumer Privacy Act",
            JurisdictionCode = "US-CA",
            ConsentModel = ConsentModel.OptOut,
            AvailableLegalBases = [],
            SubjectAccessRequestDays = 45,
            DefaultDeletionGracePeriodDays = 0,
            MaxDeletionGracePeriodDays = 0,
            CookieConsentModel = ConsentModel.OptOut,
        };
        resolver.ResolveAsync(Arg.Any<CancellationToken>()).Returns(profile);

        string result = await PrivacyEndpointRouteBuilderExtensions
            .ResolveRegulationAsync(resolver, CancellationToken.None);

        result.ShouldBe("US_CCPA");
    }

    // -------------------------------------------------------------------------
    // MapExportStatus — all enum values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(ExportRequestState.Pending, "Pending")]
    [InlineData(ExportRequestState.Completed, "Completed")]
    [InlineData(ExportRequestState.PartiallyCompleted, "PartiallyCompleted")]
    [InlineData(ExportRequestState.TimedOut, "TimedOut")]
    public void MapExportStatus_MapsStateToExpectedString(ExportRequestState state, string expectedState)
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        DateTimeOffset? completedAt = state == ExportRequestState.Completed ? DateTimeOffset.UtcNow : null;
        Granit.Domain.ValueObjects.BlobReference? archiveRef = state == ExportRequestState.Completed
            ? Granit.Domain.ValueObjects.BlobReference.Create("blob-ref-123")
            : null;
        IReadOnlyList<string> missingProviders = state == ExportRequestState.PartiallyCompleted
            ? ["ProviderA"]
            : [];

        var status = new ExportRequestStatus(requestId, userId, state, requestedAt, completedAt, archiveRef, missingProviders);

        PrivacyExportStatusResponse response =
            PrivacyEndpointRouteBuilderExtensions.MapExportStatus(status);

        response.RequestId.ShouldBe(requestId);
        response.State.ShouldBe(expectedState);
        response.RequestedAt.ShouldBe(requestedAt);
        response.CompletedAt.ShouldBe(completedAt);
        response.ArchiveBlobReferenceId.ShouldBe(archiveRef);
        response.MissingProviders.ShouldBe(missingProviders);
    }

    // -------------------------------------------------------------------------
    // MapDeletionStatus — all enum values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(DeletionRequestState.Deferred, "Deferred")]
    [InlineData(DeletionRequestState.Executed, "Executed")]
    [InlineData(DeletionRequestState.Cancelled, "Cancelled")]
    public void MapDeletionStatus_MapsStateToExpectedString(DeletionRequestState state, string expectedState)
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        DateTimeOffset scheduledAt = requestedAt.AddDays(30);
        DateTimeOffset? cancelledAt = state == DeletionRequestState.Cancelled ? DateTimeOffset.UtcNow : null;
        DateTimeOffset? executedAt = state == DeletionRequestState.Executed ? DateTimeOffset.UtcNow : null;

        var status = new DeletionRequestStatus(
            requestId, userId, state, "User requested account deletion", requestedAt, scheduledAt,
            cancelledAt, executedAt, "EU_GDPR", null);

        PrivacyDeletionStatusResponse response =
            PrivacyEndpointRouteBuilderExtensions.MapDeletionStatus(status);

        response.RequestId.ShouldBe(requestId);
        response.State.ShouldBe(expectedState);
        response.Reason.ShouldBe("User requested account deletion");
        response.RequestedAt.ShouldBe(requestedAt);
        response.ScheduledDeletionAt.ShouldBe(scheduledAt);
        response.CancelledAt.ShouldBe(cancelledAt);
        response.ExecutedAt.ShouldBe(executedAt);
    }
}
