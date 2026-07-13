using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Http.UrlSafety.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the URL-safety module.
/// Meter: <c>Granit.Http.UrlSafety</c>.
/// </summary>
/// <remarks>
/// No <c>tenant_id</c> tag: outbound URL checks are high-frequency and a raw tenant
/// dimension is an unbounded-cardinality liability on counters. Tenant correlation
/// belongs to traces/logs via the Serilog enrichers (ADR-001).
/// </remarks>
public sealed class UrlSafetyMetrics
{
    /// <summary>Name of the <see cref="Meter"/> owned by this module.</summary>
    public const string MeterName = "Granit.Http.UrlSafety";

    private readonly Counter<long> _validations;
    private readonly Counter<long> _blocks;

    /// <summary>Initializes a new <see cref="UrlSafetyMetrics"/> bound to <paramref name="meterFactory"/>.</summary>
    public UrlSafetyMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _validations = meter.CreateCounter<long>(
            "granit.http.urlsafety.url.validated",
            description: "Number of URLs passed through IUrlSafetyValidator.");

        _blocks = meter.CreateCounter<long>(
            "granit.http.urlsafety.url.blocked",
            description: "Number of URLs blocked by IUrlSafetyValidator, tagged with the violation kind.");
    }

    /// <summary>Records a URL that passed every safety rule.</summary>
    public void RecordValid()
    {
        TagList tags =
        [
            new("outcome", "valid"),
        ];
        _validations.Add(1, tags);
    }

    /// <summary>Records a URL that was rejected, tagged with its violation kind.</summary>
    public void RecordBlocked(UrlSafetyViolationKind kind)
    {
        TagList tags =
        [
            new("outcome", "blocked"),
            new("violation_kind", kind.ToString()),
        ];
        _blocks.Add(1, tags);
    }
}
