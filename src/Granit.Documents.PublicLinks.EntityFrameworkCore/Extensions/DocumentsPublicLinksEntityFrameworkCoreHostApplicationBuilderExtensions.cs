using Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;
using Granit.Documents.PublicLinks.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Extensions;

/// <summary>Extensions for registering EF Core persistence for <c>Granit.Documents.PublicLinks</c>.</summary>
public static class DocumentsPublicLinksEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="IDocumentPublicLinkStore"/> and
    /// <see cref="IDocumentPublicLinkService"/> backed by
    /// <see cref="DocumentsPublicLinksDbContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <see cref="DocumentsPublicLinksDbContext"/> via
    /// <c>AddGranitIsolatedDbContext</c> — public links are tenant-scoped and inherit the
    /// active multi-tenancy isolation strategy.
    /// </para>
    /// <para>
    /// Should be called after <c>AddGranitDocumentsEntityFrameworkCore</c> so the
    /// parent documents <c>DbContext</c> and <see cref="IDocumentService"/>
    /// implementation are already wired.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy.</param>
    /// <param name="configureDatabasePerTenant">Optional database-per-tenant configuration.</param>
    /// <param name="configureSchemaPerTenant">Optional schema-per-tenant configuration.</param>
    /// <param name="configureTenantSchema">Optional <see cref="TenantSchemaOptions"/> tuning.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitDocumentsPublicLinksEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureShared);

        builder.Services.AddGranitIsolatedDbContext<DocumentsPublicLinksDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);
        builder.Services.TryAddScoped<IDocumentPublicLinkStore, EfDocumentPublicLinkStore>();

        return builder;
    }
}
