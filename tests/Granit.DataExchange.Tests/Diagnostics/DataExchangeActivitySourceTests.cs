using System.Diagnostics;
using Granit.DataExchange.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Diagnostics;

public sealed class DataExchangeActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public DataExchangeActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DataExchangeActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Name_IsGranitDataExchange() =>
        DataExchangeActivitySource.Name.ShouldBe("Granit.DataExchange");

    [Fact]
    public void StartActivity_ImportExecute_ReturnsActivity()
    {
        using Activity? activity = DataExchangeActivitySource.Source.StartActivity(
            DataExchangeActivitySource.ImportExecute);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("data-exchange.import.execute");
    }

    [Fact]
    public void StartActivity_ExportExecute_ReturnsActivity()
    {
        using Activity? activity = DataExchangeActivitySource.Source.StartActivity(
            DataExchangeActivitySource.ExportExecute);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("data-exchange.export.execute");
    }
}
