using FluentValidation;
using Granit.Analytics.Endpoints.Dtos.Widgets;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Analytics.Endpoints.Validators;

/// <summary>
/// Validates the per-render context shared by every <c>POST /widgets/{kind}/render</c>
/// endpoint. Both period bounds must be supplied together (or both omitted);
/// supplying only one yields a 400. Mirrors the bundle path's
/// <c>DashboardRenderRequestValidator</c>.
/// </summary>
internal sealed class WidgetRenderContextRequestValidator : GranitValidator<WidgetRenderContextRequest>
{
    public WidgetRenderContextRequestValidator()
    {
        RuleFor(x => x)
            .Must(HasBothBoundsOrNone)
            .WithErrorCodeAndMessage("Granit:Validation:PeriodSpecBoundsCode");

        RuleFor(x => x)
            .Must(FromBeforeTo)
            .When(x => x.PeriodFrom is not null && x.PeriodTo is not null)
            .WithErrorCodeAndMessage("Granit:Validation:PeriodSpecOrderingCode");
    }

    private static bool HasBothBoundsOrNone(WidgetRenderContextRequest c) =>
        c.PeriodFrom is null == c.PeriodTo is null;

    private static bool FromBeforeTo(WidgetRenderContextRequest c) =>
        c.PeriodFrom!.Value < c.PeriodTo!.Value;
}

/// <summary>Validates <see cref="KpiWidgetRenderRequest"/> — definition is required, context is optional.</summary>
internal sealed class KpiWidgetRenderRequestValidator : GranitValidator<KpiWidgetRenderRequest>
{
    public KpiWidgetRenderRequestValidator()
    {
        RuleFor(x => x.Definition).NotNull();
        RuleFor(x => x.Context!)
            .SetValidator(new WidgetRenderContextRequestValidator())
            .When(x => x.Context is not null);
    }
}

/// <summary>Validates <see cref="ChartWidgetRenderRequest"/>.</summary>
internal sealed class ChartWidgetRenderRequestValidator : GranitValidator<ChartWidgetRenderRequest>
{
    public ChartWidgetRenderRequestValidator()
    {
        RuleFor(x => x.Definition).NotNull();
        RuleFor(x => x.Context!)
            .SetValidator(new WidgetRenderContextRequestValidator())
            .When(x => x.Context is not null);
    }
}

/// <summary>Validates <see cref="TableWidgetRenderRequest"/>.</summary>
internal sealed class TableWidgetRenderRequestValidator : GranitValidator<TableWidgetRenderRequest>
{
    public TableWidgetRenderRequestValidator()
    {
        RuleFor(x => x.Definition).NotNull();
        RuleFor(x => x.Context!)
            .SetValidator(new WidgetRenderContextRequestValidator())
            .When(x => x.Context is not null);
    }
}

/// <summary>Validates <see cref="PivotWidgetRenderRequest"/>.</summary>
internal sealed class PivotWidgetRenderRequestValidator : GranitValidator<PivotWidgetRenderRequest>
{
    public PivotWidgetRenderRequestValidator()
    {
        RuleFor(x => x.Definition).NotNull();
        RuleFor(x => x.Context!)
            .SetValidator(new WidgetRenderContextRequestValidator())
            .When(x => x.Context is not null);
    }
}

/// <summary>Validates <see cref="MapWidgetRenderRequest"/>.</summary>
internal sealed class MapWidgetRenderRequestValidator : GranitValidator<MapWidgetRenderRequest>
{
    public MapWidgetRenderRequestValidator()
    {
        RuleFor(x => x.Definition).NotNull();
        RuleFor(x => x.Context!)
            .SetValidator(new WidgetRenderContextRequestValidator())
            .When(x => x.Context is not null);
    }
}
