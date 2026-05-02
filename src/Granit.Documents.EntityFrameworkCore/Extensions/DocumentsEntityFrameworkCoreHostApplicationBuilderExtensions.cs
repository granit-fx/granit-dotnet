using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    /// Registers the isolated <c>DocumentsDbContext</c> via <c>AddGranitDbContext</c>
    /// (<see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/> with interceptor
    /// DI), which automatically wires <c>AuditedEntityInterceptor</c> and
    /// <c>SoftDeleteInterceptor</c> when <c>Granit.Persistence</c> is configured, enabling the
    /// ISO 27001 3-year audit trail and GDPR-compatible soft-delete out of the box.
    /// </para>
    /// <para>
    /// Must be called after <see cref="DocumentsServiceCollectionExtensions.AddGranitDocuments"/>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitDocumentsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<DocumentsDbContext>(configure);

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

        return builder;
    }
}
