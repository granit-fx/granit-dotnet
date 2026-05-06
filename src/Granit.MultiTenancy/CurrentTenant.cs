using Granit.MultiTenancy.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Granit.MultiTenancy;

/// <summary>
/// Implementation of <see cref="ICurrentTenant"/> with a dual-backend storage:
/// <list type="bullet">
///   <item><b>HTTP path</b> — when an <see cref="HttpContext"/> is active, the
///   tenant is stored on <see cref="HttpContext.Features"/> via
///   <see cref="ITenantContextFeature"/>. The lifetime is bound to the request
///   itself (closes by construction at the end of the pipeline).</item>
///   <item><b>Non-HTTP path</b> — when no <see cref="HttpContext"/> is active
///   (Wolverine handlers, background jobs, migrations, hosted services), the
///   tenant is stored in a process-static <see cref="AsyncLocal{T}"/>. Compatible
///   with the existing <c>using (currentTenant.Change(id))</c> pattern in those
///   call sites.</item>
/// </list>
/// </summary>
/// <remarks>
/// SECURITY: a cross-request tenant leak has been observed in production on the
/// HTTP path — symptom of thread-pool reuse re-flowing the framework's own
/// static <see cref="AsyncLocal{T}"/>. The HTTP path now stores the tenant on
/// <see cref="HttpContext.Features"/>, which is scoped to the request by
/// construction. The <see cref="AsyncLocal{T}"/> fallback remains for non-HTTP
/// code paths where no equivalent ambient scope exists; handlers and jobs
/// process one message at a time, so the documented leak class has not
/// materialized there.
/// </remarks>
public sealed class CurrentTenant : ICurrentTenant
{
    private static readonly AsyncLocal<TenantInfo?> _ambientNonHttp = new();
    private readonly IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// Constructs the tenant context with no <see cref="IHttpContextAccessor"/>.
    /// Used by tests and by AddGranit&lt;T&gt;() before <see cref="IHttpContextAccessor"/>
    /// is registered. In this mode all reads/writes use the AsyncLocal fallback.
    /// </summary>
    public CurrentTenant()
    {
        _httpContextAccessor = null;
    }

    /// <summary>
    /// Constructs the tenant context with an <see cref="IHttpContextAccessor"/>.
    /// HTTP requests use <see cref="HttpContext.Features"/>; non-HTTP code paths
    /// fall back to a process-static <see cref="AsyncLocal{T}"/>.
    /// </summary>
    public CurrentTenant(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public bool IsAvailable => Id.HasValue;

    /// <inheritdoc/>
    public Guid? Id => CurrentInfo?.Id;

    /// <inheritdoc/>
    public string? Name => CurrentInfo?.Name;

    /// <inheritdoc/>
    public IDisposable Change(Guid? id, string? name = null)
    {
        TenantInfo? newValue = id.HasValue ? new TenantInfo(id, name) : null;
        HttpContext? httpContext = _httpContextAccessor?.HttpContext;

        return httpContext is not null
            ? ChangeOnHttpContext(httpContext, newValue)
            : ChangeOnAmbientAsyncLocal(newValue);
    }

    private TenantInfo? CurrentInfo
    {
        get
        {
            // HTTP context takes precedence: if a request is active, its feature is
            // the source of truth. The AsyncLocal fallback is consulted only when
            // no HttpContext is present (Wolverine handlers, jobs, migrations).
            HttpContext? httpContext = _httpContextAccessor?.HttpContext;
            return httpContext is not null
                ? httpContext.Features.Get<ITenantContextFeature>()?.Tenant
                : _ambientNonHttp.Value;
        }
    }

    private static HttpScope ChangeOnHttpContext(HttpContext httpContext, TenantInfo? newValue)
    {
        ITenantContextFeature? previous = httpContext.Features.Get<ITenantContextFeature>();
        httpContext.Features.Set<ITenantContextFeature>(new TenantContextFeature(newValue));
        return new HttpScope(httpContext, previous);
    }

    private static AmbientScope ChangeOnAmbientAsyncLocal(TenantInfo? newValue)
    {
        TenantInfo? previous = _ambientNonHttp.Value;
        _ambientNonHttp.Value = newValue;
        return new AmbientScope(previous);
    }

    private sealed class HttpScope(HttpContext httpContext, ITenantContextFeature? previous) : IDisposable
    {
        private readonly HttpContext _httpContext = httpContext;
        private readonly ITenantContextFeature? _previous = previous;
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _httpContext.Features.Set(_previous);
            }
        }
    }

    private sealed class AmbientScope(TenantInfo? previous) : IDisposable
    {
        private readonly TenantInfo? _previous = previous;
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _ambientNonHttp.Value = _previous;
            }
        }
    }
}
