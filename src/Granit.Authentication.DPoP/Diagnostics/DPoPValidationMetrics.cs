using System.Diagnostics.Metrics;

namespace Granit.Authentication.DPoP.Diagnostics;

/// <summary>
/// Metrics for DPoP proof validation.
/// </summary>
internal sealed class DPoPValidationMetrics(IMeterFactory meterFactory)
{
    private readonly Meter _meter = meterFactory.Create("Granit.Authentication.DPoP");

    private Counter<long>? _validationSuccess;
    private Counter<long>? _validationFailure;
    private Counter<long>? _replayDetected;

    internal void RecordSuccess() =>
        (_validationSuccess ??= _meter.CreateCounter<long>("granit.authentication.dpop.validation.success")).Add(1);

    internal void RecordFailure(string reason) =>
        (_validationFailure ??= _meter.CreateCounter<long>("granit.authentication.dpop.validation.failure")).Add(1,
            new KeyValuePair<string, object?>("reason", reason));

    internal void RecordReplayDetected() =>
        (_replayDetected ??= _meter.CreateCounter<long>("granit.authentication.dpop.replay.detected")).Add(1);
}
