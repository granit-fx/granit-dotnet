using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.OpenIddict.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the OpenIddict module.
/// Meter: <c>Granit.OpenIddict</c>.
/// </summary>
/// <param name="meterFactory">The meter factory for creating instruments.</param>
public sealed class OpenIddictMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.OpenIddict";

    private readonly Counter<long> _tokensIssued = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.tokens.issued",
        description: "Number of tokens issued.");

    private readonly Counter<long> _tokensRevoked = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.tokens.revoked",
        description: "Number of tokens revoked.");

    private readonly Counter<long> _authenticationFailures = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.authentication.failures",
        description: "Number of failed authentication attempts.");

    private readonly Counter<long> _registrations = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.registrations",
        description: "Number of user registrations.");

    private readonly Histogram<double> _tokenIssuanceDuration = meterFactory.Create(MeterName).CreateHistogram<double>(
        "granit.openiddict.token.issuance.duration",
        unit: "s",
        description: "Duration of token issuance in seconds.");

    /// <summary>Records a token issuance.</summary>
    public void RecordTokenIssued(string? tenantId, string grantType) =>
        _tokensIssued.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "grant_type", grantType },
        });

    /// <summary>Records a token revocation.</summary>
    public void RecordTokenRevoked(string? tenantId, string reason) =>
        _tokensRevoked.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "reason", reason },
        });

    /// <summary>Records a failed authentication attempt.</summary>
    public void RecordAuthenticationFailure(string? tenantId, string reason) =>
        _authenticationFailures.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "reason", reason },
        });

    /// <summary>Records a user registration.</summary>
    public void RecordRegistration(string? tenantId) =>
        _registrations.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    /// <summary>Records the duration of a token issuance.</summary>
    public void RecordTokenIssuanceDuration(string? tenantId, string grantType, TimeSpan duration) =>
        _tokenIssuanceDuration.Record(duration.TotalSeconds, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "grant_type", grantType },
        });
}
