using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataExport;
using Granit.Privacy.EntityFrameworkCore.DataDeletion.Internal;
using Granit.Privacy.EntityFrameworkCore.DataExport.Internal;
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
    /// <see cref="IDeletionRequestTrackerReader"/>, and <see cref="IDeletionRequestTrackerWriter"/>.
    /// </summary>
    /// <remarks>
    /// Requires <c>AddGranitPrivacyEntityFrameworkCore(...)</c> to have registered
    /// <c>PrivacyDbContext</c>. The tracker tables (<c>privacy_export_requests</c>,
    /// <c>privacy_deletion_requests</c>) are added to the context via
    /// <c>ConfigurePrivacyModule</c> — run <c>dotnet ef migrations add</c> against your
    /// registered <c>PrivacyDbContext</c> to generate the schema.
    /// </remarks>
    public static GranitPrivacyBuilder UseEntityFrameworkCoreTrackers(this GranitPrivacyBuilder builder)
    {
        builder.Services.AddScoped<EfExportRequestTracker>();
        builder.Services.AddScoped<IExportRequestTrackerReader>(sp => sp.GetRequiredService<EfExportRequestTracker>());
        builder.Services.AddScoped<IExportRequestTrackerWriter>(sp => sp.GetRequiredService<EfExportRequestTracker>());

        builder.Services.AddScoped<EfDeletionRequestTracker>();
        builder.Services.AddScoped<IDeletionRequestTrackerReader>(sp => sp.GetRequiredService<EfDeletionRequestTracker>());
        builder.Services.AddScoped<IDeletionRequestTrackerWriter>(sp => sp.GetRequiredService<EfDeletionRequestTracker>());

        return builder;
    }
}
