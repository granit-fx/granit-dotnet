using System.Security.Claims;
using Granit.Users;
using Microsoft.AspNetCore.Http;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Test-only <see cref="ICurrentUserService"/> backed by <see cref="IHttpContextAccessor"/>.
/// Duplicates the shape of <c>Granit.Authentication.JwtBearer.Authentication.CurrentUserService</c>
/// to avoid pulling that package into the integration test project for a single interface.
/// </summary>
internal sealed class HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? User?.FindFirstValue("sub");

    public string? UserName => User?.Identity?.Name;

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? FirstName => User?.FindFirstValue(ClaimTypes.GivenName);

    public string? LastName => User?.FindFirstValue(ClaimTypes.Surname);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> GetRoles() =>
        User is { } user ? [.. user.FindAll(ClaimTypes.Role).Select(c => c.Value)] : [];

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
