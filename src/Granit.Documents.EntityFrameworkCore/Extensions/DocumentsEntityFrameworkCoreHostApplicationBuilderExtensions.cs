using Granit.Documents.Authorization;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Documents.Options;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Documents.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for <c>Granit.Documents</c>.
/// </summary>
public static class DocumentsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for <c>Granit.Documents</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <see cref="DocumentsDbContext"/> via <c>AddGranitIsolatedDbContext</c>, which
    /// wires the active <c>TenantIsolationStrategy</c> (<c>SharedDatabase</c>,
    /// <c>DatabasePerTenant</c>, <c>SchemaPerTenant</c>) and the
    /// <see cref="TenantSchemaConnectionInterceptor"/> for per-tenant schema routing. Audit
    /// (<c>AuditedEntityInterceptor</c>) and soft-delete (<c>SoftDeleteInterceptor</c>)
    /// interceptors are wired automatically.
    /// </para>
    /// <para>
    /// Documents is a tenant-only module: all entities are <c>IMultiTenant</c> and isolated
    /// per tenant. Provide <paramref name="configureSchemaPerTenant"/> when the host runs in
    /// <c>SchemaPerTenant</c> mode.
    /// </para>
    /// <para>
    /// Must be called after <see cref="DocumentsServiceCollectionExtensions.AddGranitDocuments"/>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy (provider + connection string).</param>
    /// <param name="configureDatabasePerTenant">Optional EF Core options for the database-per-tenant strategy.</param>
    /// <param name="configureSchemaPerTenant">Optional EF Core options for the schema-per-tenant strategy.</param>
    /// <param name="configureTenantSchema">Optional <see cref="TenantSchemaOptions"/> tuning (prefix, naming).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitDocumentsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureShared);

        builder.Services.AddGranitIsolatedDbContext<DocumentsDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);

        // Tenant-root bootstrap (F2.2): scoped so the per-instance memoisation cache lives
        // for the duration of one request only. Concurrency-safe via the partial unique
        // index ux_documents_folders_one_root_per_tenant.
        builder.Services.AddScoped<IDocumentBootstrapService, DocumentBootstrapService>();

        // Folder service (F2.3): EF Core-backed CRUD orchestration consumed by
        // Granit.Documents.Endpoints.
        builder.Services.AddScoped<IFolderService, FolderService>();

        // Document service (F3.2): upload-ticket request + finalize flow that wraps
        // BlobStorage's presigned upload pipeline and creates Document + initial
        // DocumentVersion atomically.
        builder.Services.AddScoped<IDocumentService, DocumentService>();

        // Share service (F6.1): grant / revoke / list ACL grants on folders and documents.
        // Effective-permission resolution (F6.2) and FusionCache invalidation (F6.3) plug
        // in on top in subsequent stories.
        builder.Services.AddScoped<IDocumentShareService, DocumentShareService>();

        // Tenant storage quota service (F7.1): lazy creation + atomic increment / decrement.
        // Enforcement on upload (F7.2) + admin read endpoint (F7.3) layer on top.
        builder.Services.AddScoped<ITenantQuotaService, TenantQuotaService>();

        // Maintenance service (F9): cross-tenant orphan cleanup, empty-trash, quota
        // recompute. Consumed by the Granit.Documents.BackgroundJobs handlers.
        builder.Services.AddScoped<IDocumentMaintenanceService, DocumentMaintenanceService>();

        // Effective-permission resolver (F6.2 + F6.3). The concrete EffectivePermissionResolver
        // is registered first so the cache decorator can delegate to it; the public
        // IEffectivePermissionResolver registration depends on whether the FusionCache layer
        // is enabled (DocumentsOptions.AclCacheEnabled — default true). The decorator type
        // lives in the base module (Granit.Documents.Authorization) — layer-purity rule keeps
        // EF Core out of base, but base can host pure orchestration like a FusionCache decorator.
        builder.Services.AddScoped<EffectivePermissionResolver>();
        builder.Services.AddScoped<IEffectivePermissionResolver>(sp =>
        {
            GranitDocumentsOptions opts = sp
                .GetRequiredService<IOptions<GranitDocumentsOptions>>().Value;
            EffectivePermissionResolver inner = sp.GetRequiredService<EffectivePermissionResolver>();
            return opts.AclCacheEnabled
                ? ActivatorUtilities.CreateInstance<CachedEffectivePermissionResolver>(sp, inner)
                : inner;
        });

        // Query engine source — powers the generic /documents/query endpoint.
        builder.Services.AddScoped<IQueryableSource<Document>, EfDocumentQueryableSource>();

        return builder;
    }
}
