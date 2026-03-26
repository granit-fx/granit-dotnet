using Granit.Authorization.Abstractions;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Authorization.EntityFrameworkCore.Entities;
using Granit.Authorization.Events;
using Granit.Events;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Authorization.EntityFrameworkCore.Services;

/// <summary>
/// EF Core implementation of <see cref="IPermissionManagerReader"/> and <see cref="IPermissionManagerWriter"/>.
/// Provides grant management with mandatory ISO 27001 audit logging on every mutation.
/// Cache is invalidated after each <see cref="SetAsync"/> to maintain consistency.
/// </summary>
internal sealed partial class PermissionManager<TContext>(
    TContext context,
    IPermissionDefinitionManager definitionManager,
    ILocalEventBus eventBus,
    IGuidGenerator guidGenerator,
    ILogger<PermissionManager<TContext>> logger)
    : IPermissionManagerReader, IPermissionManagerWriter
    where TContext : Microsoft.EntityFrameworkCore.DbContext, IPermissionGrantDbContext
{
    /// <inheritdoc />
    public async Task SetAsync(
        string permissionName,
        string roleName,
        Guid? tenantId,
        bool isGranted,
        CancellationToken cancellationToken = default)
    {
        if (!definitionManager.Exists(permissionName))
        {
            throw new InvalidOperationException(
                $"Permission '{permissionName}' is not defined. Register it via IPermissionDefinitionProvider.");
        }

        PermissionGrant? existing = await context.PermissionGrants
            .FirstOrDefaultAsync(
                g => g.TenantId == tenantId && g.Name == permissionName && g.RoleName == roleName,
                cancellationToken).ConfigureAwait(false);

        if (isGranted && existing is null)
        {
            context.PermissionGrants.Add(new PermissionGrant
            {
                Id = guidGenerator.Create(),
                Name = permissionName,
                RoleName = roleName,
                TenantId = tenantId
            });
        }
        else if (!isGranted && existing is not null)
        {
            context.PermissionGrants.Remove(existing);
        }
        else
        {
            return; // no-op: state already matches requested value
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (isGranted)
        {
            // VULN-205 fix: TOCTOU race — concurrent grant for the same tuple hit
            // the unique index. Treat as idempotent no-op (grant already exists).
            return;
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
        context.PermissionGrants
            .AsNoTracking()
            .AnyAsync(
                g => g.TenantId == tenantId && g.Name == permissionName && g.RoleName == roleName,
                cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PermissionGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.RoleName == roleName)
            .Select(g => g.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGrantedRolesAsync(
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PermissionGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.Name == permissionName)
            .Select(g => g.RoleName)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] Permission {Change}: permission={PermissionName} role={RoleName} tenantId={TenantId}")]
    private partial void LogPermissionChange(string change, string permissionName, string roleName, Guid? tenantId);
}
