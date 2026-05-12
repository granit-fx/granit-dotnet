using System.Text.Json;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Rendering;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Tests.Rendering;

/// <summary>
/// Locks the wire shape of <see cref="WidgetSnapshotEnvelope"/> — the contract
/// surfaced by every dashboard render endpoint and consumed by the frontend
/// `useDashboard` hook. Envelope changes here ripple to TypeScript types in
/// `granit-front/@granit/analytics`; keep tests narrow and named so a diff is
/// obvious in PR review.
/// </summary>
public sealed class WidgetSnapshotEnvelopeTests
{
    private static readonly DateTimeOffset SampleTime = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions PascalCaseEnumOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    [Fact]
    public void ForSnapshot_BuildsEnvelopeWithStatusSnapshotAndPayload()
    {
        JsonElement payload = JsonSerializer.SerializeToElement(new { value = 42 });

        var envelope = WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: "Kpi",
            snapshot: payload,
            sequence: 1,
            emittedAt: SampleTime,
            refreshHint: RefreshHint.Dynamic);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        envelope.WidgetType.ShouldBe("Kpi");
        envelope.Snapshot.ShouldNotBeNull();
        envelope.Sequence.ShouldBe(1);
        envelope.EmittedAt.ShouldBe(SampleTime);
        envelope.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        envelope.ReasonLocalizationKey.ShouldBeNull();
    }

    [Fact]
    public void Unavailable_BuildsEnvelopeWithoutSnapshot_AndDefaultReasonKey()
    {
        var envelope = WidgetSnapshotEnvelope.Unavailable(
            widgetType: "Chart",
            sequence: 1,
            emittedAt: SampleTime,
            refreshHint: RefreshHint.Dynamic);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        envelope.WidgetType.ShouldBe("Chart");
        envelope.Snapshot.ShouldBeNull();
        envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable");
    }

    [Fact]
    public void Unavailable_AcceptsCustomReasonKey()
    {
        var envelope = WidgetSnapshotEnvelope.Unavailable(
            widgetType: "Kpi",
            sequence: 1,
            emittedAt: SampleTime,
            refreshHint: RefreshHint.Static,
            reasonLocalizationKey: "Widget:Unavailable.AliasUnresolved");

        envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.AliasUnresolved");
    }

    [Fact]
    public void Error_BuildsEnvelopeWithoutSnapshot_AndDefaultReasonKey()
    {
        var envelope = WidgetSnapshotEnvelope.Error(
            widgetType: "Map",
            sequence: 1,
            emittedAt: SampleTime,
            refreshHint: RefreshHint.Dynamic);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Error);
        envelope.WidgetType.ShouldBe("Map");
        envelope.Snapshot.ShouldBeNull();
        envelope.ReasonLocalizationKey.ShouldBe("Widget:Error");
    }

    [Fact]
    public void Error_AcceptsCustomReasonKey()
    {
        var envelope = WidgetSnapshotEnvelope.Error(
            widgetType: "Kpi",
            sequence: 1,
            emittedAt: SampleTime,
            refreshHint: RefreshHint.Static,
            reasonLocalizationKey: "Widget:Error.UnknownWidgetType");

        envelope.ReasonLocalizationKey.ShouldBe("Widget:Error.UnknownWidgetType");
    }

    [Fact]
    public void RoundTrip_PreservesAllFields_IncludingNestedSnapshot()
    {
        JsonElement original = JsonSerializer.SerializeToElement(new
        {
            value = 12,
            previous = 10,
            trend = "Up",
        });

        var envelope = WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: "Kpi",
            snapshot: original,
            sequence: 1,
            emittedAt: SampleTime,
            refreshHint: RefreshHint.Dynamic);

        string json = JsonSerializer.Serialize(envelope);
        WidgetSnapshotEnvelope? decoded = JsonSerializer.Deserialize<WidgetSnapshotEnvelope>(json);

        decoded.ShouldNotBeNull();
        decoded.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        decoded.WidgetType.ShouldBe("Kpi");
        decoded.Sequence.ShouldBe(1);
        decoded.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        decoded.Snapshot.ShouldNotBeNull();

        // Nested payload is materialised as JsonElement and travels by value through STJ —
        // not by reference to the original. Its property structure must still survive.
        decoded.Snapshot!.Value.GetProperty("value").GetInt32().ShouldBe(12);
        decoded.Snapshot.Value.GetProperty("previous").GetInt32().ShouldBe(10);
        decoded.Snapshot.Value.GetProperty("trend").GetString().ShouldBe("Up");
    }

    [Fact]
    public void Wire_serialises_Status_with_PascalCase_per_ADR_039_section_6_1()
    {
        // ADR-039 §6.1 — host JsonStringEnumConverter() default policy is PascalCase.
        // The test guards against an accidental switch to camelCase that would silently
        // break the frontend's discriminated-union literal types.
        var envelope = WidgetSnapshotEnvelope.Unavailable(
            widgetType: "Kpi",
            sequence: 1,
            emittedAt: SampleTime,
            refreshHint: RefreshHint.Dynamic);

        string json = JsonSerializer.Serialize(envelope, PascalCaseEnumOptions);

        // Shouldly's ShouldContain on strings is case-insensitive by default — use
        // Contains directly on the underlying string with explicit ordinal compare.
        json.Contains("\"Status\":\"Unavailable\"", StringComparison.Ordinal).ShouldBeTrue(json);
        json.Contains("\"RefreshHint\":\"Dynamic\"", StringComparison.Ordinal).ShouldBeTrue(json);
        json.Contains("\"unavailable\"", StringComparison.Ordinal).ShouldBeFalse(json);
        json.Contains("\"dynamic\"", StringComparison.Ordinal).ShouldBeFalse(json);
    }
}
