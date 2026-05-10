namespace Granit.Documents.Endpoints.Quotas.Dtos;

/// <summary>
/// Wire-shape representation of a tenant's storage quota usage (F7.3).
/// </summary>
/// <param name="LimitBytes">Soft cap on the tenant's stored bytes.</param>
/// <param name="UsageBytes">Sum of active version sizes for the tenant.</param>
/// <param name="PercentUsed">
/// Convenience field equal to <c>UsageBytes / LimitBytes * 100</c> rounded to two decimals.
/// Frontends use it to render a usage bar without computing the ratio themselves.
/// </param>
/// <param name="UpdatedAt">UTC instant of the last increment, decrement, or limit change.</param>
public sealed record TenantStorageQuotaResponse(
    long LimitBytes,
    long UsageBytes,
    double PercentUsed,
    DateTimeOffset UpdatedAt);
