using System.Diagnostics.Metrics;
using Granit.OpenIddict.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Diagnostics;

public sealed class OpenIddictMetricsTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMeterFactory _meterFactory;
    private readonly OpenIddictMetrics _metrics;

    public OpenIddictMetricsTests()
    {
        _serviceProvider = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();

        _meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
        _metrics = new OpenIddictMetrics(_meterFactory);
    }

    public void Dispose() => _serviceProvider.Dispose();

    private MetricCollector<long> CollectLong(string instrument) =>
        new(_meterFactory, OpenIddictMetrics.MeterName, instrument);

    [Fact]
    public void MeterName_Is_Granit_OpenIddict() =>
        OpenIddictMetrics.MeterName.ShouldBe("Granit.OpenIddict");

    [Fact]
    public void RecordTokenIssued_EmitsWithTenantAndGrantTypeTags()
    {
        using MetricCollector<long> collector = CollectLong("granit.openiddict.tokens.issued");

        _metrics.RecordTokenIssued("tenant-1", "authorization_code");

        CollectedMeasurement<long> measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["tenant_id"].ShouldBe("tenant-1");
        measurement.Tags["grant_type"].ShouldBe("authorization_code");
    }

    [Fact]
    public void RecordTokenIssued_NullTenant_CoalescesTenantIdToGlobal()
    {
        using MetricCollector<long> collector = CollectLong("granit.openiddict.tokens.issued");

        _metrics.RecordTokenIssued(null, "client_credentials");

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordTokenRevoked_EmitsWithReasonTag()
    {
        using MetricCollector<long> collector = CollectLong("granit.openiddict.tokens.revoked");

        _metrics.RecordTokenRevoked("tenant-1", "invalid_token");

        CollectedMeasurement<long> measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["reason"].ShouldBe("invalid_token");
    }

    [Fact]
    public void RecordTokenRevoked_UnknownReason_IsSanitizedToUnknown()
    {
        using MetricCollector<long> collector = CollectLong("granit.openiddict.tokens.revoked");

        _metrics.RecordTokenRevoked("tenant-1", "arbitrary-attacker-value");

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["reason"].ShouldBe("unknown");
    }

    [Fact]
    public void RecordAuthenticationSuccess_EmitsWithGrantTypeTag()
    {
        using MetricCollector<long> collector = CollectLong("granit.openiddict.authentication.successes");

        _metrics.RecordAuthenticationSuccess("tenant-1", "authorization_code");

        CollectedMeasurement<long> measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["grant_type"].ShouldBe("authorization_code");
    }

    [Fact]
    public void RecordAuthenticationFailure_EmitsWithReasonTag()
    {
        using MetricCollector<long> collector = CollectLong("granit.openiddict.authentication.failures");

        _metrics.RecordAuthenticationFailure("tenant-1", "invalid_credentials");

        CollectedMeasurement<long> measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["reason"].ShouldBe("invalid_credentials");
    }

    [Fact]
    public void RecordAuthenticationFailure_NullTenant_CoalescesTenantIdToGlobal()
    {
        using MetricCollector<long> collector = CollectLong("granit.openiddict.authentication.failures");

        _metrics.RecordAuthenticationFailure(null, "invalid_credentials");

        collector.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordKeyRotation_EmitsWithKeyCountTags()
    {
        using MetricCollector<long> collector = CollectLong("granit.openiddict.keys.rotated");

        _metrics.RecordKeyRotation("tenant-1", keysGenerated: 3, keysRetired: 2, keysRevoked: 1);

        CollectedMeasurement<long> measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["keys_generated"].ShouldBe(3);
        measurement.Tags["keys_retired"].ShouldBe(2);
        measurement.Tags["keys_revoked"].ShouldBe(1);
    }

    [Fact]
    public void RecordTokenIssuanceDuration_RecordsHistogramInSeconds()
    {
        using MetricCollector<double> collector =
            new(_meterFactory, OpenIddictMetrics.MeterName, "granit.openiddict.token.issuance.duration");

        _metrics.RecordTokenIssuanceDuration("tenant-1", "authorization_code", TimeSpan.FromMilliseconds(150));

        CollectedMeasurement<double> measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(0.15, 0.001);
        measurement.Tags["grant_type"].ShouldBe("authorization_code");
    }
}
