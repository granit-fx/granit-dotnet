using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Http.Security.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the HTTP-security module.
/// Meter: <c>Granit.HttpSecurity</c>.
/// </summary>
public sealed class HttpSecurityMetrics
{
    /// <summary>Name of the <see cref="Meter"/> owned by this module.</summary>
    public const string MeterName = "Granit.HttpSecurity";

    private readonly Counter<long> _validations;
    private readonly Counter<long> _blocks;

    /// <summary>Initializes a new <see cref="HttpSecurityMetrics"/> bound to <paramref name="meterFactory"/>.</summary>
    public HttpSecurityMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _validations = meter.CreateCounter<long>(
            "granit.http_security.url.validated",
            description: "Number of URLs passed through IUrlSafetyValidator.");

        _blocks = meter.CreateCounter<long>(
            "granit.http_security.url.blocked",
            description: "Number of URLs blocked by IUrlSafetyValidator, tagged with the violation kind.");
    }

    /// <summary>Records a URL that passed every safety rule.</summary>
    public void RecordValid(string? tenantId)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("outcome", "valid"),
        ];
        _validations.Add(1, tags);
    }

    /// <summary>Records a URL that was rejected, tagged with its violation kind.</summary>
    public void RecordBlocked(string? tenantId, UrlSafetyViolationKind kind)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("outcome", "blocked"),
            new("violation_kind", kind.ToString()),
        ];
        _validations.Add(1, tags);
        _blocks.Add(1, tags);
    }
}
