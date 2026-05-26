using System.Diagnostics.Metrics;
using Granit.TextExtraction.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Tests;

public sealed class TextExtractionMetricsTests : IDisposable
{
    private readonly TestMeterFactory _meterFactory = new();
    private readonly TextExtractionMetrics _metrics;

    public TextExtractionMetricsTests()
    {
        _metrics = new TextExtractionMetrics(_meterFactory);
    }

    public void Dispose() => _meterFactory.Dispose();

    [Fact]
    public void MeterName_is_Granit_TextExtraction() =>
        TextExtractionMetrics.MeterName.ShouldBe("Granit.TextExtraction");

    [Fact]
    public void RecordSuccess_increments_counter_with_global_tenant_when_null()
    {
        long total = 0;
        string? tenantTag = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument.Name == "granit.text_extraction.document.extracted")
            {
                ml.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            total += measurement;
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "tenant_id")
                {
                    tenantTag = tag.Value as string;
                }
            }
        });
        listener.Start();

        _metrics.RecordSuccess(tenantId: null, extractorName: "x", contentType: "text/plain");
        listener.RecordObservableInstruments();

        total.ShouldBe(1);
        tenantTag.ShouldBe("global");
    }

    [Fact]
    public void RecordTruncated_increments_truncated_counter()
    {
        long total = 0;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument.Name == "granit.text_extraction.document.truncated")
            {
                ml.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => total += measurement);
        listener.Start();

        _metrics.RecordTruncated("tenant-1", "x", "text/plain");
        listener.RecordObservableInstruments();

        total.ShouldBe(1);
    }

    [Fact]
    public void RecordSkipped_includes_content_type_tag()
    {
        string? typeTag = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument.Name == "granit.text_extraction.document.skipped")
            {
                ml.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "content_type")
                {
                    typeTag = tag.Value as string;
                }
            }
        });
        listener.Start();

        _metrics.RecordSkipped("t", "application/x-unknown");
        listener.RecordObservableInstruments();

        typeTag.ShouldBe("application/x-unknown");
    }

    [Fact]
    public void NormalizeContentType_strips_parameters_and_lowercases()
    {
        // A malicious upstream submitting a fresh boundary= per request would
        // explode metric cardinality unless we normalise. NormalizeContentType is the
        // single chokepoint — all four RecordXxx call it.
        TextExtractionMetrics.NormalizeContentType("APPLICATION/JSON; charset=utf-8")
            .ShouldBe("application/json");
        TextExtractionMetrics.NormalizeContentType("Multipart/Form-Data; boundary=----xyz")
            .ShouldBe("multipart/form-data");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a media type")]
    [InlineData("/no-type")]
    public void NormalizeContentType_returns_invalid_sentinel_on_malformed_input(string? raw) =>
        TextExtractionMetrics.NormalizeContentType(raw).ShouldBe(TextExtractionMetrics.InvalidContentTypeTag);

    [Fact]
    public void RecordSkipped_normalises_content_type_tag_dropping_boundary_parameter()
    {
        string? typeTag = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument.Name == "granit.text_extraction.document.skipped")
            {
                ml.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "content_type")
                {
                    typeTag = tag.Value as string;
                }
            }
        });
        listener.Start();

        // The boundary= parameter must NOT survive into the tag — that's the cardinality bomb.
        _metrics.RecordSkipped("t", "application/x-unknown; boundary=attacker-controlled");
        listener.RecordObservableInstruments();

        typeTag.ShouldBe("application/x-unknown");
    }

    [Fact]
    public void RecordFailed_includes_reason_tag()
    {
        string? reasonTag = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument.Name == "granit.text_extraction.document.failed")
            {
                ml.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "reason")
                {
                    reasonTag = tag.Value as string;
                }
            }
        });
        listener.Start();

        _metrics.RecordFailed("t", "x", "text/plain", "input_too_large");
        listener.RecordObservableInstruments();

        reasonTag.ShouldBe("input_too_large");
    }
}
