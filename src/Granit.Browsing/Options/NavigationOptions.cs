namespace Granit.Browsing.Options;

/// <summary>Options controlling a navigation or content-replacement operation.</summary>
public sealed record NavigationOptions
{
    /// <summary>
    /// Maximum time to wait for the navigation to complete. <c>null</c> uses the engine's
    /// default (typically 30 seconds).
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Phase the navigation must reach before the call returns. Defaults to
    /// <see cref="LoadState.Load"/>.
    /// </summary>
    public LoadState WaitUntil { get; init; } = LoadState.Load;

    /// <summary>
    /// Optional <c>Referer</c> header sent on the navigation. Ignored on
    /// <c>SetContentAsync</c>.
    /// </summary>
    public string? Referer { get; init; }
}
