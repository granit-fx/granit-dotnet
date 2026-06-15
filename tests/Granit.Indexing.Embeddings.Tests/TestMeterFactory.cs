using System.Diagnostics.Metrics;
using Granit.Indexing.Embeddings.Diagnostics;

namespace Granit.Indexing.Embeddings.Tests;

/// <summary>
/// Minimal <see cref="IMeterFactory"/> exposing a single <see cref="Meter"/> named
/// <see cref="EmbeddingsMetrics.MeterName"/> so tests can attach a
/// <c>MetricCollector</c> and assert on emitted measurements.
/// </summary>
internal sealed class TestMeterFactory : IMeterFactory
{
    public Meter Meter { get; } = new(EmbeddingsMetrics.MeterName);

    public Meter Create(MeterOptions options) => Meter;

    public void Dispose() => Meter.Dispose();
}
