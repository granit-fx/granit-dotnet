using System.Diagnostics;

namespace Granit.Metering.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Metering distributed tracing.
/// </summary>
internal static class MeteringActivitySource
{
    internal const string Name = "Granit.Metering";

    internal static readonly ActivitySource Source = new(Name);

    internal const string RecordEvent = "metering.record_event";
    internal const string RecordBatch = "metering.record_batch";
    internal const string Aggregate = "metering.aggregate";
    internal const string CheckQuota = "metering.check_quota";
}
