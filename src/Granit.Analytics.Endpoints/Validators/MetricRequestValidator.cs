using FluentValidation;
using Granit.Analytics.Endpoints.Dtos;
using Granit.Timing;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Analytics.Endpoints.Validators;

/// <summary>
/// Validates the body of <c>POST {prefix}/metrics/{name}</c>. Period is required and must
/// be either an absolute <c>{from, to}</c> window with <c>from &lt; to</c> or a non-empty
/// named <c>token</c> (resolved server-side by <see cref="Internal.PeriodResolver"/>) —
/// not both. The optional <see cref="MetricRequest.CompareTo"/> follows the same rules
/// plus accepts the special <c>previous_period</c> token.
/// </summary>
internal sealed class MetricRequestValidator : GranitValidator<MetricRequest>
{
    public MetricRequestValidator()
    {
        RuleFor(x => x.Period).NotNull();

        RuleFor(x => x.Period)
            .SetValidator(new PeriodSpecValidator())
            .When(x => x.Period is not null);

        RuleFor(x => x.CompareTo!)
            .SetValidator(new PeriodSpecValidator())
            .When(x => x.CompareTo is not null);
    }

    private sealed class PeriodSpecValidator : GranitValidator<PeriodSpec>
    {
        public PeriodSpecValidator()
        {
            // Either Token is set (named period — possibly "previous_period" for the
            // comparison window) or both From and To are set. Anything else is rejected.
            RuleFor(x => x)
                .Must(HasEitherTokenOrAbsoluteRange)
                .WithErrorCodeAndMessage("Granit:Validation:PeriodSpecBoundsCode");

            // When the absolute form is used, From must be strictly before To.
            RuleFor(x => x)
                .Must(FromBeforeTo)
                .When(x => x.Token is null && x.From.HasValue && x.To.HasValue)
                .WithErrorCodeAndMessage("Granit:Validation:PeriodSpecOrderingCode");
        }

        private static bool HasEitherTokenOrAbsoluteRange(PeriodSpec spec) =>
            !string.IsNullOrWhiteSpace(spec.Token)
                ? !spec.From.HasValue && !spec.To.HasValue
                : spec.From.HasValue && spec.To.HasValue;

        private static bool FromBeforeTo(PeriodSpec spec) =>
            spec.From!.Value < spec.To!.Value;
    }
}
