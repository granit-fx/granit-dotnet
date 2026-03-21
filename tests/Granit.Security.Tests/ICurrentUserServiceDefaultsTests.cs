using Shouldly;
using Xunit;

namespace Granit.Security.Tests;

/// <summary>
/// Verifies that default interface methods on <see cref="ICurrentUserService"/>
/// return the expected values for implementations that do not override them
/// (backward compatibility with <c>CurrentUserService</c> from JwtBearer).
/// </summary>
public sealed class ICurrentUserServiceDefaultsTests
{
    [Fact]
    public void ActorKind_DefaultsToUser()
    {
        ICurrentUserService sut = CreateMinimalImplementation();
        sut.ActorKind.ShouldBe(ActorKind.User);
    }

    [Fact]
    public void IsMachine_DefaultsToFalse_WhenActorKindIsUser()
    {
        ICurrentUserService sut = CreateMinimalImplementation();
        sut.IsMachine.ShouldBeFalse();
    }

    [Fact]
    public void IsMachine_ReturnsTrue_WhenActorKindIsExternalSystem()
    {
        ICurrentUserService sut = CreateImplementationWithActorKind(ActorKind.ExternalSystem);
        sut.IsMachine.ShouldBeTrue();
    }

    [Fact]
    public void IsMachine_ReturnsTrue_WhenActorKindIsSystem()
    {
        ICurrentUserService sut = CreateImplementationWithActorKind(ActorKind.System);
        sut.IsMachine.ShouldBeTrue();
    }

    [Fact]
    public void ApiKeyId_DefaultsToNull()
    {
        ICurrentUserService sut = CreateMinimalImplementation();
        sut.ApiKeyId.ShouldBeNull();
    }

    // CA1859: intentionally typed as interface to test default interface method dispatch.
#pragma warning disable CA1859
    private static ICurrentUserService CreateMinimalImplementation() => new MinimalCurrentUserService();

    private static ICurrentUserService CreateImplementationWithActorKind(ActorKind actorKind) =>
        new ActorKindOverrideCurrentUserService(actorKind);
#pragma warning restore CA1859

    /// <summary>
    /// Minimal implementation that does NOT override the new default interface members.
    /// Simulates an existing implementation like <c>CurrentUserService</c> from JwtBearer.
    /// </summary>
    private sealed class MinimalCurrentUserService : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => null;
        public string? Email => null;
        public string? FirstName => null;
        public string? LastName => null;
        public bool IsAuthenticated => false;
        public IReadOnlyList<string> GetRoles() => [];
        public bool IsInRole(string role) => false;
        // ActorKind, IsMachine, ApiKeyId NOT overridden — defaults apply.
    }

    /// <summary>
    /// Implementation that overrides <see cref="ICurrentUserService.ActorKind"/>
    /// but relies on the default <see cref="ICurrentUserService.IsMachine"/> logic.
    /// Tests the <c>ActorKind is not ActorKind.User</c> default expression.
    /// </summary>
    private sealed class ActorKindOverrideCurrentUserService(ActorKind actorKind) : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => null;
        public string? Email => null;
        public string? FirstName => null;
        public string? LastName => null;
        public bool IsAuthenticated => false;
        public IReadOnlyList<string> GetRoles() => [];
        public bool IsInRole(string role) => false;
        public ActorKind ActorKind => actorKind;
        // IsMachine NOT overridden — default logic applies based on ActorKind.
    }
}
