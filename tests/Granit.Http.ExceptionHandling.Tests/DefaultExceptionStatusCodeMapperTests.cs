// =============================================================================
// Tests - DefaultExceptionStatusCodeMapper
// =============================================================================
// Verifies that each exception type is mapped to the expected HTTP status code.
// Also verifies the chain-of-responsibility contract (returns null for unknown
// exceptions is NOT the default behaviour — the default mapper always returns
// a status code; null is for specialized mappers that don't handle an exception).
// =============================================================================

using Granit.Core.Exceptions;
using Granit.Http.ExceptionHandling;
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
    public void EntityNotFoundException_Returns404()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new EntityNotFoundException(typeof(object), 1));

        result.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void NotFoundException_Returns404()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new NotFoundException("Resource not found"));

        result.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void SubclassOfNotFoundException_Returns404()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new CustomNotFoundException());

        result.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ForbiddenException_Returns403()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new ForbiddenException());

        result.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void BusinessException_Returns400()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new BusinessException("Test:Error"));

        result.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void BusinessRuleViolationException_Returns422()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new BusinessRuleViolationException("Appointment:SlotUnavailable"));

        result.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void BusinessRuleViolationException_TreatedAs422_NotInheritedAs400()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();
        BusinessException exception = new BusinessRuleViolationException("Appointment:SlotUnavailable");

        int? result = mapper.TryGetStatusCode(exception);

        result.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void ConflictException_Returns409()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new ConflictException("Test:Conflict"));

        result.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void ValidationException_Returns422()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();
        Dictionary<string, string[]> errors = new() { ["Field"] = ["Required"] };
        int? result = mapper.TryGetStatusCode(new Core.Exceptions.ValidationException(errors));

        result.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    // -------------------------------------------------------------------------
    // Standard .NET exception types
    // -------------------------------------------------------------------------

    [Fact]
    public void UnauthorizedAccessException_Returns403()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new UnauthorizedAccessException());

        result.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void NotImplementedException_Returns501()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new NotImplementedException());

        result.ShouldBe(StatusCodes.Status501NotImplemented);
    }

    [Fact]
    public void OperationCanceledException_Returns499()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new OperationCanceledException());

        result.ShouldBe(499);
    }

    [Fact]
    public void TimeoutException_Returns408()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new TimeoutException());

        result.ShouldBe(StatusCodes.Status408RequestTimeout);
    }

    // -------------------------------------------------------------------------
    // Fallback: unknown exception → 500
    // -------------------------------------------------------------------------

    [Fact]
    public void UnknownException_Returns500()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new InvalidOperationException("Something went wrong"));

        result.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void ArithmeticException_Returns500()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new DivideByZeroException());

        result.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // -------------------------------------------------------------------------
    // Interface-based matching
    // -------------------------------------------------------------------------

    [Fact]
    public void CustomExceptionImplementingIHasErrorCode_Returns400()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new CustomDomainException("Custom:Error"));

        result.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void CustomExceptionImplementingIHasValidationErrors_Returns422()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new CustomValidationException());

        result.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    // -------------------------------------------------------------------------
    // Return value is always non-null (default mapper is the final fallback)
    // -------------------------------------------------------------------------

    [Fact]
    public void AlwaysReturnsNonNull()
    {
        DefaultExceptionStatusCodeMapper mapper = Create();

        int? result = mapper.TryGetStatusCode(new InvalidOperationException("generic"));

        result.ShouldNotBeNull();
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
