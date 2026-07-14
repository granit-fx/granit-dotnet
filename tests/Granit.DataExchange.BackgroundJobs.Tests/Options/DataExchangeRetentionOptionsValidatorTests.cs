using Granit.DataExchange.BackgroundJobs.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.BackgroundJobs.Tests.Options;

public sealed class DataExchangeRetentionOptionsValidatorTests
{
    private readonly DataExchangeRetentionOptionsValidator _sut = new();

    [Fact]
    public void Validate_with_defaults_should_succeed() =>
        _sut.Validate(null, new DataExchangeRetentionOptions()).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_with_non_positive_ImportFileRetention_should_fail(int days)
    {
        var options = new DataExchangeRetentionOptions { ImportFileRetention = TimeSpan.FromDays(days) };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(DataExchangeRetentionOptions.ImportFileRetention));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_with_non_positive_ExportFileRetention_should_fail(int days)
    {
        var options = new DataExchangeRetentionOptions { ExportFileRetention = TimeSpan.FromDays(days) };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(DataExchangeRetentionOptions.ExportFileRetention));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_with_non_positive_JobRecordRetention_should_fail(int days)
    {
        var options = new DataExchangeRetentionOptions { JobRecordRetention = TimeSpan.FromDays(days) };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(DataExchangeRetentionOptions.JobRecordRetention));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_with_non_positive_StuckJobTimeout_should_fail(int hours)
    {
        var options = new DataExchangeRetentionOptions { StuckJobTimeout = TimeSpan.FromHours(hours) };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(DataExchangeRetentionOptions.StuckJobTimeout));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Validate_with_out_of_range_SweepBatchSize_should_fail(int batchSize)
    {
        var options = new DataExchangeRetentionOptions { SweepBatchSize = batchSize };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(DataExchangeRetentionOptions.SweepBatchSize));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10_000)]
    public void Validate_with_boundary_SweepBatchSize_should_succeed(int batchSize)
    {
        var options = new DataExchangeRetentionOptions { SweepBatchSize = batchSize };

        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }
}
