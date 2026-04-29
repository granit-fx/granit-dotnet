using FluentValidation;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Dashboards.Endpoints.Validators;

/// <summary>
/// Validates the body of <c>POST {prefix}/dashboards/{id}/render</c>. The
/// optional period is supplied as absolute <see cref="DashboardRenderRequest.PeriodFrom"/>
/// / <see cref="DashboardRenderRequest.PeriodTo"/> bounds — both required
/// together, with <c>From</c> strictly before <c>To</c>. When neither is
/// supplied, the renderer runs without a period filter (typical for
/// dashboards composed exclusively of static-content widgets).
/// </summary>
internal sealed class DashboardRenderRequestValidator : GranitValidator<DashboardRenderRequest>
{
    public DashboardRenderRequestValidator()
    {
        RuleFor(x => x)
            .Must(BothPeriodBoundsOrNeither)
            .WithErrorCodeAndMessage("Granit:Validation:PeriodSpecBoundsCode");

        RuleFor(x => x)
            .Must(FromBeforeTo)
            .When(x => x.PeriodFrom.HasValue && x.PeriodTo.HasValue)
            .WithErrorCodeAndMessage("Granit:Validation:PeriodSpecOrderingCode");
    }

    private static bool BothPeriodBoundsOrNeither(DashboardRenderRequest request) =>
        request.PeriodFrom.HasValue == request.PeriodTo.HasValue;

    private static bool FromBeforeTo(DashboardRenderRequest request) =>
        request.PeriodFrom!.Value < request.PeriodTo!.Value;
}
