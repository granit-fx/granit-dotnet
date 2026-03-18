using Granit.Core.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Exceptions;

public sealed class ConflictExceptionTests
{
    [Fact]
    public void Constructor_SetsErrorCode()
    {
        ConflictException exception = new("Patient:DuplicateNationalId");

        exception.ErrorCode.ShouldBe("Patient:DuplicateNationalId");
    }

    [Fact]
    public void Constructor_NullMessage_UsesErrorCodeAsMessage()
    {
        ConflictException exception = new("Order:DuplicateReference");

        exception.Message.ShouldBe("Order:DuplicateReference");
    }

    [Fact]
    public void Constructor_WithMessage_UsesProvidedMessage()
    {
        ConflictException exception = new("Patient:DuplicateNationalId", "A patient with this national ID already exists.");

        exception.Message.ShouldBe("A patient with this national ID already exists.");
        exception.ErrorCode.ShouldBe("Patient:DuplicateNationalId");
    }

    [Fact]
    public void Constructor_WithInnerException_SetsInnerException()
    {
        InvalidOperationException inner = new("original cause");
        ConflictException exception = new("Resource:Conflict", "Conflict occurred", inner);

        exception.InnerException.ShouldBeSameAs(inner);
        exception.ErrorCode.ShouldBe("Resource:Conflict");
        exception.Message.ShouldBe("Conflict occurred");
    }

    [Fact]
    public void Implements_IHasErrorCode()
    {
        ConflictException exception = new("Code:X");

        exception.ShouldBeAssignableTo<IHasErrorCode>();
    }

    [Fact]
    public void Implements_IUserFriendlyException()
    {
        ConflictException exception = new("Code:X");

        exception.ShouldBeAssignableTo<IUserFriendlyException>();
    }

    [Fact]
    public void IsException()
    {
        ConflictException exception = new("Code:X");

        exception.ShouldBeAssignableTo<Exception>();
    }
}
