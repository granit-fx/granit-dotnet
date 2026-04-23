using System.Diagnostics;

namespace Granit.Identity.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for the Granit.Identity abstractions.
/// </summary>
/// <remarks>
/// Registered with <c>GranitActivitySourceRegistry</c> by <c>AddGranitIdentity()</c>
/// so OpenTelemetry exporters configured via <c>WithTracing(...)</c> pick it up
/// automatically.
/// </remarks>
internal static class IdentityActivitySource
{
    /// <summary>The name of the Granit.Identity <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Identity";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
