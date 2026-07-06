// =============================================================================
// Tests - WolverineSqlServerOptions (additional coverage)
// =============================================================================
// Covers property setters and additional validation edge cases.
// =============================================================================

using Granit.Wolverine.SqlServer.Internal;
using Granit.Wolverine.SqlServer.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.SqlServer.Tests;

public sealed class WolverineSqlServerOptionsAdditionalTests
{

    [Fact]
    public void Validate_ValidConnectionString_Succeeds()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new()
        {
            TransportConnectionString = "Server=localhost;Database=test;User Id=sa;Password=pass",
        };

        ValidateOptionsResult result = validator.Validate("custom-name", options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_FailureMessage_ContainsISO27001Reference()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new() { TransportConnectionString = string.Empty };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("ISO 27001"));
    }
}
