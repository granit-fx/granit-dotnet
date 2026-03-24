// =============================================================================
// Tests - UserContextBehavior (additional coverage)
// =============================================================================
// Covers FirstName/LastName passthrough, ActorKind parsing, ApiKeyId parsing,
// and edge cases for the user context restore behavior.
// =============================================================================

using Granit.Users;
using Granit.Wolverine.Behaviors;
using Granit.Wolverine.Internal;
using Granit.Wolverine.Middleware;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class UserContextBehaviorAdditionalTests
{
    // -------------------------------------------------------------------------
    // FirstName / LastName passthrough
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithFirstNameAndLastNameHeaders_PassesBothToSetter()
    {
        const string userId = "user-1";
        const string firstName = "Jean";
        const string lastName = "Dupont";

        IWolverineUserContextSetter setter = Substitute.For<IWolverineUserContextSetter>();
        setter.Change(
                userId,
                firstName,
                lastName,
                Arg.Any<ActorKind>(),
                Arg.Any<Guid?>())
            .Returns(Substitute.For<IDisposable>());

        UserContextBehavior behavior = new(setter);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader] = userId;
        envelope.Headers[OutgoingContextMiddleware.UserFirstNameHeader] = firstName;
        envelope.Headers[OutgoingContextMiddleware.UserLastNameHeader] = lastName;

        behavior.Before(envelope);

        setter.Received(1).Change(
            userId,
            firstName,
            lastName,
            Arg.Any<ActorKind>(),
            Arg.Any<Guid?>());
    }

    [Fact]
    public void Before_WithNoFirstNameHeader_PassesNullFirstName()
    {
        const string userId = "user-1";

        IWolverineUserContextSetter setter = Substitute.For<IWolverineUserContextSetter>();
        setter.Change(
                userId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<ActorKind>(),
                Arg.Any<Guid?>())
            .Returns(Substitute.For<IDisposable>());

        UserContextBehavior behavior = new(setter);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader] = userId;

        behavior.Before(envelope);

        setter.Received(1).Change(
            userId,
            null,
            null,
            ActorKind.User,
            null);
    }

    // -------------------------------------------------------------------------
    // ActorKind parsing
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithValidActorKindHeader_ParsesCorrectly()
    {
        const string userId = "user-1";

        IWolverineUserContextSetter setter = Substitute.For<IWolverineUserContextSetter>();
        setter.Change(
                userId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                ActorKind.ExternalSystem,
                Arg.Any<Guid?>())
            .Returns(Substitute.For<IDisposable>());

        UserContextBehavior behavior = new(setter);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader] = userId;
        envelope.Headers[OutgoingContextMiddleware.ActorKindHeader] = "ExternalSystem";

        behavior.Before(envelope);

        setter.Received(1).Change(
            userId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            ActorKind.ExternalSystem,
            Arg.Any<Guid?>());
    }

    [Fact]
    public void Before_WithInvalidActorKindHeader_DefaultsToUser()
    {
        const string userId = "user-1";

        IWolverineUserContextSetter setter = Substitute.For<IWolverineUserContextSetter>();
        setter.Change(
                userId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                ActorKind.User,
                Arg.Any<Guid?>())
            .Returns(Substitute.For<IDisposable>());

        UserContextBehavior behavior = new(setter);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader] = userId;
        envelope.Headers[OutgoingContextMiddleware.ActorKindHeader] = "InvalidKind";

        behavior.Before(envelope);

        setter.Received(1).Change(
            userId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            ActorKind.User,
            Arg.Any<Guid?>());
    }

    [Fact]
    public void Before_WithNoActorKindHeader_DefaultsToUser()
    {
        const string userId = "user-1";

        IWolverineUserContextSetter setter = Substitute.For<IWolverineUserContextSetter>();
        setter.Change(
                userId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                ActorKind.User,
                Arg.Any<Guid?>())
            .Returns(Substitute.For<IDisposable>());

        UserContextBehavior behavior = new(setter);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader] = userId;

        behavior.Before(envelope);

        setter.Received(1).Change(
            userId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            ActorKind.User,
            Arg.Any<Guid?>());
    }

    // -------------------------------------------------------------------------
    // ApiKeyId parsing
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithValidApiKeyIdHeader_ParsesCorrectly()
    {
        const string userId = "user-1";
        var apiKeyId = Guid.NewGuid();

        IWolverineUserContextSetter setter = Substitute.For<IWolverineUserContextSetter>();
        setter.Change(
                userId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<ActorKind>(),
                apiKeyId)
            .Returns(Substitute.For<IDisposable>());

        UserContextBehavior behavior = new(setter);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader] = userId;
        envelope.Headers[OutgoingContextMiddleware.ApiKeyIdHeader] = apiKeyId.ToString();

        behavior.Before(envelope);

        setter.Received(1).Change(
            userId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<ActorKind>(),
            apiKeyId);
    }

    [Fact]
    public void Before_WithInvalidApiKeyIdHeader_PassesNull()
    {
        const string userId = "user-1";

        IWolverineUserContextSetter setter = Substitute.For<IWolverineUserContextSetter>();
        setter.Change(
                userId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<ActorKind>(),
                (Guid?)null)
            .Returns(Substitute.For<IDisposable>());

        UserContextBehavior behavior = new(setter);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader] = userId;
        envelope.Headers[OutgoingContextMiddleware.ApiKeyIdHeader] = "not-a-guid";

        behavior.Before(envelope);

        setter.Received(1).Change(
            userId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<ActorKind>(),
            (Guid?)null);
    }

    [Fact]
    public void Before_WithNoApiKeyIdHeader_PassesNull()
    {
        const string userId = "user-1";

        IWolverineUserContextSetter setter = Substitute.For<IWolverineUserContextSetter>();
        setter.Change(
                userId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<ActorKind>(),
                (Guid?)null)
            .Returns(Substitute.For<IDisposable>());

        UserContextBehavior behavior = new(setter);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader] = userId;

        behavior.Before(envelope);

        setter.Received(1).Change(
            userId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<ActorKind>(),
            (Guid?)null);
    }

    // -------------------------------------------------------------------------
    // Full round-trip with all headers
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithAllHeaders_PassesAllValuesToSetter()
    {
        const string userId = "user-full";
        const string firstName = "Marie";
        const string lastName = "Curie";
        var apiKeyId = Guid.NewGuid();

        IWolverineUserContextSetter setter = Substitute.For<IWolverineUserContextSetter>();
        setter.Change(
                userId,
                firstName,
                lastName,
                ActorKind.ExternalSystem,
                apiKeyId)
            .Returns(Substitute.For<IDisposable>());

        UserContextBehavior behavior = new(setter);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader] = userId;
        envelope.Headers[OutgoingContextMiddleware.UserFirstNameHeader] = firstName;
        envelope.Headers[OutgoingContextMiddleware.UserLastNameHeader] = lastName;
        envelope.Headers[OutgoingContextMiddleware.ActorKindHeader] = "ExternalSystem";
        envelope.Headers[OutgoingContextMiddleware.ApiKeyIdHeader] = apiKeyId.ToString();

        behavior.Before(envelope);

        setter.Received(1).Change(
            userId,
            firstName,
            lastName,
            ActorKind.ExternalSystem,
            apiKeyId);
    }
}
