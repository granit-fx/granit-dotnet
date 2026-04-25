namespace Granit.Subscriptions.Endpoints.Internal;

/// <summary>
/// Shared constants for endpoint handlers in the Subscriptions module.
/// Centralizes literal strings reused across multiple endpoint groups
/// (e.g. ProblemDetails messages) to avoid duplication.
/// </summary>
internal static class EndpointConstants
{
    /// <summary>
    /// ProblemDetails message returned when a tenant-scoped endpoint is invoked
    /// without an active <see cref="Granit.MultiTenancy.ICurrentTenant"/> context.
    /// </summary>
    public const string TenantContextRequiredMessage = "Tenant context required.";
}
