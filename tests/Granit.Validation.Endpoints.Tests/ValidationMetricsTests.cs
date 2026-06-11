using System.Diagnostics.Metrics;
using Granit.Validation.Endpoints.Diagnostics;
using Granit.Validation.Endpoints.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

public sealed class ValidationMetricsTests
{
    [Fact]
    public void RecordFieldValidated_KnownCode_TagsCodeStatusAndGlobalTenant()
    {
        using var recorder = Recorder.Start();

        recorder.Metrics.RecordFieldValidated(tenantId: null, "Validation:InvalidIban", ValidationFieldStatus.Valid);

        Measurement m = recorder.Single();
        m.Value.ShouldBe(1);
        m.Tag("error_code").ShouldBe("Validation:InvalidIban");
        m.Tag("status").ShouldBe("Valid");
        m.Tag("tenant_id").ShouldBe("global");
    }

    [Fact]
    public void RecordFieldValidated_UnknownCode_IsCappedToSentinel()
    {
        // The raw client-supplied code must never reach the tag (cardinality bomb under a scan).
        using var recorder = Recorder.Start();

        recorder.Metrics.RecordFieldValidated(tenantId: null, errorCode: null, ValidationFieldStatus.ValidatorNotFound);

        Measurement m = recorder.Single();
        m.Tag("error_code").ShouldBe("(unknown)");
        m.Tag("status").ShouldBe("ValidatorNotFound");
    }

    [Fact]
    public void RecordFieldValidated_ResolvedTenant_IsTagged()
    {
        using var recorder = Recorder.Start();

        recorder.Metrics.RecordFieldValidated("tenant-7", "Validation:InvalidEmail", ValidationFieldStatus.Invalid);

        Measurement m = recorder.Single();
        m.Tag("tenant_id").ShouldBe("tenant-7");
        m.Tag("status").ShouldBe("Invalid");
    }

    // -------------------------------------------------------------------------
    // Helpers — a minimal in-process MeterListener collector (no extra package).
    // -------------------------------------------------------------------------

    private sealed record Measurement(long Value, KeyValuePair<string, object?>[] Tags)
    {
        public object? Tag(string key) => Tags.Single(t => t.Key == key).Value;
    }

    private sealed class Recorder : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly List<Measurement> _measurements = [];

        public ValidationMetrics Metrics { get; }

        private Recorder()
        {
            IMeterFactory factory = new ServiceCollection().AddMetrics()
                .BuildServiceProvider().GetRequiredService<IMeterFactory>();
            Metrics = new ValidationMetrics(factory);

            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == ValidationMetrics.MeterName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _listener.SetMeasurementEventCallback<long>(
                (_, measurement, tags, _) => _measurements.Add(new Measurement(measurement, tags.ToArray())));
            _listener.Start();
        }

        public static Recorder Start() => new();

        // Measurements are delivered synchronously on Add, so the list is already populated.
        public Measurement Single() => _measurements.ShouldHaveSingleItem();

        public void Dispose() => _listener.Dispose();
    }
}
