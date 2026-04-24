using FluentValidation.Results;
using Granit.Metering.Endpoints.Dtos;
using Granit.Metering.Endpoints.Validators;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Metering.Endpoints.Tests.Validators;

public sealed class BackfillUsageRequestValidatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-24T12:00:00Z");

    private static IClock FixedClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        return clock;
    }

    private static MeterEventRequest BuildEvent(DateTimeOffset timestamp) =>
        new(MeterDefinitionId: Guid.NewGuid(),
            IdempotencyKey: $"key-{Guid.NewGuid():N}",
            Quantity: 1m,
            Timestamp: timestamp);

    [Fact]
    public void Accepts_event_30_days_old()
    {
        var validator = new BackfillUsageRequestValidator(FixedClock());
        var request = new BackfillUsageRequest([BuildEvent(Now.AddDays(-30))]);

        ValidationResult result = validator.Validate(request);

        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Accepts_event_at_max_age_minus_one_minute()
    {
        var validator = new BackfillUsageRequestValidator(FixedClock());
        var request = new BackfillUsageRequest([
            BuildEvent(Now - BackfillUsageRequestValidator.MaxAge + TimeSpan.FromMinutes(1)),
        ]);

        ValidationResult result = validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_event_older_than_retention_policy()
    {
        var validator = new BackfillUsageRequestValidator(FixedClock());
        var request = new BackfillUsageRequest([
            BuildEvent(Now - BackfillUsageRequestValidator.MaxAge - TimeSpan.FromHours(1)),
        ]);

        ValidationResult result = validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_event_in_the_future()
    {
        var validator = new BackfillUsageRequestValidator(FixedClock());
        var request = new BackfillUsageRequest([BuildEvent(Now.AddHours(1))]);

        ValidationResult result = validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_empty_batch()
    {
        var validator = new BackfillUsageRequestValidator(FixedClock());
        var request = new BackfillUsageRequest([]);

        ValidationResult result = validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_batch_over_max_size()
    {
        var validator = new BackfillUsageRequestValidator(FixedClock());
        var events = Enumerable
            .Range(0, RecordUsageRequestValidator.MaxBatchSize + 1)
            .Select(_ => BuildEvent(Now.AddDays(-1)))
            .ToList();
        var request = new BackfillUsageRequest(events);

        ValidationResult result = validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }
}
