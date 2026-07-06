// =============================================================================
// Tests - WolverineMessagingOptions (additional coverage)
// =============================================================================
// Covers the RetryDelays null validation branch.
// =============================================================================

using Granit.Wolverine.Internal;
using Granit.Wolverine.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class WolverineMessagingOptionsAdditionalTests
{
    [Fact]
    public void Validate_NullRetryDelays_Fails()
    {
        WolverineMessagingOptionsValidator validator = new();
        WolverineMessagingOptions options = new() { RetryDelays = null! };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("RetryDelays"));
    }

    [Fact]
    public void Validate_MultipleFailures_ReportsAll()
    {
        WolverineMessagingOptionsValidator validator = new();
        WolverineMessagingOptions options = new()
        {
            MaxRetryAttempts = 0,
            RetryDelays = [],
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("MaxRetryAttempts"));
        result.Failures.ShouldContain(x => x.Contains("RetryDelays"));
    }

    [Fact]
    public void RetryDelays_CanBeSetToCustomValues()
    {
        WolverineMessagingOptions options = new()
        {
            RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)],
        };

        options.RetryDelays.Length.ShouldBe(2);
        options.RetryDelays[0].ShouldBe(TimeSpan.FromSeconds(1));
        options.RetryDelays[1].ShouldBe(TimeSpan.FromSeconds(2));
    }

}
