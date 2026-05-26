// =============================================================================
// Tests - OwnedByCurrentUserHandler
// =============================================================================
// Resource-based handler: succeeds when the current user's UserGuid matches the
// resource's IOwnable.OwnerId. Fail-silent on non-Guid sub (machine actors,
// federated identities), never calls context.Fail().
// =============================================================================

using System.Security.Claims;
using Granit.Authorization.Authorization;
using Granit.Domain;
using Granit.Users;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class OwnedByCurrentUserHandlerTests
{
    private static readonly Guid AliceId = Guid.Parse("c2c61eaf-1a3a-4bbf-90d7-9c8a5e2d6f12");
    private static readonly Guid BobId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task HandleAsync_UserGuidMatchesOwnerId_ContextSucceeds()
    {
        // NSubstitute proxies every interface member including default-impls — UserGuid
        // must be stubbed explicitly. Tests for the default-impl itself live in
        // Granit.Users.Tests.ICurrentUserServiceDefaultsTests.
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserGuid.Returns(AliceId);

        OwnedByCurrentUserHandler handler = new(currentUser);
        OwnedRequirement requirement = new();
        AuthorizationHandlerContext context = new(
            [requirement],
            new ClaimsPrincipal(),
            resource: new OwnedResource(AliceId));

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_UserGuidDoesNotMatchOwnerId_ContextDoesNotSucceed()
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserGuid.Returns(AliceId);

        OwnedByCurrentUserHandler handler = new(currentUser);
        OwnedRequirement requirement = new();
        AuthorizationHandlerContext context = new(
            [requirement],
            new ClaimsPrincipal(),
            resource: new OwnedResource(BobId));

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
        context.HasFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_UserGuidIsNull_ContextDoesNotSucceed_AndDoesNotFail()
    {
        // Non-Guid sub (Google numeric, Cognito federated, anonymous) — UserGuid is null.
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserGuid.Returns((Guid?)null);

        OwnedByCurrentUserHandler handler = new(currentUser);
        OwnedRequirement requirement = new();
        AuthorizationHandlerContext context = new(
            [requirement],
            new ClaimsPrincipal(),
            resource: new OwnedResource(AliceId));

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
        // Fail-silent: other handlers (permission-based, role-based) can still grant access.
        context.HasFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_ResourceIsNotIOwnable_HandlerIsNotInvoked()
    {
        // ASP.NET only dispatches AuthorizationHandler<TReq, TRes> when the resource
        // matches TRes. With a non-IOwnable resource, the handler is bypassed entirely
        // and the requirement remains unsatisfied.
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserGuid.Returns(AliceId);

        OwnedByCurrentUserHandler handler = new(currentUser);
        OwnedRequirement requirement = new();
        AuthorizationHandlerContext context = new(
            [requirement],
            new ClaimsPrincipal(),
            resource: "not-an-iownable");

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
        context.HasFailed.ShouldBeFalse();
    }

    private sealed record OwnedResource(Guid OwnerId) : IOwnable;
}
