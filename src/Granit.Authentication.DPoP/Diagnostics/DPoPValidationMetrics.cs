using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Authentication.DPoP.Diagnostics;

/// <summary>
/// Metrics for DPoP proof validation.
/// Meter: <c>Granit.Authentication.DPoP</c>.
/// </summary>
internal sealed class DPoPValidationMetrics
{
    public const string MeterName = "Granit.Authentication.DPoP";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _validationSuccess;
    private readonly Counter<long> _validationFailure;
    private readonly Counter<long> _replayDetected;

    public DPoPValidationMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _validationSuccess = meter.CreateCounter<long>(
            "granit.authentication.dpop.validation.success",
            description: "Number of successful DPoP proof validations.");

        _validationFailure = meter.CreateCounter<long>(
            "granit.authentication.dpop.validation.failure",
            description: "Number of failed DPoP proof validations.");

        _replayDetected = meter.CreateCounter<long>(
            "granit.authentication.dpop.replay.detected",
            description: "Number of DPoP proof replays detected.");
    }

    internal void RecordSuccess(string? tenantId) =>
        _validationSuccess.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    internal void RecordFailure(string reason, string? tenantId) =>
        _validationFailure.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "reason", reason },
        });

    internal void RecordReplayDetected(string? tenantId) =>
        _replayDetected.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });
}
