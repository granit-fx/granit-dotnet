// =============================================================================
// Tests - DefaultExceptionStatusCodeMapper
// =============================================================================
// Verifies that each exception type is mapped to the expected HTTP status code.
// The default mapper returns null for unrecognized exceptions, delegating to
// downstream mappers or the handler's own fallback (500).
// =============================================================================

using Granit.Exceptions;
using Granit.Http.ExceptionHandling.Internal;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Http.ExceptionHandling.Tests;

public sealed class DefaultExceptionStatusCodeMapperTests
{
    private static DefaultExceptionStatusCodeMapper Create() => new();

    // -------------------------------------------------------------------------
    // Granit exception types
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetStatusCode_EntityNotFoundException_Returns404()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new EntityNotFoundException(typeof(object), 1));

        result.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void TryGetStatusCode_NotFoundException_Returns404()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new NotFoundException("Resource not found"));

        result.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void TryGetStatusCode_SubclassOfNotFoundException_Returns404()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new CustomNotFoundException());

        result.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void TryGetStatusCode_ForbiddenException_Returns403()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new ForbiddenException());

        result.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void TryGetStatusCode_ForbiddenExceptionWithMessage_Returns403()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new ForbiddenException("Access denied to resource"));

        result.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void TryGetStatusCode_BusinessException_Returns400()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new BusinessException("Test:Error"));

        result.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void TryGetStatusCode_BusinessRuleViolationException_Returns422()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new BusinessRuleViolationException("Appointment:SlotUnavailable"));

        result.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void TryGetStatusCode_BusinessRuleViolationException_TreatedAs422_NotInheritedAs400()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();
        BusinessException exception = new BusinessRuleViolationException("Appointment:SlotUnavailable");

        int? result = mapper.TryGetStatusCode(exception);

        result.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void TryGetStatusCode_ConflictException_Returns409()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new ConflictException("Test:Conflict"));

        result.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void TryGetStatusCode_ValidationException_Returns422()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();
        Dictionary<string, string[]> errors = new() { ["Field"] = ["Required"] };
        int? result = mapper.TryGetStatusCode(new Exceptions.ValidationException(errors));

        result.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    // -------------------------------------------------------------------------
    // Standard .NET exception types
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetStatusCode_UnauthorizedAccessException_Returns403()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new UnauthorizedAccessException());

        result.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void TryGetStatusCode_NotImplementedException_Returns501()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new NotImplementedException());

        result.ShouldBe(StatusCodes.Status501NotImplemented);
    }

    [Fact]
    public void TryGetStatusCode_OperationCanceledException_Returns499()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new OperationCanceledException());

        result.ShouldBe(499);
    }

    [Fact]
    public void TryGetStatusCode_TaskCanceledException_Returns499()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new TaskCanceledException());

        result.ShouldBe(499);
    }

    [Fact]
    public void TryGetStatusCode_TimeoutException_Returns408()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new TimeoutException());

        result.ShouldBe(StatusCodes.Status408RequestTimeout);
    }

    // -------------------------------------------------------------------------
    // BadHttpRequestException: honours the status ASP.NET already resolved
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetStatusCode_BadHttpRequestException_Returns400()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(
            new BadHttpRequestException("Malformed request body", StatusCodes.Status400BadRequest));

        result.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void TryGetStatusCode_BadHttpRequestExceptionWithNon400StatusCode_ReturnsThatStatusCode()
    {
        // A too-large body surfaces as BadHttpRequestException with StatusCode 413.
        // The mapper must honour it rather than hardcoding 400.
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(
            new BadHttpRequestException("Request body too large", StatusCodes.Status413PayloadTooLarge));

        result.ShouldBe(StatusCodes.Status413PayloadTooLarge);
    }

    // -------------------------------------------------------------------------
    // Fallback: unknown exception -> null (delegates to next mapper or handler)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetStatusCode_UnknownException_ReturnsNull()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new InvalidOperationException("Something went wrong"));

        result.ShouldBeNull("unknown exceptions must delegate to downstream mappers");
    }

    [Fact]
    public void TryGetStatusCode_ArithmeticException_ReturnsNull()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new DivideByZeroException());

        result.ShouldBeNull();
    }

    [Fact]
    public void TryGetStatusCode_ArgumentException_ReturnsNull()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new ArgumentException("bad arg"));

        result.ShouldBeNull();
    }

    [Fact]
    public void TryGetStatusCode_StackOverflowLikeException_ReturnsNull()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new InsufficientMemoryException());

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Interface-based matching
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetStatusCode_CustomExceptionImplementingIHasErrorCode_Returns400()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new CustomDomainException("Custom:Error"));

        result.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void TryGetStatusCode_CustomExceptionImplementingIHasValidationErrors_Returns422()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new CustomValidationException());

        result.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    // -------------------------------------------------------------------------
    // Known exceptions always return non-null
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetStatusCode_KnownExceptionType_ReturnsNonNull()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new BusinessException("Test:Error"));

        result.ShouldNotBeNull();
    }

    // -------------------------------------------------------------------------
    // Pattern matching order: more specific types match first
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetStatusCode_EntityNotFoundExceptionBeforeNotFoundException_Returns404()
    {
        // EntityNotFoundException is a subclass of NotFoundException.
        // The pattern match checks EntityNotFoundException first, ensuring the most
        // specific type is matched.
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new EntityNotFoundException(typeof(string), "abc"));

        result.ShouldBe(StatusCodes.Status404NotFound);
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed class CustomDomainException(string errorCode) : Exception("msg"), IHasErrorCode
    {
        public string ErrorCode { get; } = errorCode;
    }

    private sealed class CustomValidationException() : Exception("validation"), IHasValidationErrors
    {
        public IReadOnlyDictionary<string, string[]> ValidationErrors =>
            new Dictionary<string, string[]> { ["Field"] = ["Error"] };
    }

    private sealed class CustomNotFoundException() : NotFoundException("Custom resource not found");
}
