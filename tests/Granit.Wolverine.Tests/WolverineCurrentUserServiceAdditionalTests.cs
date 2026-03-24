// =============================================================================
// Tests - WolverineCurrentUserService (additional coverage)
// =============================================================================
// Covers FirstName/LastName override and HTTP fallback, ActorKind override and
// HTTP fallback, IsMachine property, ApiKeyId override and HTTP fallback,
// and edge cases not covered by WolverineCurrentUserServiceTests.
// =============================================================================

using System.Security.Claims;
using Granit.Users;
using Granit.Wolverine.Internal;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class WolverineCurrentUserServiceAdditionalTests
{
    private static WolverineCurrentUserService CreateWithoutHttpContext()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        return new WolverineCurrentUserService(accessor);
    }

    private static WolverineCurrentUserService CreateWithHttpContext(
        bool isAuthenticated, params Claim[] claims)
    {
        ClaimsIdentity identity = new(claims, isAuthenticated ? "test" : null);
        ClaimsPrincipal principal = new(identity);
        DefaultHttpContext httpContext = new() { User = principal };
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new WolverineCurrentUserService(accessor);
    }

    // -------------------------------------------------------------------------
    // FirstName — AsyncLocal override
    // -------------------------------------------------------------------------

    [Fact]
    public void FirstName_AfterChangeWithFirstName_ReturnsOverrideValue()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        using IDisposable scope = sut.Change("user", firstName: "Jean");

        sut.FirstName.ShouldBe("Jean");
    }

    [Fact]
    public void FirstName_AfterChangeWithoutFirstName_ReturnsNull()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        using IDisposable scope = sut.Change("user");

        sut.FirstName.ShouldBeNull();
    }

    [Fact]
    public void FirstName_WithOverrideActive_IgnoresHttpContext()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.GivenName, "HttpJean"));

        using IDisposable scope = sut.Change("user", firstName: "OverrideJean");

        sut.FirstName.ShouldBe("OverrideJean");
    }

    // -------------------------------------------------------------------------
    // FirstName — HTTP context fallback
    // -------------------------------------------------------------------------

    [Fact]
    public void FirstName_WithGivenNameClaim_ReturnsGivenName()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.GivenName, "Pierre"));

        sut.FirstName.ShouldBe("Pierre");
    }

    [Fact]
    public void FirstName_WithGivenNameShorthandClaim_ReturnsGivenName()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim("given_name", "Marie"));

        sut.FirstName.ShouldBe("Marie");
    }

    [Fact]
    public void FirstName_WithNoRelevantClaim_ReturnsNull()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.Email, "user@example.com"));

        sut.FirstName.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // LastName — AsyncLocal override
    // -------------------------------------------------------------------------

    [Fact]
    public void LastName_AfterChangeWithLastName_ReturnsOverrideValue()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        using IDisposable scope = sut.Change("user", lastName: "Dupont");

        sut.LastName.ShouldBe("Dupont");
    }

    [Fact]
    public void LastName_AfterChangeWithoutLastName_ReturnsNull()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        using IDisposable scope = sut.Change("user");

        sut.LastName.ShouldBeNull();
    }

    [Fact]
    public void LastName_WithOverrideActive_IgnoresHttpContext()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.Surname, "HttpDupont"));

        using IDisposable scope = sut.Change("user", lastName: "OverrideDupont");

        sut.LastName.ShouldBe("OverrideDupont");
    }

    // -------------------------------------------------------------------------
    // LastName — HTTP context fallback
    // -------------------------------------------------------------------------

    [Fact]
    public void LastName_WithSurnameClaim_ReturnsSurname()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.Surname, "Martin"));

        sut.LastName.ShouldBe("Martin");
    }

    [Fact]
    public void LastName_WithFamilyNameShorthandClaim_ReturnsFamilyName()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim("family_name", "Durand"));

        sut.LastName.ShouldBe("Durand");
    }

    [Fact]
    public void LastName_WithNoRelevantClaim_ReturnsNull()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim(ClaimTypes.Email, "user@example.com"));

        sut.LastName.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // ActorKind — AsyncLocal override
    // -------------------------------------------------------------------------

    [Fact]
    public void ActorKind_DefaultWithNoOverride_ReturnsUser()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        sut.ActorKind.ShouldBe(ActorKind.User);
    }

    [Fact]
    public void ActorKind_AfterChangeWithExternalSystem_ReturnsExternalSystem()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        using IDisposable scope = sut.Change("user", actorKind: ActorKind.ExternalSystem);

        sut.ActorKind.ShouldBe(ActorKind.ExternalSystem);
    }

    [Fact]
    public void ActorKind_AfterChangeWithSystem_ReturnsSystem()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        using IDisposable scope = sut.Change("user", actorKind: ActorKind.System);

        sut.ActorKind.ShouldBe(ActorKind.System);
    }

    // -------------------------------------------------------------------------
    // ActorKind — HTTP context fallback
    // -------------------------------------------------------------------------

    [Fact]
    public void ActorKind_WithActorKindClaim_ReturnsParsedValue()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim("actor_kind", "ExternalSystem"));

        sut.ActorKind.ShouldBe(ActorKind.ExternalSystem);
    }

    [Fact]
    public void ActorKind_WithInvalidActorKindClaim_ReturnsUser()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim("actor_kind", "InvalidValue"));

        sut.ActorKind.ShouldBe(ActorKind.User);
    }

    [Fact]
    public void ActorKind_WithNoActorKindClaim_ReturnsUser()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim("sub", "user-123"));

        sut.ActorKind.ShouldBe(ActorKind.User);
    }

    // -------------------------------------------------------------------------
    // IsMachine
    // -------------------------------------------------------------------------

    [Fact]
    public void IsMachine_WhenActorKindIsUser_ReturnsFalse()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        sut.IsMachine.ShouldBeFalse();
    }

    [Fact]
    public void IsMachine_WhenActorKindIsExternalSystem_ReturnsTrue()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        using IDisposable scope = sut.Change("user", actorKind: ActorKind.ExternalSystem);

        sut.IsMachine.ShouldBeTrue();
    }

    [Fact]
    public void IsMachine_WhenActorKindIsSystem_ReturnsTrue()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        using IDisposable scope = sut.Change("user", actorKind: ActorKind.System);

        sut.IsMachine.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // ApiKeyId — AsyncLocal override
    // -------------------------------------------------------------------------

    [Fact]
    public void ApiKeyId_DefaultWithNoOverride_ReturnsNull()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        sut.ApiKeyId.ShouldBeNull();
    }

    [Fact]
    public void ApiKeyId_AfterChangeWithApiKeyId_ReturnsOverrideValue()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();
        var expectedId = Guid.NewGuid();

        using IDisposable scope = sut.Change("user", apiKeyId: expectedId);

        sut.ApiKeyId.ShouldBe(expectedId);
    }

    [Fact]
    public void ApiKeyId_AfterScopeDisposed_RestoresNull()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        IDisposable scope = sut.Change("user", apiKeyId: Guid.NewGuid());
        scope.Dispose();

        sut.ApiKeyId.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // ApiKeyId — HTTP context fallback
    // -------------------------------------------------------------------------

    [Fact]
    public void ApiKeyId_WithValidApiKeyIdClaim_ReturnsParsedGuid()
    {
        var expectedId = Guid.NewGuid();
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim("api_key_id", expectedId.ToString()));

        sut.ApiKeyId.ShouldBe(expectedId);
    }

    [Fact]
    public void ApiKeyId_WithInvalidApiKeyIdClaim_ReturnsNull()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim("api_key_id", "not-a-guid"));

        sut.ApiKeyId.ShouldBeNull();
    }

    [Fact]
    public void ApiKeyId_WithNoApiKeyIdClaim_ReturnsNull()
    {
        WolverineCurrentUserService sut = CreateWithHttpContext(
            isAuthenticated: true,
            new Claim("sub", "user-123"));

        sut.ApiKeyId.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Nested scopes — FirstName/LastName/ActorKind/ApiKeyId restore correctly
    // -------------------------------------------------------------------------

    [Fact]
    public void NestedScopes_RestoreAllFieldsCorrectly()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();
        var outerApiKeyId = Guid.NewGuid();
        var innerApiKeyId = Guid.NewGuid();

        IDisposable outerScope = sut.Change(
            "outer", "OuterFirst", "OuterLast", ActorKind.ExternalSystem, outerApiKeyId);

        sut.FirstName.ShouldBe("OuterFirst");
        sut.LastName.ShouldBe("OuterLast");
        sut.ActorKind.ShouldBe(ActorKind.ExternalSystem);
        sut.ApiKeyId.ShouldBe(outerApiKeyId);

        IDisposable innerScope = sut.Change(
            "inner", "InnerFirst", "InnerLast", ActorKind.System, innerApiKeyId);

        sut.FirstName.ShouldBe("InnerFirst");
        sut.LastName.ShouldBe("InnerLast");
        sut.ActorKind.ShouldBe(ActorKind.System);
        sut.ApiKeyId.ShouldBe(innerApiKeyId);

        innerScope.Dispose();

        sut.FirstName.ShouldBe("OuterFirst");
        sut.LastName.ShouldBe("OuterLast");
        sut.ActorKind.ShouldBe(ActorKind.ExternalSystem);
        sut.ApiKeyId.ShouldBe(outerApiKeyId);

        outerScope.Dispose();

        sut.FirstName.ShouldBeNull();
        sut.LastName.ShouldBeNull();
        sut.ActorKind.ShouldBe(ActorKind.User);
        sut.ApiKeyId.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Roles — HTTP context (no override)
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRoles_WithNoHttpContextAndNoOverride_ReturnsEmpty()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        sut.GetRoles().ShouldBeEmpty();
    }

    [Fact]
    public void IsInRole_WithNoHttpContextAndNoOverride_ReturnsFalse()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        sut.IsInRole("admin").ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // UserName and Email — no HTTP context, no override
    // -------------------------------------------------------------------------

    [Fact]
    public void UserName_WithNoHttpContextAndNoOverride_ReturnsNull()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        sut.UserName.ShouldBeNull();
    }

    [Fact]
    public void Email_WithNoHttpContextAndNoOverride_ReturnsNull()
    {
        WolverineCurrentUserService sut = CreateWithoutHttpContext();

        sut.Email.ShouldBeNull();
    }
}
