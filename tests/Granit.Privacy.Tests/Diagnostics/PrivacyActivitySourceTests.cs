using System.Diagnostics;
using Granit.Privacy.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.Diagnostics;

public sealed class PrivacyActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public PrivacyActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PrivacyActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Name_IsGranitPrivacy() =>
        PrivacyActivitySource.Name.ShouldBe("Granit.Privacy");

    [Fact]
    public void StartActivity_ExportExecute_ReturnsActivity()
    {
        using Activity? activity = PrivacyActivitySource.Source.StartActivity(
            PrivacyActivitySource.ExportExecute);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("privacy.export.execute");
    }

    [Fact]
    public void StartActivity_DeletionExecute_ReturnsActivity()
    {
        using Activity? activity = PrivacyActivitySource.Source.StartActivity(
            PrivacyActivitySource.DeletionExecute);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("privacy.deletion.execute");
    }
}
