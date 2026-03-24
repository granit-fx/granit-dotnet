using Granit.Diagnostics;
using Granit.Http.ExceptionHandling;
using Granit.Modularity;
using Granit.Observability;
using Granit.Persistence;
using Granit.Timing;
using Granit.Users;
using Granit.Validation;

namespace Granit.Bundle.Essentials;

/// <summary>
/// Extension methods on <see cref="GranitBuilder"/> for adding the Essentials bundle.
/// </summary>
public static class GranitBuilderEssentialsExtensions
{
    /// <summary>
    /// Adds the Essentials bundle: Core, Timing, Guids, Security, Validation,
    /// Persistence, Observability, ExceptionHandling, Diagnostics.
    /// </summary>
    public static GranitBuilder AddEssentials(this GranitBuilder builder)
    {
        builder.AddModule<GranitTimingModule>();
        builder.AddModule<GranitValidationModule>();
        builder.AddModule<GranitPersistenceModule>();
        builder.AddModule<GranitObservabilityModule>();
        builder.AddModule<GranitExceptionHandlingModule>();
        builder.AddModule<GranitDiagnosticsModule>();
        return builder;
    }
}
