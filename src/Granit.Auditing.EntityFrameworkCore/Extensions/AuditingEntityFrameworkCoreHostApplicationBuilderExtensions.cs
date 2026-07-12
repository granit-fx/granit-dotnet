using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Interceptors;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.QueryEngine;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Auditing.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit audit logging.
/// </summary>
public static class AuditingEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for the Granit audit log module.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers the <see cref="AuditingDbContext"/>, <see cref="IAuditingReader"/>,
    /// <see cref="IAuditingWriter"/>, <see cref="IAuditingCleaner"/>, the
    /// <see cref="AuditingChangeTrackingInterceptor"/> and the persistence pipeline.
    /// </para>
    /// <para>
    /// The interceptor must be added to the host application's DbContext separately
    /// via <see cref="DbContextOptionsBuilderAuditingExtensions.UseGranitAuditingInterceptor"/>.
    /// For <b>atomic</b> auditing (audit rows committed in the same transaction as the
    /// business mutation), the host context additionally maps the audit entities via
    /// <c>modelBuilder.ConfigureAuditingModule()</c> — see
    /// <see cref="AuditingModelBuilderExtensions"/> for the single-DDL-owner rule.
    /// Without the mapping, audit rows are persisted post-commit through the isolated
    /// <see cref="AuditingDbContext"/> (standalone mode, not atomic).
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(conn)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAuditingEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        // Isolated DbContext (standalone persistence, reads, retention cleanup).
        builder.Services.AddGranitDbContext<AuditingDbContext>(configure);

        // Single persistence choke point: staging, standalone saves, metrics, Eto emission.
        builder.Services.TryAddScoped<AuditPersistencePipeline>();

        // EF Core implementations of persistence abstractions.
        builder.Services.TryAddScoped<IAuditingCleaner, EfCoreAuditingCleaner>();

        // CQRS services.
        builder.Services.TryAddScoped<IAuditingReader, EfCoreAuditingReader>();
        builder.Services.TryAddScoped<IAuditingWriter, EfCoreAuditingWriter>();

        // Queryable sources for MapGranitQuery (host bypasses tenant filter for cross-tenant audit review).
        builder.Services.TryAddScoped<IQueryableSource<AuditEntry>, EfAuditEntryQueryableSource>();
        builder.Services.TryAddScoped<IQueryableSource<AuditEntityChange>, EfAuditEntityChangeQueryableSource>();

        // HttpContextAccessor for IP/UserAgent capture in audit entries.
        builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Interceptor + capture service (scoped — host DbContext resolves from its SP).
        // Registered as both concrete type (for UseGranitAuditingInterceptor backward compat)
        // and IGranitAutoInterceptor (for automatic wiring via UseGranitInterceptors).
        builder.Services.TryAddScoped<AuditingChangeTrackingInterceptor>();
        builder.Services.AddScoped<IGranitAutoInterceptor>(sp =>
            sp.GetRequiredService<AuditingChangeTrackingInterceptor>());
        builder.Services.TryAddScoped<ChangeTrackingCaptureService>();

        return builder;
    }
}
