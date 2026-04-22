using System.Security.Claims;
using Granit.Users;
using Microsoft.AspNetCore.Http;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Minimal HttpContext-backed <see cref="ICurrentUserService"/> for the permission
/// enforcement tests — reads <c>sub</c> / <c>NameIdentifier</c> and role claims from
/// the authenticated principal attached by <see cref="TestAuthHandler"/>.
/// </summary>
internal sealed class HttpContextCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");

    public string? UserName => User?.Identity?.Name;

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? FirstName => User?.FindFirstValue(ClaimTypes.GivenName);

    public string? LastName => User?.FindFirstValue(ClaimTypes.Surname);

    public string? ClientId => User?.FindFirstValue("client_id");

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> GetRoles() =>
        User is null ? [] : [.. User.FindAll(ClaimTypes.Role).Select(c => c.Value)];

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
