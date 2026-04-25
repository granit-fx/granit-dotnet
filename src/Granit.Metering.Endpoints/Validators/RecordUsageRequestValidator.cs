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
            .ChildRules(events => MeterEventRules.Apply(events, clock, StandardMaxAge));
    }
}

/// <summary>
/// Per-event rule definitions shared between the standard
/// <see cref="RecordUsageRequestValidator"/> (7-day window) and
/// <see cref="BackfillUsageRequestValidator"/> (365-day window).
/// </summary>
/// <remarks>
/// Static class — deliberately NOT an <c>AbstractValidator</c> so that
/// FluentValidation's <c>AddValidatorsFromAssembly(includeInternalTypes: true)</c>
/// scanner skips it (no <c>IValidator</c> implementation to discover). Exposing it
/// as a typed validator would have the DI container try to construct it, which
/// fails because <see cref="TimeSpan"/> isn't a registered service.
/// </remarks>
internal static class MeterEventRules
{
    public static void Apply(InlineValidator<MeterEventRequest> events, IClock clock, TimeSpan maxAge)
    {
        events.RuleFor(x => x.MeterDefinitionId)
            .NotEmpty();

        events.RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(256);

        events.RuleFor(x => x.Quantity)
            .GreaterThan(0);

        events.RuleFor(x => x.Timestamp)
            .NotEmpty()
            .LessThanOrEqualTo(clock.Now.AddMinutes(5))
            .GreaterThan(clock.Now - maxAge);

        events.RuleFor(x => x.Metadata)
            .MaximumLength(4000)
            .When(x => x.Metadata is not null);
    }
}
