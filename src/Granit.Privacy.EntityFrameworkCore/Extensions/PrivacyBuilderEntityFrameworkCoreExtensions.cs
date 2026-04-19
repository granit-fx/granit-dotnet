using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataExport;
using Granit.Privacy.EntityFrameworkCore.DataDeletion.Internal;
using Granit.Privacy.EntityFrameworkCore.DataExport.Internal;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods on <see cref="GranitPrivacyBuilder"/> for wiring the EF Core tracker
/// defaults. Apps that already persist export / deletion state elsewhere may skip these
/// and provide their own <c>Use*Tracker&lt;TStore&gt;()</c> implementations instead.
/// </summary>
public static class PrivacyBuilderEntityFrameworkCoreExtensions
{
    /// <summary>
    /// Registers EF Core default implementations for
    /// <see cref="IExportRequestTrackerReader"/>, <see cref="IExportRequestTrackerWriter"/>,
    /// <see cref="IDeletionRequestTrackerReader"/>, and <see cref="IDeletionRequestTrackerWriter"/>
    /// targeting the built-in <see cref="PrivacyDbContext"/>.
    /// </summary>
    /// <remarks>
    /// Requires <c>AddGranitPrivacyEntityFrameworkCore(...)</c> to have registered
    /// <c>PrivacyDbContext</c>. For apps that apply <c>ConfigurePrivacyModule()</c> to their
    /// own host / tenant DbContext, use the generic overload
    /// <see cref="UseEntityFrameworkCoreTrackers{TContext}"/> instead.
    /// </remarks>
    public static GranitPrivacyBuilder UseEntityFrameworkCoreTrackers(this GranitPrivacyBuilder builder) =>
        builder.UseEntityFrameworkCoreTrackers<PrivacyDbContext>();

    /// <summary>
    /// Registers EF Core tracker implementations against the host <typeparamref name="TContext"/>.
    /// Use this overload when the application keeps privacy entities on its own shared or
    /// tenant-scoped DbContext (via <c>modelBuilder.ConfigurePrivacyModule()</c>) instead of
    /// the default <see cref="PrivacyDbContext"/>.
    /// </summary>
    /// <typeparam name="TContext">The DbContext that hosts <c>ExportRequestEntity</c> and <c>DeletionRequestEntity</c>.</typeparam>
    public static GranitPrivacyBuilder UseEntityFrameworkCoreTrackers<TContext>(this GranitPrivacyBuilder builder)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<EfExportRequestTracker<TContext>>();
        builder.Services.AddScoped<IExportRequestTrackerReader>(
            sp => sp.GetRequiredService<EfExportRequestTracker<TContext>>());
        builder.Services.AddScoped<IExportRequestTrackerWriter>(
            sp => sp.GetRequiredService<EfExportRequestTracker<TContext>>());

        builder.Services.AddScoped<EfDeletionRequestTracker<TContext>>();
        builder.Services.AddScoped<IDeletionRequestTrackerReader>(
            sp => sp.GetRequiredService<EfDeletionRequestTracker<TContext>>());
        builder.Services.AddScoped<IDeletionRequestTrackerWriter>(
            sp => sp.GetRequiredService<EfDeletionRequestTracker<TContext>>());

        return builder;
    }
}
