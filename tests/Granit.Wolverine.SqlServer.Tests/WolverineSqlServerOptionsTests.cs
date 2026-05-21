// =============================================================================
// Tests - WolverineSqlServerOptions + WolverineSqlServerOptionsValidator
// =============================================================================
// Verifies default values, section name constant, and all validation branches.
// =============================================================================

using Granit.Wolverine.SqlServer.Internal;
using Granit.Wolverine.SqlServer.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Wolverine.Persistence;
using Xunit;

namespace Granit.Wolverine.SqlServer.Tests;

public sealed class WolverineSqlServerOptionsTests
{
    // -----------------------------------------------------------------------
    // WolverineSqlServerOptions — defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void SectionName_IsWolverineSqlServer() =>
        WolverineSqlServerOptions.SectionName.ShouldBe("Wolverine:SqlServer");

    [Fact]
    public void DefaultTransportConnectionString_IsEmpty() =>
        new WolverineSqlServerOptions().TransportConnectionString.ShouldBeEmpty();

    [Fact]
    public void DefaultTransactionMode_IsEager() =>
        new WolverineSqlServerOptions().TransactionMode.ShouldBe(TransactionMiddlewareMode.Eager);

    // -----------------------------------------------------------------------
    // WolverineSqlServerOptionsValidator — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_ValidConnectionString_Succeeds()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new()
        {
            TransportConnectionString = "Server=localhost;Database=test;User Id=sa;Password=pass;TrustServerCertificate=True",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_LightweightMode_Succeeds()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new()
        {
            TransportConnectionString = "Server=localhost;Database=test",
            TransactionMode = TransactionMiddlewareMode.Lightweight,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // WolverineSqlServerOptionsValidator — TransportConnectionString failures
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_NullConnectionString_Fails()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new() { TransportConnectionString = null! };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("TransportConnectionString"));
    }

    [Fact]
    public void Validate_EmptyConnectionString_Fails()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new() { TransportConnectionString = string.Empty };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("TransportConnectionString"));
    }

    [Fact]
    public void Validate_WhitespaceConnectionString_Fails()
    {
        WolverineSqlServerOptionsValidator validator = new();
        WolverineSqlServerOptions options = new() { TransportConnectionString = "   " };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("ISO 27001"));
    }
}
