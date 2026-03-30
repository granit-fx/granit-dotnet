using Microsoft.AspNetCore.Http;

namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Default <see cref="ICookieConsentModelProvider"/> that returns <c>null</c>,
/// indicating no regulation context is available. Replaced by the bridge package
/// <c>Granit.Privacy.Regulations.Cookies</c> when loaded.
/// </summary>
internal sealed class NullCookieConsentModelProvider : ICookieConsentModelProvider
{
    /// <inheritdoc/>
    public Task<ConsentModelInfo?> GetConsentModelAsync(HttpContext httpContext) =>
        Task.FromResult<ConsentModelInfo?>(null);
}
