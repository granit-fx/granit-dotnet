using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Interceptors;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Internal.Services;
using Granit.Auditing.Options;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
    /// Registers the <see cref="AuditingDbContext"/>,
    /// <see cref="IAuditingReader"/>, <see cref="IAuditingWriter"/>,
    /// <see cref="IAuditBatchPersister"/>, <see cref="IAuditingCleaner"/>,
    /// and the <see cref="AuditingChangeTrackingInterceptor"/>.
    /// </para>
    /// <para>
    /// The interceptor must be added to the host application's DbContext separately
    /// via <see cref="DbContextOptionsBuilderAuditingExtensions.UseGranitAuditingInterceptor"/>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(conn)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAuditingEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        // Isolated DbContext (no audit interceptor — prevents recursion).
        builder.Services.AddGranitDbContext<AuditingDbContext>(configure);

        // Publisher: async (Channel) or strict (synchronous).
        builder.Services.AddScoped<StrictAuditingPublisher>();
        builder.Services.AddScoped<IAuditEntryPublisher>(sp =>
        {
            AuditingOptions options = sp.GetRequiredService<IOptions<AuditingOptions>>().Value;
            return options.PersistenceMode == AuditPersistenceMode.Strict
                ? sp.GetRequiredService<StrictAuditingPublisher>()
                : sp.GetRequiredService<ChannelAuditingPublisher>();
        });

        // EF Core implementations of persistence abstractions.
        builder.Services.AddScoped<IAuditBatchPersister, EfCoreAuditBatchPersister>();
        builder.Services.AddScoped<IAuditingCleaner, EfCoreAuditingCleaner>();

        // CQRS services.
        builder.Services.AddScoped<IAuditingReader, EfCoreAuditingReader>();
        builder.Services.AddScoped<IAuditingWriter, EfCoreAuditingWriter>();

        // HttpContextAccessor for IP/UserAgent capture in audit entries.
        builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Interceptor + capture service (scoped — host DbContext resolves from its SP).
        // Registered as both concrete type (for UseGranitAuditingInterceptor backward compat)
        // and IGranitAutoInterceptor (for automatic wiring via UseGranitInterceptors).
        builder.Services.AddScoped<AuditingChangeTrackingInterceptor>();
        builder.Services.AddScoped<IGranitAutoInterceptor>(sp =>
            sp.GetRequiredService<AuditingChangeTrackingInterceptor>());
        builder.Services.AddScoped<ChangeTrackingCaptureService>();

        return builder;
    }
}
