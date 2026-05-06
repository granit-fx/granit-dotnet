namespace Granit.MultiTenancy.Internal;

/// <summary>
/// Default <see cref="ITenantContextFeature"/> implementation, written to
/// <see cref="Microsoft.AspNetCore.Http.HttpContext.Features"/> by
/// <see cref="CurrentTenant"/> on the HTTP path.
/// </summary>
internal sealed record TenantContextFeature(TenantInfo? Tenant) : ITenantContextFeature;
