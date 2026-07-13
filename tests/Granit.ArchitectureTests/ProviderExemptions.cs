namespace Granit.ArchitectureTests;

/// <summary>
/// Grandfathered violations of the notification-provider template
/// (<see cref="ProviderConventionTests"/>). Every entry MUST carry an inline justification
/// and its removal issue. A companion fact fails when an entry stops being necessary, so
/// this list can only shrink (honest-backlog pattern, cf. ValidationKeyConventionTests).
/// </summary>
/// <remarks>
/// All lists were emptied by phases 1b/2 (#2960, #2961): every notification provider now
/// conforms to the canonical template. A new provider that violates a rule must be fixed,
/// not exempted — add an entry only for a documented, time-boxed exception.
/// </remarks>
internal static class ProviderExemptions
{
    /// <summary>Provider modules not yet self-registering in <c>ConfigureServices</c>.</summary>
    public static readonly HashSet<string> SelfRegistrationPending = new(StringComparer.Ordinal);

    /// <summary>Providers without a registered ActivitySource.</summary>
    public static readonly HashSet<string> ActivitySourcePending = new(StringComparer.Ordinal);

    /// <summary>Providers without a health check.</summary>
    public static readonly HashSet<string> HealthCheckPending = new(StringComparer.Ordinal);

    /// <summary>Providers nested under a channel package.</summary>
    public static readonly HashSet<string> PlacementPending = new(StringComparer.Ordinal);

    /// <summary>Providers whose options validation is a no-op.</summary>
    public static readonly HashSet<string> ValidationPending = new(StringComparer.Ordinal);

    /// <summary>Providers without a SectionName assertion test.</summary>
    public static readonly HashSet<string> SectionNameTestPending = new(StringComparer.Ordinal);

    /// <summary>Providers whose module [DependsOn] omits direct module references.</summary>
    public static readonly HashSet<string> DependsOnPending = new(StringComparer.Ordinal);
}
