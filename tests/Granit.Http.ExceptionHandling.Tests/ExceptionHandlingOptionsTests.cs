// =============================================================================
// Tests - ExceptionHandlingOptions
// =============================================================================
// Verifies that the options class has correct defaults.
// =============================================================================

using Granit.Http.ExceptionHandling.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ExceptionHandling.Tests;

public sealed class ExceptionHandlingOptionsTests
{
    [Fact]
    public void ExposeInternalErrorDetails_DefaultsToFalse()
    {
        ExceptionHandlingOptions options = new();

        options.ExposeInternalErrorDetails.ShouldBeFalse(
            "internal error details must default to false for ISO 27001 compliance");
    }
}
