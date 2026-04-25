namespace Granit.Authorization;

/// <summary>
/// Context passed to each <see cref="IPermissionGrantValidator"/> when evaluating a grant
/// before it is persisted.
/// </summary>
/// <param name="PermissionName">The permission being granted (e.g. <c>"Invoices.Delete"</c>).</param>
/// <param name="ProviderName">Provider identifier for the grantee: <c>"R"</c> (role), <c>"U"</c> (user), <c>"C"</c> (OIDC client).</param>
/// <param name="ProviderKey">Provider-specific key: role name, user id, or client id.</param>
/// <param name="TenantId">
/// Target tenant scope. <see langword="null"/> means the grant is host-level (cross-tenant
/// or infrastructure-level).
/// </param>
/// <param name="Definition">The resolved permission definition (includes <see cref="MultiTenancySides"/>).</param>
public sealed record PermissionGrantValidationContext(
    string PermissionName,
    string ProviderName,
    string ProviderKey,
    Guid? TenantId,
    PermissionDefinition Definition);
