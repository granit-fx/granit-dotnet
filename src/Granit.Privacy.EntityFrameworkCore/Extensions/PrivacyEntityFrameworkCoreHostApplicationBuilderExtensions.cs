using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Privacy.DataExport;
using Granit.Privacy.EntityFrameworkCore.DataExport.Internal;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Privacy.EntityFrameworkCore.Extensions;

/// <summary>Extension methods for registering EF Core persistence for Granit.Privacy.</summary>
public static class PrivacyEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for Granit.Privacy legal document management,
    /// deletion requests and export requests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <see cref="PrivacyDbContext"/> via <c>AddGranitIsolatedDbContext</c>
    /// so the active <c>TenantIsolationStrategy</c> (<c>SharedDatabase</c>,
    /// <c>SchemaPerTenant</c>, <c>DatabasePerTenant</c>) is honored end-to-end. Privacy
    /// entities (<c>DeletionRequest</c>, <c>ExportRequest</c>, <c>LegalDocument</c>) are
    /// tenant-scoped — under <c>SchemaPerTenant</c> the
    /// <c>TenantSchemaConnectionInterceptor</c> is wired automatically so unqualified
    /// queries land in the tenant's schema instead of <c>public</c>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy (always required).</param>
    /// <param name="configureDatabasePerTenant">Optional database-per-tenant configuration.</param>
    /// <param name="configureSchemaPerTenant">Optional schema-per-tenant configuration.</param>
    /// <param name="configureTenantSchema">Optional <see cref="TenantSchemaOptions"/> tuning.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitPrivacyEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureShared);

        builder.Services.AddGranitIsolatedDbContext<PrivacyDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);

        builder.Services.AddHostedService<PrivacyTenantScopedProviderValidator>();

        builder.Services.AddScoped<EfLegalDocumentStore>();
        builder.Services.TryAddScoped<ILegalDocumentReader>(sp => sp.GetRequiredService<EfLegalDocumentStore>());
        builder.Services.TryAddScoped<ILegalDocumentWriter>(sp => sp.GetRequiredService<EfLegalDocumentStore>());
        builder.Services.TryAddScoped<IQueryableSource<LegalDocument>, EfLegalDocumentQueryableSource>();

        builder.Services.TryAddScoped<ILegalDocumentPublicationService, LegalDocumentPublicationService>();

        // Swap the BlobStorage in-memory default with the durable EF impl.
        // Replace (not TryAdd) so the EF impl wins regardless of registration order.
        builder.Services.Replace(ServiceDescriptor.Scoped<IExportAssemblyCheckpointStore, EfExportAssemblyCheckpointStore>());

        return builder;
    }
}
