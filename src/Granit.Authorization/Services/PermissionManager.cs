using Granit.Authorization.Events;
using Granit.Events;
using Microsoft.Extensions.Logging;

namespace Granit.Authorization.Services;

/// <summary>
/// Domain service implementing <see cref="IPermissionManagerReader"/> and <see cref="IPermissionManagerWriter"/>.
/// Delegates data access to <see cref="IPermissionGrantStore"/> and provides business logic:
/// permission definition validation, grant validation chain, event-driven cache invalidation,
/// and ISO 27001 audit logging.
/// </summary>
internal sealed partial class PermissionManager(
    IPermissionGrantStore grantStore,
    IPermissionDefinitionManager definitionManager,
    IEnumerable<IPermissionGrantValidator> grantValidators,
    ILocalEventBus eventBus,
    ILogger<PermissionManager> logger)
    : IPermissionManagerReader, IPermissionManagerWriter
{
    /// <inheritdoc />
    public async Task SetAsync(
        string permissionName,
        string roleName,
        Guid? tenantId,
        bool isGranted,
        CancellationToken cancellationToken = default)
    {
        PermissionDefinition? definition = definitionManager.Find(permissionName);
        if (definition is null)
        {
            throw new InvalidOperationException(
                $"Permission '{permissionName}' is not defined. Register it via IPermissionDefinitionProvider.");
        }

        // Validators only gate additions. Revocations must always be possible so that a
        // grant previously made under relaxed rules can still be removed after a policy change.
        if (isGranted)
        {
            PermissionGrantValidationContext validationContext = new(
                permissionName,
                PermissionGrantProviderNames.Role,
                roleName,
                tenantId,
                definition);

            foreach (IPermissionGrantValidator validator in grantValidators)
            {
                PermissionGrantValidationResult result = await validator
                    .ValidateAsync(validationContext, cancellationToken).ConfigureAwait(false);

                if (!result.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Grant rejected ({result.ReasonCode}): {result.ReasonMessage}");
                }
            }
        }

        bool changed = isGranted
            ? await grantStore.GrantAsync(permissionName, roleName, tenantId, cancellationToken)
                .ConfigureAwait(false)
            : await grantStore.RevokeAsync(permissionName, roleName, tenantId, cancellationToken)
                .ConfigureAwait(false);

        if (!changed)
        {
            return; // no-op: state already matches requested value (or idempotent race)
        }

        // Event-driven cache invalidation — consumed by PermissionCacheInvalidationHandler.
        // Decoupled from the store so other modules can react to permission changes.
        await eventBus.PublishAsync(
            new PermissionGrantChangedEvent(permissionName, roleName, tenantId, isGranted),
            cancellationToken).ConfigureAwait(false);

        // ISO 27001 audit trail: emitted as structured log → Serilog → OTLP → Loki (3-year retention)
        // RGPD: no personal data — only role name, permission name, tenant scope
        LogPermissionChange(isGranted ? "Granted" : "Revoked", permissionName, roleName, tenantId);
    }

    /// <inheritdoc />
    public Task<bool> IsGrantedAsync(
        string permissionName,
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        grantStore.IsGrantedAsync(roleName, permissionName, tenantId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        grantStore.GetGrantedPermissionsAsync(roleName, tenantId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetGrantedRolesAsync(
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        grantStore.GetGrantedRolesAsync(permissionName, tenantId, cancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] Permission {Change}: permission={PermissionName} role={RoleName} tenantId={TenantId}")]
    private partial void LogPermissionChange(string change, string permissionName, string roleName, Guid? tenantId);
}
