using System.Diagnostics;
using System.Diagnostics.Metrics;
using Granit.Imaging.Diagnostics;
using Granit.Imaging.MagickNet.Internal;
using Granit.Imaging.MagickNet.Options;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests.Internal;

public sealed class ImagingTelemetryTests
{
    private static Stream GetTestImageStream() =>
        typeof(ImagingTelemetryTests).Assembly
            .GetManifestResourceStream("Granit.Imaging.MagickNet.Tests.TestAssets.test-image.png")!;

    private static MagickNetImageProcessor CreateProcessor(ICurrentTenant tenant, out Meter meter)
    {
        Meter localMeter = new(ImagingMetrics.MeterName);
        meter = localMeter;
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(localMeter);
        return new MagickNetImageProcessor(
            new ImagingMetrics(factory),
            tenant,
            Microsoft.Extensions.Options.Options.Create(new ImagingMagickNetOptions()));
    }

    [Fact]
    public async Task RecordMetrics_WithAvailableTenant_TagsTheRealTenantId()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(tenantId);

        string? observedTenantTag = await CaptureTenantTagAsync(tenant);

        observedTenantTag.ShouldBe(tenantId.ToString());
    }

    [Fact]
    public async Task RecordMetrics_WithoutTenant_CoalescesToGlobal()
    {
        string? observedTenantTag = await CaptureTenantTagAsync(NullTenantContext.Instance);

        observedTenantTag.ShouldBe("global");
    }

    [Fact]
    public async Task TerminalOperation_EmitsEncodeActivityWithOutputFormat()
    {
        List<Activity> activities = [];
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == "Granit.Imaging.MagickNet",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);

        // The listener is process-global, so a MagickNet test class running in parallel
        // emits its own imaging.load/imaging.encode activities into this same list. Anchor
        // a correlation activity so the pipeline's activities inherit its TraceId, then keep
        // only ours — otherwise First(...) can grab a foreign activity and flake.
        using Activity correlation = new Activity(nameof(TerminalOperation_EmitsEncodeActivityWithOutputFormat)).Start();

        MagickNetImageProcessor processor = CreateProcessor(NullTenantContext.Instance, out Meter meter);
        using (meter)
        {
            await using Stream stream = GetTestImageStream();
            await using IImagePipeline pipeline = await processor.LoadAsync(stream, TestContext.Current.CancellationToken);
            await pipeline.ConvertTo(ImageFormat.Jpeg).ToResultAsync(TestContext.Current.CancellationToken);
        }

        Activity load = activities.First(a => a.OperationName == "imaging.load" && a.TraceId == correlation.TraceId);
        load.GetTagItem("imaging.source_format").ShouldBe("Png");

        Activity encode = activities.First(a => a.OperationName == "imaging.encode" && a.TraceId == correlation.TraceId);
        encode.GetTagItem("imaging.output_format").ShouldBe("Jpeg");
        encode.GetTagItem("imaging.width").ShouldBe(100);
    }

    private static async Task<string?> CaptureTenantTagAsync(ICurrentTenant tenant)
    {
        MagickNetImageProcessor processor = CreateProcessor(tenant, out Meter meter);

        string? observedTenantTag = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter == meter && instrument.Name == "granit.imaging.image.processed")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "tenant_id")
                {
                    observedTenantTag = tag.Value?.ToString();
                }
            }
        });
        listener.Start();

        using (meter)
        {
            await using Stream stream = GetTestImageStream();
            await using IImagePipeline pipeline = await processor.LoadAsync(stream, TestContext.Current.CancellationToken);
            await pipeline.ToResultAsync(TestContext.Current.CancellationToken);
        }

        return observedTenantTag;
    }
}
