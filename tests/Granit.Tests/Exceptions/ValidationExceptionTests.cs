// =============================================================================
// Tests - ValidationException
// =============================================================================
// Verifies:
//   - Constructor stores ValidationErrors correctly
//   - Message is always the standard generic validation message
//   - Implements IHasValidationErrors and IUserFriendlyException
// =============================================================================

using Granit.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Tests.Exceptions;

public sealed class ValidationExceptionTests
{
    private static Dictionary<string, string[]> BuildErrors() =>
        new Dictionary<string, string[]>
        {
            ["Email"] = ["The Email field is required."],
            ["Name"] = ["Must be between 3 and 50 characters."]
        };

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_StoresValidationErrors()
    {
        IReadOnlyDictionary<string, string[]> errors = BuildErrors();
        ValidationException exception = new(errors);

        exception.ValidationErrors.ShouldBeSameAs(errors);
    }

    [Fact]
    public void Constructor_EmailErrors_AreAccessible()
    {
        ValidationException exception = new(BuildErrors());

        exception.ValidationErrors["Email"].ShouldHaveSingleItem().ShouldBe("The Email field is required.");
    }

    [Fact]
    public void Constructor_MultipleFieldErrors_AreAllStored()
    {
        ValidationException exception = new(BuildErrors());

        exception.ValidationErrors.Count.ShouldBe(2);
        exception.ValidationErrors.Keys.ShouldContain("Email");
        exception.ValidationErrors.Keys.ShouldContain("Name");
    }

    [Fact]
    public void Constructor_SetsGenericMessage()
    {
        ValidationException exception = new(BuildErrors());

        exception.Message.ShouldBe("One or more validation errors occurred.");
    }

    // -------------------------------------------------------------------------
    // Interface implementation
    // -------------------------------------------------------------------------

    [Fact]
    public void Implements_IHasValidationErrors()
    {
        ValidationException exception = new(BuildErrors());

        exception.ShouldBeAssignableTo<IHasValidationErrors>();
    }

    [Fact]
    public void Implements_IUserFriendlyException()
    {
        ValidationException exception = new(BuildErrors());

        exception.ShouldBeAssignableTo<IUserFriendlyException>();
    }

    [Fact]
    public void IsException()
    {
        ValidationException exception = new(BuildErrors());

        exception.ShouldBeAssignableTo<Exception>();
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_EmptyErrors_IsAllowed()
    {
        IReadOnlyDictionary<string, string[]> empty = new Dictionary<string, string[]>();
        ValidationException exception = new(empty);

        exception.ValidationErrors.ShouldBeEmpty();
    }
}
