using Granit.MultiTenancy.Domain;
using Granit.MultiTenancy.Stores;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.MultiTenancy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ITenantReader"/> and <see cref="ITenantWriter"/>.
/// Persists tenants in the <c>tenants_tenants</c> table with full ISO 27001 audit trail.
/// </summary>
/// <remarks>
/// Domain events are raised on the <see cref="Tenant"/> aggregate root and dispatched
/// automatically by <c>DomainEventDispatcherInterceptor</c> during <c>SaveChangesAsync</c>.
/// </remarks>
internal sealed partial class EfCoreTenantStore(
    IDbContextFactory<MultiTenancyDbContext> contextFactory,
    ILogger<EfCoreTenantStore> logger)
    : EfStoreBase<Tenant, MultiTenancyDbContext>(contextFactory), ITenantReader, ITenantWriter
{
    // -------------------------------------------------------------------------
    // ITenantReader
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public new async Task<TenantData?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Tenant? tenant = await ReadAsync(
            async db => await db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return tenant is null ? null : ToData(tenant);
    }

    /// <inheritdoc/>
    public async Task<TenantData?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        Tenant? tenant = await ReadAsync(
            async db => await db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Identifier == identifier, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return tenant is null ? null : ToData(tenant);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantData>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Tenant> tenants = await ReadAsync(
            async db => await db.Tenants
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return tenants.Select(ToData).ToList();
    }

    /// <inheritdoc/>
    public async Task<TenantData?> FindByCustomDomainAsync(string customDomain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customDomain);

        Tenant? tenant = await ReadAsync(
            async db => await db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.CustomDomain == customDomain, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return tenant is null ? null : ToData(tenant);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ReadAsync(
            async db => await db.Tenants
                .AnyAsync(t => t.Id == id, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    // -------------------------------------------------------------------------
    // ITenantWriter
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task CreateAsync(
        Guid id,
        string name,
        string identifier,
        string? contactEmail,
        string? jurisdiction,
        CancellationToken cancellationToken = default)
    {
        var tenant = Tenant.Create(id, name, identifier, contactEmail, jurisdiction);

        await WriteAsync(async db =>
        {
            db.Tenants.Add(tenant);
        }, cancellationToken).ConfigureAwait(false);

        LogTenantCreated(id, identifier);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        Guid id,
        string name,
        string? contactEmail,
        string? jurisdiction,
        CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
        {
            Tenant tenant = await db.Tenants
                .FirstAsync(t => t.Id == id, cancellationToken)
                .ConfigureAwait(false);

            tenant.UpdateDetails(name, contactEmail, jurisdiction);
        }, cancellationToken).ConfigureAwait(false);

        LogTenantUpdated(id);
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
        {
            Tenant tenant = await db.Tenants
                .FirstAsync(t => t.Id == id, cancellationToken)
                .ConfigureAwait(false);

            tenant.Activate();
        }, cancellationToken).ConfigureAwait(false);

        LogTenantActivated(id);
    }

    /// <inheritdoc/>
    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
        {
            Tenant tenant = await db.Tenants
                .FirstAsync(t => t.Id == id, cancellationToken)
                .ConfigureAwait(false);

            tenant.Deactivate();
        }, cancellationToken).ConfigureAwait(false);

        LogTenantDeactivated(id);
    }

    // -------------------------------------------------------------------------
    // Mapping
    // -------------------------------------------------------------------------

    private static TenantData ToData(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.Identifier, tenant.PartyEmail, tenant.Activated, tenant.Jurisdiction, tenant.CreatedAt, tenant.CustomDomain);

    // -------------------------------------------------------------------------
    // Logging
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {TenantId} created with identifier '{Identifier}'.")]
    private partial void LogTenantCreated(Guid tenantId, string identifier);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {TenantId} details updated.")]
    private partial void LogTenantUpdated(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {TenantId} activated.")]
    private partial void LogTenantActivated(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {TenantId} deactivated.")]
    private partial void LogTenantDeactivated(Guid tenantId);
}
