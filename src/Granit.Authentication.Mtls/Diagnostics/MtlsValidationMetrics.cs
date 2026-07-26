using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Authentication.Mtls.Diagnostics;

/// <summary>
/// Metrics for mutual-TLS certificate-bound token validation.
/// Meter: <c>Granit.Authentication.Mtls</c>.
/// </summary>
internal sealed class MtlsValidationMetrics
{
    public const string MeterName = "Granit.Authentication.Mtls";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _validationSuccess;
    private readonly Counter<long> _validationFailure;

    public MtlsValidationMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _validationSuccess = meter.CreateCounter<long>(
            "granit.authentication.mtls.validation.success",
            description: "Number of successful certificate-bound token validations.");

        _validationFailure = meter.CreateCounter<long>(
            "granit.authentication.mtls.validation.failure",
            description: "Number of failed certificate-bound token validations.");
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
}
