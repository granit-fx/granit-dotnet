using Granit.Core.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Exceptions;

public sealed class ForbiddenExceptionTests
{
    // -------------------------------------------------------------------------
    // Constructor — default message
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_NoMessage_UsesDefaultMessage()
    {
        ForbiddenException exception = new();

        exception.Message.ShouldBe("Access to this resource is forbidden.");
    }

    [Fact]
    public void Constructor_NullMessage_UsesDefaultMessage()
    {
        ForbiddenException exception = new(null);

        exception.Message.ShouldBe("Access to this resource is forbidden.");
    }

    // -------------------------------------------------------------------------
    // Constructor — custom message
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithMessage_UsesProvidedMessage()
    {
        ForbiddenException exception = new("Access to this appointment is not allowed.");

        exception.Message.ShouldBe("Access to this appointment is not allowed.");
    }

    // -------------------------------------------------------------------------
    // Constructor — inner exception
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithInnerException_PropagatesIt()
    {
        InvalidOperationException inner = new("inner");

        ForbiddenException exception = new("message", inner);

        exception.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public void Constructor_WithNullInnerException_InnerIsNull()
    {
        ForbiddenException exception = new("message", null);

        exception.InnerException.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Interface implementation
    // -------------------------------------------------------------------------

    [Fact]
    public void Implements_IUserFriendlyException()
    {
        ForbiddenException exception = new();

        exception.ShouldBeAssignableTo<IUserFriendlyException>();
    }

    [Fact]
    public void DoesNotImplement_IHasErrorCode()
    {
        ForbiddenException exception = new();

        exception.ShouldNotBeAssignableTo<IHasErrorCode>();
    }

    [Fact]
    public void IsException()
    {
        ForbiddenException exception = new();

        exception.ShouldBeAssignableTo<Exception>();
    }
}
