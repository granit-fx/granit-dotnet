// =============================================================================
// Tests - OutgoingContextMiddleware (additional coverage)
// =============================================================================
// Covers FirstName/LastName headers, ActorKind header, ApiKeyId header, and
// edge cases not covered by the existing OutgoingContextMiddlewareTests.
// =============================================================================

using Granit.MultiTenancy;
using Granit.Users;
using Granit.Wolverine.Middleware;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class OutgoingContextMiddlewareAdditionalTests
{
    private static Envelope CreateEnvelope() => new();

    // -------------------------------------------------------------------------
    // FirstName / LastName headers
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithAuthenticatedUserAndFirstName_SetsFirstNameHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(true);
        userService.UserId.Returns("user-1");
        userService.FirstName.Returns("Jean");
        userService.LastName.Returns((string?)null);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers[OutgoingContextMiddleware.UserFirstNameHeader].ShouldBe("Jean");
        envelope.Headers.ContainsKey(OutgoingContextMiddleware.UserLastNameHeader).ShouldBeFalse();
    }

    [Fact]
    public void Before_WithAuthenticatedUserAndLastName_SetsLastNameHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(true);
        userService.UserId.Returns("user-1");
        userService.FirstName.Returns((string?)null);
        userService.LastName.Returns("Dupont");

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.UserFirstNameHeader).ShouldBeFalse();
        envelope.Headers[OutgoingContextMiddleware.UserLastNameHeader].ShouldBe("Dupont");
    }

    [Fact]
    public void Before_WithAuthenticatedUserAndBothNames_SetsBothHeaders()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(true);
        userService.UserId.Returns("user-1");
        userService.FirstName.Returns("Jean");
        userService.LastName.Returns("Dupont");

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers[OutgoingContextMiddleware.UserFirstNameHeader].ShouldBe("Jean");
        envelope.Headers[OutgoingContextMiddleware.UserLastNameHeader].ShouldBe("Dupont");
    }

    [Fact]
    public void Before_WithEmptyFirstName_DoesNotSetFirstNameHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(true);
        userService.UserId.Returns("user-1");
        userService.FirstName.Returns(string.Empty);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.UserFirstNameHeader).ShouldBeFalse();
    }

    [Fact]
    public void Before_WithEmptyLastName_DoesNotSetLastNameHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(true);
        userService.UserId.Returns("user-1");
        userService.LastName.Returns(string.Empty);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.UserLastNameHeader).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // ActorKind header
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithActorKindUser_DoesNotSetActorKindHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(false);
        userService.ActorKind.Returns(ActorKind.User);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.ActorKindHeader).ShouldBeFalse();
    }

    [Fact]
    public void Before_WithActorKindExternalSystem_SetsActorKindHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(false);
        userService.ActorKind.Returns(ActorKind.ExternalSystem);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers[OutgoingContextMiddleware.ActorKindHeader].ShouldBe("ExternalSystem");
    }

    [Fact]
    public void Before_WithActorKindSystem_SetsActorKindHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(false);
        userService.ActorKind.Returns(ActorKind.System);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers[OutgoingContextMiddleware.ActorKindHeader].ShouldBe("System");
    }

    // -------------------------------------------------------------------------
    // ApiKeyId header
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithApiKeyId_SetsApiKeyIdHeader()
    {
        var apiKeyId = Guid.NewGuid();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(false);
        userService.ApiKeyId.Returns(apiKeyId);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers[OutgoingContextMiddleware.ApiKeyIdHeader].ShouldBe(apiKeyId.ToString());
    }

    [Fact]
    public void Before_WithNullApiKeyId_DoesNotSetApiKeyIdHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(false);
        userService.ApiKeyId.Returns((Guid?)null);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.ApiKeyIdHeader).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Header constants
    // -------------------------------------------------------------------------

    [Fact]
    public void TenantIdHeader_HasExpectedValue() =>
        OutgoingContextMiddleware.TenantIdHeader.ShouldBe("X-Tenant-Id");

    [Fact]
    public void UserIdHeader_HasExpectedValue() =>
        OutgoingContextMiddleware.UserIdHeader.ShouldBe("X-User-Id");

    [Fact]
    public void UserFirstNameHeader_HasExpectedValue() =>
        OutgoingContextMiddleware.UserFirstNameHeader.ShouldBe("X-User-FirstName");

    [Fact]
    public void UserLastNameHeader_HasExpectedValue() =>
        OutgoingContextMiddleware.UserLastNameHeader.ShouldBe("X-User-LastName");

    [Fact]
    public void ActorKindHeader_HasExpectedValue() =>
        OutgoingContextMiddleware.ActorKindHeader.ShouldBe("X-Actor-Kind");

    [Fact]
    public void ApiKeyIdHeader_HasExpectedValue() =>
        OutgoingContextMiddleware.ApiKeyIdHeader.ShouldBe("X-Api-Key-Id");

    [Fact]
    public void TraceParentHeader_HasExpectedValue() =>
        OutgoingContextMiddleware.TraceParentHeader.ShouldBe("traceparent");
}
