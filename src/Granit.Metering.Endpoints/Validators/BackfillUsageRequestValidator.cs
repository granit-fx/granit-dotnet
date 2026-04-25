using FluentValidation;
using Granit.Metering.Endpoints.Dtos;
using Granit.Timing;
using Granit.Validation.Extensions;

namespace Granit.Metering.Endpoints.Validators;

/// <summary>
/// Backfill validator — same shape as <see cref="RecordUsageRequestValidator"/> but
/// allows event timestamps up to <see cref="MaxAge"/> in the past (365 days by default).
/// Future timestamps are still rejected — backfill is for past data only.
/// </summary>
internal sealed class BackfillUsageRequestValidator : AbstractValidator<BackfillUsageRequest>
{
    /// <summary>
    /// Retention ceiling for backfill — events older than this are rejected to keep
    /// historical aggregates bounded. Aligned with typical SaaS billing-records
    /// retention (12 months hot + offline archive after).
    /// </summary>
    internal static readonly TimeSpan MaxAge = TimeSpan.FromDays(365);

    public BackfillUsageRequestValidator(IClock clock)
    {
        RuleFor(x => x.Events)
            .NotEmpty()
            .Must(events => events.Count <= RecordUsageRequestValidator.MaxBatchSize)
            .WithErrorCodeAndMessage("Granit:Validation:MaxBatchSize");

        RuleForEach(x => x.Events)
            .ChildRules(events => MeterEventRules.Apply(events, clock, MaxAge));
    }
}
