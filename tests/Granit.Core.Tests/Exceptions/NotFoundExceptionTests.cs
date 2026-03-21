using Granit.Core.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Exceptions;

public sealed class NotFoundExceptionTests
{
    // -------------------------------------------------------------------------
    // Constructor — default message
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_NoMessage_UsesDefaultMessage()
    {
        NotFoundException exception = new();

        exception.Message.ShouldBe("The requested resource was not found.");
    }

    [Fact]
    public void Constructor_NullMessage_UsesDefaultMessage()
    {
        NotFoundException exception = new(null);

        exception.Message.ShouldBe("The requested resource was not found.");
    }

    // -------------------------------------------------------------------------
    // Constructor — custom message
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithMessage_UsesProvidedMessage()
    {
        NotFoundException exception = new("Custom not found message.");

        exception.Message.ShouldBe("Custom not found message.");
    }

    // -------------------------------------------------------------------------
    // Constructor — inner exception
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithInnerException_PropagatesIt()
    {
        InvalidOperationException inner = new("inner");

        NotFoundException exception = new("message", inner);

        exception.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public void Constructor_WithNullInnerException_InnerIsNull()
    {
        NotFoundException exception = new("message", null);

        exception.InnerException.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Interface implementation
    // -------------------------------------------------------------------------

    [Fact]
    public void Implements_IUserFriendlyException()
    {
        NotFoundException exception = new();

        exception.ShouldBeAssignableTo<IUserFriendlyException>();
    }

    [Fact]
    public void DoesNotImplement_IHasErrorCode()
    {
        NotFoundException exception = new();

        exception.ShouldNotBeAssignableTo<IHasErrorCode>();
    }

    [Fact]
    public void IsException()
    {
        NotFoundException exception = new();

        exception.ShouldBeAssignableTo<Exception>();
    }
}
