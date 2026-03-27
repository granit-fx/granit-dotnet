using System.Diagnostics.Metrics;

namespace Granit.Testing.EntityFrameworkCore.Internal;

/// <summary>
/// Minimal <see cref="IMeterFactory"/> for test infrastructure that creates
/// real <see cref="Meter"/> instances without requiring a DI container.
/// </summary>
internal sealed class TestMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options) => new(options);

    public void Dispose()
    {
        // Nothing to dispose — meters created by callers manage their own lifetime.
    }
}
