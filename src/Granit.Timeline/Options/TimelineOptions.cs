namespace Granit.Timeline.Options;

/// <summary>
/// Tunables for the federated activity stream.
/// </summary>
public sealed class TimelineOptions
{
    /// <summary>
    /// Maximum 1-based page accepted by <c>ITimelineReader.GetStreamAsync</c>.
    /// Beyond this, the endpoint returns a 400 <c>timeline-depth-exceeded</c>
    /// problem detail. Federated pagination cost scales with depth (every
    /// page re-fetches a top-(page × pageSize) slice from every source); the
    /// cap keeps a misbehaving client from hammering the DB.
    /// </summary>
    public int MaxPage { get; set; } = 10;

    /// <summary>
    /// Per-source timeout. A source slower than this is reported as
    /// degraded — its entries are dropped from the response when
    /// <see cref="OnSourceFailure"/> is <see cref="SourceFailurePolicy.DegradeGracefully"/>,
    /// or rethrown when <see cref="SourceFailurePolicy.ThrowAll"/>.
    /// </summary>
    public TimeSpan SourceTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Behavior when a registered <see cref="Abstractions.ITimelineSource"/> times out
    /// or throws. Production defaults to <see cref="SourceFailurePolicy.DegradeGracefully"/>;
    /// dev/test environments should set <see cref="SourceFailurePolicy.ThrowAll"/> so
    /// regressions surface loudly.
    /// </summary>
    public SourceFailurePolicy OnSourceFailure { get; set; } = SourceFailurePolicy.DegradeGracefully;

    /// <summary>
    /// Window during which the author of a Comment or InternalNote may edit
    /// their own body. <see cref="TimeSpan.Zero"/> disables editing entirely.
    /// Enforced server-side by <c>ITimelineWriter.UpdateEntryBodyAsync</c>;
    /// front-ends should mirror the check to hide the edit affordance, but the
    /// writer is the source of truth.
    /// </summary>
    public TimeSpan EditWindow { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>How <c>ITimelineReader</c> reacts when a source contributor fails.</summary>
public enum SourceFailurePolicy
{
    /// <summary>
    /// Drop the failing source from the merged response and surface it via the
    /// <c>X-Timeline-Degraded-Sources</c> header. Recommended for production.
    /// </summary>
    DegradeGracefully = 0,

    /// <summary>Rethrow the first source failure as a 500. Use in dev/test to fail loud.</summary>
    ThrowAll = 1,
}
