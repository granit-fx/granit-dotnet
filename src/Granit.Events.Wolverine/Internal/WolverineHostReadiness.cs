using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Events.Wolverine.Internal;

/// <summary>
/// Tracks whether the host has fully started (all <see cref="IHostedService.StartAsync"/>
/// calls completed). Used by Wolverine event bus adapters to fall back gracefully
/// when Wolverine is not yet ready (e.g., during data seeding or <c>--migrate</c> mode).
/// </summary>
/// <remarks>
/// Registered as a singleton AND as an <see cref="IHostedLifecycleService"/>.
/// The <see cref="IHostedLifecycleService.StartedAsync"/> callback fires after ALL
/// hosted services (including Wolverine) have completed <c>StartAsync</c>,
/// making it the safe point to enable Wolverine-backed event publishing.
/// </remarks>
public sealed partial class WolverineHostReadiness(
    ILogger<WolverineHostReadiness> logger) : IHostedLifecycleService
{
    private volatile bool _isReady;

    /// <summary>
    /// Returns <c>true</c> after all <see cref="IHostedService.StartAsync"/> calls
    /// have completed (including Wolverine's runtime initialization).
    /// </summary>
    internal bool IsReady => _isReady;

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IHostedService.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IHostedLifecycleService.StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IHostedLifecycleService.StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IHostedLifecycleService.StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task IHostedLifecycleService.StartedAsync(CancellationToken cancellationToken)
    {
        _isReady = true;
        LogWolverineReady();
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Wolverine host readiness flag set — event bus adapters switching to Wolverine pipeline.")]
    private partial void LogWolverineReady();
}
