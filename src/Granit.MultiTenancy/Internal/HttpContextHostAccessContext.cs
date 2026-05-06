using Microsoft.AspNetCore.Http;

namespace Granit.MultiTenancy.Internal;

/// <summary>
/// HTTP-backed <see cref="IHostAccessContext"/> reading the
/// <see cref="IHostAccessFeature"/> placed on the request feature collection
/// by <c>RequireHostContextEndpointFilter</c>.
/// </summary>
internal sealed class HttpContextHostAccessContext(IHttpContextAccessor httpContextAccessor)
    : IHostAccessContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public bool IsHostAccess =>
        _httpContextAccessor.HttpContext?.Features.Get<IHostAccessFeature>()?.IsHostAccess
        ?? false;
}
