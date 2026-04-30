using System.Text.Json;
using Granit.Analytics;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Internal;
using Granit.Dashboards.Rendering;
using Granit.Dashboards.Widgets;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// Pins the wire shape locked by ADR-039 §6 — the JSON the endpoint emits
/// must travel through this projection unchanged from the typed envelopes the
/// renderer pipeline produces. Frontend types in
/// <c>granit-front/@granit/analytics</c> consume these fields directly; a
/// silent rename here ripples to TypeScript.
/// </summary>
public sealed class DashboardRenderProjectionTests
{
    private const string SourceDefinitionName = "Granit.Test.SampleDashboard";
    private static readonly DateTimeOffset RenderedAt = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToResponse_MapsEnvelopeFieldsOntoFlattenedWidgetRecords()
    {
        Dashboard dashboard = NewDashboard();
        WidgetInstance widget = dashboard.AddWidget(
            widgetId: Guid.NewGuid(),
            widgetType: "Kpi",
            position: 0,
            width: 4,
            height: 2,
            titleLocalizationKey: $"Widget:{SourceDefinitionName}.unpaid-total",
            configJson: "{}",
            requiredPermission: "Invoicing.Invoices.Read");

        JsonElement payload = JsonSerializer.SerializeToElement(new { value = 42 });

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id,
            RenderedAt: RenderedAt,
            Period: new ResolvedPeriod(RenderedAt.AddDays(-7), RenderedAt),
            Widgets:
            [
                new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.ForSnapshot(
                    widgetType: "Kpi",
                    snapshot: payload,
                    sequence: 1,
                    emittedAt: RenderedAt,
                    refreshHint: RefreshHint.Dynamic)),
            ]);

        IDashboardDefinitionRegistry registry = EmptyRegistry();

        ResolvedRenderTarget target = new(dashboard.Widgets, ActiveViewName: null);
        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard, target, registry, periodToken: "mtd");

        response.DashboardId.ShouldBe(dashboard.Id);
        response.RenderedAt.ShouldBe(RenderedAt);
        response.Period.ShouldNotBeNull();
        response.Period!.From.ShouldBe(RenderedAt.AddDays(-7));
        response.Period.To.ShouldBe(RenderedAt);
        response.Period.Token.ShouldBe("mtd");

        response.Widgets.Count.ShouldBe(1);
        DashboardRenderedWidgetResponse w = response.Widgets[0];
        w.Id.ShouldBe(widget.Id);
        w.WidgetType.ShouldBe("Kpi");
        w.Slug.ShouldBe("unpaid-total");
        w.Position.ShouldBe(0);
        w.Width.ShouldBe(4);
        w.Height.ShouldBe(2);
        w.TitleLocalizationKey.ShouldBe($"Widget:{SourceDefinitionName}.unpaid-total");
        w.RequiredPermission.ShouldBe("Invoicing.Invoices.Read");
        w.Actions.ShouldBeNull();
        w.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        w.Sequence.ShouldBe(1);
        w.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        w.Transport.ShouldBe(WidgetTransport.Pull);                          // default push policy + Dynamic hint = pull
        w.Snapshot.ShouldNotBeNull();
        w.Snapshot!.Value.GetProperty("value").GetInt32().ShouldBe(42);
        w.ReasonLocalizationKey.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_ProjectsWidgetActionsFromRegisteredDefinition()
    {
        Dashboard dashboard = NewDashboard();
        WidgetInstance widget = dashboard.AddWidget(
            widgetId: Guid.NewGuid(),
            widgetType: "Kpi",
            position: 0,
            width: 4,
            height: 2,
            titleLocalizationKey: $"Widget:{SourceDefinitionName}.click-target",
            configJson: "{}");

        IReadOnlyList<WidgetAction> actions =
        [
            new WidgetAction(WidgetActionTrigger.Click, WidgetActionKind.Navigate, "/invoicing?status=unpaid"),
        ];

        IDashboardDefinitionDescriptor descriptor = Substitute.For<IDashboardDefinitionDescriptor>();
        descriptor.Widgets.Returns(new[]
        {
            FakeWidgetDefinition("click-target", actions),
            FakeWidgetDefinition("unrelated", null),
        });

        IDashboardDefinitionRegistry registry = Substitute.For<IDashboardDefinitionRegistry>();
        registry.Find(SourceDefinitionName).Returns(descriptor);

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id,
            RenderedAt: RenderedAt,
            Period: null,
            Widgets:
            [
                new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.ForSnapshot(
                    widgetType: "Kpi",
                    snapshot: JsonSerializer.SerializeToElement(new { value = 1 }),
                    sequence: 1,
                    emittedAt: RenderedAt,
                    refreshHint: RefreshHint.Dynamic)),
            ]);

        ResolvedRenderTarget target = new(dashboard.Widgets, ActiveViewName: null);
        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard, target, registry, periodToken: null);

        DashboardRenderedWidgetResponse w = response.Widgets[0];
        w.Slug.ShouldBe("click-target");
        w.Actions.ShouldNotBeNull();
        w.Actions!.Count.ShouldBe(1);
        w.Actions[0].Trigger.ShouldBe(WidgetActionTrigger.Click);
        w.Actions[0].Kind.ShouldBe(WidgetActionKind.Navigate);
        w.Actions[0].Target.ShouldBe("/invoicing?status=unpaid");
    }

    [Fact]
    public void ToResponse_DashboardWithoutSourceDefinition_ReturnsNullActions()
    {
        // Custom-built dashboard (no SourceDefinitionName) — registry never queried.
        var dashboard = Dashboard.Create(
            id: Guid.NewGuid(),
            name: "Adhoc",
            category: DashboardCategory.General);
        WidgetInstance widget = dashboard.AddWidget(
            widgetId: Guid.NewGuid(),
            widgetType: "Markdown",
            position: 0,
            width: 12,
            height: 1,
            titleLocalizationKey: "Widget:Adhoc.banner",
            configJson: "{}");

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id,
            RenderedAt: RenderedAt,
            Period: null,
            Widgets:
            [
                new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.ForSnapshot(
                    widgetType: "Markdown",
                    snapshot: JsonSerializer.SerializeToElement(new { body = "hi" }),
                    sequence: 1,
                    emittedAt: RenderedAt,
                    refreshHint: RefreshHint.Static)),
            ]);

        ResolvedRenderTarget target = new(dashboard.Widgets, ActiveViewName: null);
        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard, target, EmptyRegistry(), periodToken: null);

        DashboardRenderedWidgetResponse w = response.Widgets[0];
        w.Actions.ShouldBeNull();
        w.Slug.ShouldBe("Adhoc.banner");                              // generic Widget: prefix stripped
    }

    [Fact]
    public void ToResponse_NullPeriod_OmitsPeriodEnvelopeField()
    {
        Dashboard dashboard = NewDashboard();
        DashboardRenderResult result = new(
            DashboardId: dashboard.Id,
            RenderedAt: RenderedAt,
            Period: null,
            Widgets: []);

        ResolvedRenderTarget target = new(dashboard.Widgets, ActiveViewName: null);
        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard, target, EmptyRegistry(), periodToken: null);

        response.Period.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_UnavailableEnvelope_PreservesReasonKey()
    {
        Dashboard dashboard = NewDashboard();
        WidgetInstance widget = dashboard.AddWidget(
            widgetId: Guid.NewGuid(),
            widgetType: "Kpi",
            position: 0,
            width: 4,
            height: 2,
            titleLocalizationKey: $"Widget:{SourceDefinitionName}.gated",
            configJson: "{}");

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id,
            RenderedAt: RenderedAt,
            Period: null,
            Widgets:
            [
                new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.Unavailable(
                    widgetType: "Kpi",
                    sequence: 1,
                    emittedAt: RenderedAt,
                    refreshHint: RefreshHint.Static,
                    reasonLocalizationKey: "Widget:Unavailable.MetricNotFound")),
            ]);

        ResolvedRenderTarget target = new(dashboard.Widgets, ActiveViewName: null);
        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard, target, EmptyRegistry(), periodToken: null);

        DashboardRenderedWidgetResponse w = response.Widgets[0];
        w.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        w.Snapshot.ShouldBeNull();
        w.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.MetricNotFound");
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("2026-04-01T00:00:00Z", null, null)]                 // single bound = no period
    [InlineData(null, "2026-05-01T00:00:00Z", null)]                 // single bound = no period
    [InlineData("2026-04-01T00:00:00Z", "2026-05-01T00:00:00Z", "mtd")]
    public void TryBuildResolvedPeriod_RequiresBothBounds(string? from, string? to, string? expectedToken)
    {
        DashboardRenderRequest request = new(
            PeriodFrom: from is null ? null : DateTimeOffset.Parse(from, System.Globalization.CultureInfo.InvariantCulture),
            PeriodTo: to is null ? null : DateTimeOffset.Parse(to, System.Globalization.CultureInfo.InvariantCulture));

        ResolvedPeriod? period = DashboardRenderProjection.TryBuildResolvedPeriod(request);

        if (from is null || to is null)
        {
            period.ShouldBeNull();
        }
        else
        {
            period.ShouldNotBeNull();
            // Asserting expectedToken non-null for completeness — the token isn't
            // part of ResolvedPeriod itself, so we just assert resolution succeeded.
            expectedToken.ShouldNotBeNull();
        }
    }

    private static Dashboard NewDashboard(
        DashboardPushPolicy pushPolicy = DashboardPushPolicy.WhenWidgetsRequest) => Dashboard.Create(
        id: Guid.NewGuid(),
        name: "SampleDashboard",
        category: DashboardCategory.Finance,
        sourceDefinitionName: SourceDefinitionName,
        sourceDefinitionVersion: "1.0.0",
        pushPolicy: pushPolicy);

    [Theory]
    [InlineData(DashboardPushPolicy.PullOnly, RefreshHint.Realtime, WidgetTransport.Pull)]
    [InlineData(DashboardPushPolicy.WhenWidgetsRequest, RefreshHint.Dynamic, WidgetTransport.Pull)]
    [InlineData(DashboardPushPolicy.WhenWidgetsRequest, RefreshHint.Realtime, WidgetTransport.Push)]
    [InlineData(DashboardPushPolicy.Force, RefreshHint.Static, WidgetTransport.Pull)]
    [InlineData(DashboardPushPolicy.Force, RefreshHint.Dynamic, WidgetTransport.Push)]
    public void ToResponse_ComputesEffectiveTransportPerWidget(
        DashboardPushPolicy policy,
        RefreshHint hint,
        WidgetTransport expected)
    {
        Dashboard dashboard = NewDashboard(policy);
        WidgetInstance widget = dashboard.AddWidget(
            widgetId: Guid.NewGuid(),
            widgetType: "Kpi",
            position: 0,
            width: 4,
            height: 2,
            titleLocalizationKey: $"Widget:{SourceDefinitionName}.live",
            configJson: "{}");

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id,
            RenderedAt: RenderedAt,
            Period: null,
            Widgets:
            [
                new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.ForSnapshot(
                    widgetType: "Kpi",
                    snapshot: JsonSerializer.SerializeToElement(new { value = 1 }),
                    sequence: 1,
                    emittedAt: RenderedAt,
                    refreshHint: hint)),
            ]);

        ResolvedRenderTarget target = new(dashboard.Widgets, ActiveViewName: null);
        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard, target, EmptyRegistry(), periodToken: null);

        response.Widgets[0].Transport.ShouldBe(expected);
    }

    [Fact]
    public void ToResponse_DriftStatus_NotApplicable_WhenNoSourceDefinition()
    {
        // Custom-built dashboard — no SourceDefinitionName.
        var dashboard = Dashboard.Create(
            id: Guid.NewGuid(),
            name: "Adhoc",
            category: DashboardCategory.General);

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id, RenderedAt: RenderedAt, Period: null, Widgets: []);

        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard,
            new ResolvedRenderTarget(dashboard.Widgets, ActiveViewName: null),
            EmptyRegistry(),
            periodToken: null);

        response.DriftStatus.ShouldBe(DashboardDriftStatus.NotApplicable);
        response.SourceDefinitionVersion.ShouldBeNull();
        response.RegisteredVersion.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_DriftStatus_SourceUnregistered_WhenDescriptorMissing()
    {
        // Imported dashboard whose source module is no longer loaded.
        Dashboard dashboard = NewDashboard();

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id, RenderedAt: RenderedAt, Period: null, Widgets: []);

        IDashboardDefinitionRegistry registry = Substitute.For<IDashboardDefinitionRegistry>();
        registry.Find(SourceDefinitionName).Returns((IDashboardDefinitionDescriptor?)null);

        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard,
            new ResolvedRenderTarget(dashboard.Widgets, ActiveViewName: null),
            registry,
            periodToken: null);

        response.DriftStatus.ShouldBe(DashboardDriftStatus.SourceUnregistered);
        response.SourceDefinitionVersion.ShouldBe("1.0.0");
        response.RegisteredVersion.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_DriftStatus_Aligned_WhenVersionsMatch()
    {
        Dashboard dashboard = NewDashboard();

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id, RenderedAt: RenderedAt, Period: null, Widgets: []);

        IDashboardDefinitionDescriptor descriptor = Substitute.For<IDashboardDefinitionDescriptor>();
        descriptor.Name.Returns(SourceDefinitionName);
        descriptor.Version.Returns("1.0.0");
        descriptor.Widgets.Returns(Array.Empty<WidgetDefinition>());

        IDashboardDefinitionRegistry registry = Substitute.For<IDashboardDefinitionRegistry>();
        registry.Find(SourceDefinitionName).Returns(descriptor);

        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard,
            new ResolvedRenderTarget(dashboard.Widgets, ActiveViewName: null),
            registry,
            periodToken: null);

        response.DriftStatus.ShouldBe(DashboardDriftStatus.Aligned);
        response.SourceDefinitionVersion.ShouldBe("1.0.0");
        response.RegisteredVersion.ShouldBe("1.0.0");
    }

    [Fact]
    public void ToResponse_DriftStatus_Behind_WhenRegisteredVersionIsNewer()
    {
        Dashboard dashboard = NewDashboard();                        // imported at v1.0.0

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id, RenderedAt: RenderedAt, Period: null, Widgets: []);

        IDashboardDefinitionDescriptor descriptor = Substitute.For<IDashboardDefinitionDescriptor>();
        descriptor.Name.Returns(SourceDefinitionName);
        descriptor.Version.Returns("1.1.0");                         // module shipped a newer version
        descriptor.Widgets.Returns(Array.Empty<WidgetDefinition>());

        IDashboardDefinitionRegistry registry = Substitute.For<IDashboardDefinitionRegistry>();
        registry.Find(SourceDefinitionName).Returns(descriptor);

        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard,
            new ResolvedRenderTarget(dashboard.Widgets, ActiveViewName: null),
            registry,
            periodToken: null);

        response.DriftStatus.ShouldBe(DashboardDriftStatus.Behind);
        response.SourceDefinitionVersion.ShouldBe("1.0.0");
        response.RegisteredVersion.ShouldBe("1.1.0");
    }

    [Fact]
    public void ToResponse_DriftStatus_Ahead_WhenRegisteredVersionIsOlder()
    {
        Dashboard dashboard = NewDashboard();                        // imported at v1.0.0

        DashboardRenderResult result = new(
            DashboardId: dashboard.Id, RenderedAt: RenderedAt, Period: null, Widgets: []);

        IDashboardDefinitionDescriptor descriptor = Substitute.For<IDashboardDefinitionDescriptor>();
        descriptor.Name.Returns(SourceDefinitionName);
        descriptor.Version.Returns("0.9.0");                         // host loaded an older module
        descriptor.Widgets.Returns(Array.Empty<WidgetDefinition>());

        IDashboardDefinitionRegistry registry = Substitute.For<IDashboardDefinitionRegistry>();
        registry.Find(SourceDefinitionName).Returns(descriptor);

        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(
            result, dashboard,
            new ResolvedRenderTarget(dashboard.Widgets, ActiveViewName: null),
            registry,
            periodToken: null);

        response.DriftStatus.ShouldBe(DashboardDriftStatus.Ahead);
        response.SourceDefinitionVersion.ShouldBe("1.0.0");
        response.RegisteredVersion.ShouldBe("0.9.0");
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", DashboardDriftStatus.Aligned)]
    [InlineData("1.0.0", "1.0.1", DashboardDriftStatus.Behind)]                  // patch bump
    [InlineData("1.0.0", "1.1.0", DashboardDriftStatus.Behind)]                  // minor bump
    [InlineData("1.0.0", "2.0.0", DashboardDriftStatus.Behind)]                  // major bump
    [InlineData("1.10.0", "1.9.0", DashboardDriftStatus.Ahead)]                  // numeric (not lexicographic) compare
    [InlineData("2.0.0", "1.99.99", DashboardDriftStatus.Ahead)]                 // major dominates
    [InlineData("1.0.0-alpha", "1.0.0-alpha", DashboardDriftStatus.Aligned)]     // identical pre-release suffix
    [InlineData("1.0.0-alpha", "1.0.0-beta", DashboardDriftStatus.Unknown)]      // mismatched pre-release suffix
    [InlineData("1.0.0", "1.0.0-rc.1", DashboardDriftStatus.Unknown)]            // suffix vs no suffix on equal tuple
    [InlineData("1.0.0+build.7", "1.0.0+build.8", DashboardDriftStatus.Unknown)] // build metadata differs
    [InlineData("not-a-version", "1.0.0", DashboardDriftStatus.Unknown)]         // unparseable persisted
    [InlineData("1.0.0", "v1.0.0", DashboardDriftStatus.Unknown)]                // 'v' prefix not part of semver shape
    [InlineData("", "1.0.0", DashboardDriftStatus.Unknown)]                      // empty persisted
    public void CompareSemver_PinsTheMatrix(string persisted, string registered, DashboardDriftStatus expected)
    {
        DashboardRenderProjection.CompareSemver(persisted, registered).ShouldBe(expected);
    }

    private static IDashboardDefinitionRegistry EmptyRegistry()
    {
        IDashboardDefinitionRegistry registry = Substitute.For<IDashboardDefinitionRegistry>();
        registry.Find(Arg.Any<string>()).Returns((IDashboardDefinitionDescriptor?)null);
        return registry;
    }

    private static MarkdownWidgetDefinition FakeWidgetDefinition(string slug, IReadOnlyList<WidgetAction>? actions) =>
        new(
            Slug: slug,
            ContentLocalizationKey: $"Widget:{SourceDefinitionName}.{slug}.Body",
            Position: 0,
            Actions: actions);
}
