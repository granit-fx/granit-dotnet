using System.Security.Claims;
using Granit.Documents.Authorization;
using Microsoft.AspNetCore.Http;

namespace Granit.Documents.Endpoints.Authorization;

/// <summary>
/// Default <see cref="IDocumentPrincipalAccessor"/> implementation that resolves the User
/// id from <see cref="ClaimTypes.NameIdentifier"/> ("sub") on the current HTTP context.
/// </summary>
/// <remarks>
/// <para>
/// Role and group memberships are intentionally left empty: Granit does not impose a claim
/// convention for those, and apps that need them to flow into the share resolver replace
/// this accessor with their own implementation (e.g., reading from custom claims or a
/// per-request membership service).
/// </para>
/// </remarks>
internal sealed class HttpContextDocumentPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
    : IDocumentPrincipalAccessor
{
    public DocumentPrincipal? GetCurrent()
    {
        HttpContext? context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return null;
        }

        string? sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out Guid userId) || userId == Guid.Empty)
        {
            return null;
        }
        return DocumentPrincipal.ForUser(userId);
    }
}
