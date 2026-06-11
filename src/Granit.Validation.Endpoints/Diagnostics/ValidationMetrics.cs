using System.Diagnostics;
using System.Diagnostics.Metrics;
using Granit.Validation.Endpoints.Dtos;

namespace Granit.Validation.Endpoints.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the public server-side field-validation endpoints.
/// Meter: <c>Granit.Validation.Endpoints</c>.
/// </summary>
/// <remarks>
/// The endpoints are unauthenticated by default and abuse-prone (rate-limiting is
/// recommended), so a spike in <c>ValidatorNotFound</c> outcomes is a useful
/// enumeration/scan signal and a high <c>Invalid</c> ratio flags a broken client.
/// </remarks>
public sealed class ValidationMetrics
{
    /// <summary>Name of the <see cref="Meter"/> owned by this package.</summary>
    public const string MeterName = "Granit.Validation.Endpoints";

    private readonly Counter<long> _fieldValidations;

    /// <summary>Initializes a new <see cref="ValidationMetrics"/> bound to <paramref name="meterFactory"/>.</summary>
    public ValidationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _fieldValidations = meter.CreateCounter<long>(
            "granit.validation.field.validated",
            description: "Server-side field validations performed via the validation endpoints, tagged with the error code and outcome.");
    }

    /// <summary>
    /// Records a single field validation.
    /// </summary>
    /// <param name="tenantId">
    /// Resolved tenant, or <see langword="null"/> for the anonymous/public case — coalesced to <c>"global"</c>.
    /// </param>
    /// <param name="errorCode">
    /// The registered validator code, or <see langword="null"/> when the requested code is unknown.
    /// The raw client-supplied code is deliberately NOT tagged in the unknown case (it is unbounded and
    /// would explode tag cardinality under a scan); it is recorded as <c>"(unknown)"</c> instead.
    /// </param>
    /// <param name="status">The validation outcome.</param>
    public void RecordFieldValidated(string? tenantId, string? errorCode, ValidationFieldStatus status)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("error_code", errorCode ?? "(unknown)"),
            new("status", status.ToString()),
        ];

        _fieldValidations.Add(1, tags);
    }
}
