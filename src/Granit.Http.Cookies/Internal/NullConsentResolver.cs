using Microsoft.AspNetCore.Http;

namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Default consent resolver that denies consent for all non-essential categories.
/// Applications must register a real <see cref="IConsentResolver"/> (@cookieconsent/core, Axeptio, etc.)
/// to allow non-<see cref="CookieCategory.StrictlyNecessary"/> cookies.
/// </summary>
internal sealed class NullConsentResolver : IConsentResolver
{
    /// <inheritdoc/>
    public Task<bool> HasConsentAsync(HttpContext httpContext, CookieCategory category) =>
        Task.FromResult(false);
}
