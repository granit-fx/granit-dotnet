using FluentValidation;
using Granit.Metering.Endpoints.Dtos;
using Granit.Timing;
using Granit.Validation.Extensions;

namespace Granit.Metering.Endpoints.Validators;

internal sealed class RecordUsageRequestValidator : AbstractValidator<RecordUsageRequest>
{
    internal const int MaxBatchSize = 1000;

    /// <summary>Max event age accepted by the standard ingestion endpoint.</summary>
    internal static readonly TimeSpan StandardMaxAge = TimeSpan.FromDays(7);

    public RecordUsageRequestValidator(IClock clock)
    {
        RuleFor(x => x.Events)
            .NotEmpty()
            .Must(events => events.Count <= MaxBatchSize)
            .WithErrorCodeAndMessage("Granit:Validation:MaxBatchSize");

        RuleForEach(x => x.Events)
            .SetValidator(new MeterEventRequestValidator(clock, StandardMaxAge));
    }
}

internal sealed class MeterEventRequestValidator : AbstractValidator<MeterEventRequest>
{
    public MeterEventRequestValidator(IClock clock, TimeSpan maxAge)
    {
        RuleFor(x => x.MeterDefinitionId)
            .NotEmpty();

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Quantity)
            .GreaterThan(0);

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .LessThanOrEqualTo(clock.Now.AddMinutes(5))
            .GreaterThan(clock.Now - maxAge);

        RuleFor(x => x.Metadata)
            .MaximumLength(4000)
            .When(x => x.Metadata is not null);
    }
}
