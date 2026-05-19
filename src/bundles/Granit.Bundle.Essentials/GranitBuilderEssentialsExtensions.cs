using Granit.Diagnostics;
using Granit.Http.ExceptionHandling;
using Granit.Http.SecurityHeaders;
using Granit.Modularity;
using Granit.Observability;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Granit.Validation;

namespace Granit.Bundle.Essentials;

/// <summary>
/// Extension methods on <see cref="GranitBuilder"/> for adding the Essentials bundle.
/// </summary>
public static class GranitBuilderEssentialsExtensions
{
    /// <summary>
    /// Adds the Essentials bundle: Core, Timing, Guids, Security, Validation,
    /// Persistence, Observability, ExceptionHandling, Http.Security, Diagnostics.
    /// </summary>
    public static GranitBuilder AddEssentials(this GranitBuilder builder)
    {
        builder.AddModule<GranitTimingModule>();
        builder.AddModule<GranitValidationModule>();
        builder.AddModule<GranitPersistenceEntityFrameworkCoreModule>();
        builder.AddModule<GranitObservabilityModule>();
        builder.AddModule<GranitHttpExceptionHandlingModule>();
        builder.AddModule<GranitHttpSecurityHeadersModule>();
        builder.AddModule<GranitDiagnosticsModule>();
        return builder;
    }
}
