// =============================================================================
// Tests - GranitExceptionHandler
// =============================================================================
// Verifies the full handler pipeline:
//   - OperationCanceledException: returns true, no response written
//   - EntityNotFoundException -> 404, traceId in extensions
//   - BusinessException -> 400, errorCode in extensions
//   - ValidationException -> 422, errors in extensions
//   - ISO 27001 security: 5xx message masked in production (ExposeInternalErrorDetails = false)
//   - ISO 27001 security: 5xx message exposed in development (ExposeInternalErrorDetails = true)
//   - traceId always present in extensions
//   - Localization fallback when resource not found
//   - Detail exposure for 5xx in development mode
//   - Custom mapper chain of responsibility
//   - Activity.Current traceId propagation
// =============================================================================

using System.Diagnostics;
using Granit.Core.Exceptions;
using Granit.Http.ExceptionHandling.Extensions;
using Granit.Http.ExceptionHandling.Internal;
using Granit.Http.ExceptionHandling.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ExceptionHandling.Tests;

public sealed class GranitExceptionHandlerTests
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    private static ServiceProvider BuildServiceProvider(
        Action<ExceptionHandlingOptions>? configureOptions = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddGranitExceptionHandling(configureOptions);
        return services.BuildServiceProvider();
    }

    private static async Task<(int StatusCode, IDictionary<string, object?> Extensions, string? Title, string? Detail)>
        InvokeHandlerAsync(
            ServiceProvider sp,
            Exception exception)
    {
        var handler = (GranitExceptionHandler)sp
            .GetRequiredService<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>();

        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new System.IO.MemoryStream();

        bool handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);
        handled.ShouldBeTrue();

        // Read ProblemDetails written to the body
        httpContext.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        ProblemDetails? problem = await System.Text.Json.JsonSerializer.DeserializeAsync<ProblemDetails>(
            httpContext.Response.Body,
            JsonOptions);

        return (
            problem?.Status ?? 0,
            problem?.Extensions ?? new Dictionary<string, object?>(),
            problem?.Title,
            problem?.Detail);
    }

    // -------------------------------------------------------------------------
    // OperationCanceledException: no response, return true
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_OperationCanceled_ReturnsTrueWithoutWritingResponse()
    {
        using ServiceProvider sp = BuildServiceProvider();
        var handler = (GranitExceptionHandler)sp
            .GetRequiredService<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>();

        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new System.IO.MemoryStream();

        bool handled = await handler.TryHandleAsync(
            httpContext, new OperationCanceledException(), CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.Body.Length.ShouldBe(0, "no response should be written for cancelled requests");
    }

    [Fact]
    public async Task TryHandleAsync_TaskCanceledException_ReturnsTrueWithoutWritingResponse()
    {
        using ServiceProvider sp = BuildServiceProvider();
        var handler = (GranitExceptionHandler)sp
            .GetRequiredService<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>();

        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new System.IO.MemoryStream();

        bool handled = await handler.TryHandleAsync(
            httpContext, new TaskCanceledException(), CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.Body.Length.ShouldBe(0,
            "TaskCanceledException inherits OperationCanceledException and should be handled the same way");
    }

    // -------------------------------------------------------------------------
    // Status code mapping through handler
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_EntityNotFoundException_ReturnsStatus404()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new EntityNotFoundException(typeof(object), 1));

        statusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_ReturnsStatus404()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new NotFoundException("Resource not found"));

        statusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TryHandleAsync_BusinessException_ReturnsStatus400()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new BusinessException("Test:Error", "Business rule violated."));

        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_ReturnsStatus422()
    {
        using ServiceProvider sp = BuildServiceProvider();
        Dictionary<string, string[]> errors = new() { ["Email"] = ["Required"] };

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new Core.Exceptions.ValidationException(errors));

        statusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task TryHandleAsync_ForbiddenException_ReturnsStatus403()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new ForbiddenException("Access denied"));

        statusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedAccessException_ReturnsStatus403()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new UnauthorizedAccessException("Not authorized"));

        statusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TryHandleAsync_ConflictException_ReturnsStatus409()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new ConflictException("Test:Conflict"));

        statusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task TryHandleAsync_NotImplementedException_ReturnsStatus501()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new NotImplementedException("Feature not implemented"));

        statusCode.ShouldBe(StatusCodes.Status501NotImplemented);
    }

    [Fact]
    public async Task TryHandleAsync_TimeoutException_ReturnsStatus408()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new TimeoutException("Request timed out"));

        statusCode.ShouldBe(StatusCodes.Status408RequestTimeout);
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_ReturnsStatus500()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new InvalidOperationException("Something went wrong"));

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // -------------------------------------------------------------------------
    // Custom mapper chain of responsibility
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_CustomMapperReturnsCode_UsesCustomMapperResult()
    {
        ServiceCollection services = new();
        services.AddLogging();
        // Register custom mapper BEFORE calling AddGranitExceptionHandling
        // so it's tried first in the chain
        services.AddSingleton<IExceptionStatusCodeMapper, ArgumentExceptionMapper>();
        services.AddGranitExceptionHandling();
        using ServiceProvider sp = services.BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new ArgumentException("Bad argument"));

        // Custom mapper returns 400 for ArgumentException (instead of default 500)
        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TryHandleAsync_CustomMapperReturnsNull_FallsBackToDefault()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IExceptionStatusCodeMapper, NullReturningMapper>();
        services.AddGranitExceptionHandling();
        using ServiceProvider sp = services.BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new InvalidOperationException("generic error"));

        // NullReturningMapper returns null, so DefaultExceptionStatusCodeMapper handles it -> 500
        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // -------------------------------------------------------------------------
    // Extensions: traceId always present
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_AnyException_TraceIdPresentInExtensions()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (_, IDictionary<string, object?> extensions, _, _) = await InvokeHandlerAsync(
            sp, new BusinessException("Test:Error"));

        extensions.ShouldContainKey("traceId");
        extensions["traceId"].ShouldNotBeNull();
    }

    [Fact]
    public async Task TryHandleAsync_WithActivityCurrent_UsesActivityTraceId()
    {
        using ServiceProvider sp = BuildServiceProvider();
        using ActivitySource source = new("TestSource");
        using ActivityListener listener = new()
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using Activity? activity = source.StartActivity("TestOperation");
        activity.ShouldNotBeNull();

        string expectedTraceId = activity.TraceId.ToString();

        (_, IDictionary<string, object?> extensions, _, _) = await InvokeHandlerAsync(
            sp, new BusinessException("Test:Error"));

        string actualTraceId = extensions["traceId"]!.ToString()!;
        actualTraceId.ShouldContain(expectedTraceId);
    }

    [Fact]
    public async Task TryHandleAsync_WithoutActivityCurrent_UsesHttpContextTraceIdentifier()
    {
        using ServiceProvider sp = BuildServiceProvider();
        var handler = (GranitExceptionHandler)sp
            .GetRequiredService<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>();

        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new System.IO.MemoryStream();
        httpContext.TraceIdentifier = "custom-trace-id-12345";

        // Ensure no Activity.Current
        Activity.Current = null;

        await handler.TryHandleAsync(httpContext, new BusinessException("Test:Error"), TestContext.Current.CancellationToken);

        httpContext.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        ProblemDetails? problem = await System.Text.Json.JsonSerializer.DeserializeAsync<ProblemDetails>(
            httpContext.Response.Body, JsonOptions, TestContext.Current.CancellationToken);

        problem.ShouldNotBeNull();
        problem.Extensions["traceId"]!.ToString().ShouldBe("custom-trace-id-12345");
    }

    // -------------------------------------------------------------------------
    // Extensions: errorCode for IHasErrorCode
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_BusinessException_ErrorCodeInExtensions()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (_, IDictionary<string, object?> extensions, _, _) = await InvokeHandlerAsync(
            sp, new BusinessException("Appointment:SlotUnavailable", "Slot unavailable."));

        extensions.ShouldContainKey("errorCode");
    }

    [Fact]
    public async Task TryHandleAsync_EntityNotFoundException_NoErrorCodeInExtensions()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (_, IDictionary<string, object?> extensions, _, _) = await InvokeHandlerAsync(
            sp, new EntityNotFoundException(typeof(object), 99));

        extensions.ShouldNotContainKey("errorCode");
    }

    // -------------------------------------------------------------------------
    // Extensions: validationErrors for IHasValidationErrors
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_ValidationException_ErrorsInExtensions()
    {
        using ServiceProvider sp = BuildServiceProvider();
        Dictionary<string, string[]> errors = new()
        {
            ["Email"] = ["Email is required."],
            ["Name"] = ["Name must not be empty."]
        };

        (_, IDictionary<string, object?> extensions, _, _) = await InvokeHandlerAsync(
            sp, new Core.Exceptions.ValidationException(errors));

        extensions.ShouldContainKey("errors");
        extensions["errors"].ShouldNotBeNull();
    }

    [Fact]
    public async Task TryHandleAsync_NonValidationException_NoErrorsInExtensions()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (_, IDictionary<string, object?> extensions, _, _) = await InvokeHandlerAsync(
            sp, new BusinessException("Test:Error", "Some error"));

        extensions.ShouldNotContainKey("errors");
    }

    // -------------------------------------------------------------------------
    // ISO 27001 security: 5xx masking in production
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_InternalException_Production_TitleIsMasked()
    {
        // ExposeInternalErrorDetails = false (default = production behaviour)
        using ServiceProvider sp = BuildServiceProvider(opts => opts.ExposeInternalErrorDetails = false);

        (_, _, string? title, _) = await InvokeHandlerAsync(
            sp, new InvalidOperationException("Patient#12345 caused NullRef"));

        title.ShouldBe("An unexpected error occurred.",
            "internal exception messages must never be exposed in production");
        title!.ShouldNotContain("Patient");
    }

    [Fact]
    public async Task TryHandleAsync_InternalException_Development_TitleExposesMessage()
    {
        // ExposeInternalErrorDetails = true (development/staging)
        using ServiceProvider sp = BuildServiceProvider(opts => opts.ExposeInternalErrorDetails = true);

        (_, _, string? title, _) = await InvokeHandlerAsync(
            sp, new InvalidOperationException("Detailed dev error"));

        title.ShouldBe("Detailed dev error");
    }

    [Fact]
    public async Task TryHandleAsync_InternalException_Production_DetailIsNull()
    {
        using ServiceProvider sp = BuildServiceProvider(opts => opts.ExposeInternalErrorDetails = false);

        (_, _, _, string? detail) = await InvokeHandlerAsync(
            sp, new InvalidOperationException("Sensitive SQL query here"));

        detail.ShouldBeNull("detail must be null in production for 5xx errors (ISO 27001)");
    }

    [Fact]
    public async Task TryHandleAsync_InternalException_Development_DetailExposesStackTrace()
    {
        using ServiceProvider sp = BuildServiceProvider(opts => opts.ExposeInternalErrorDetails = true);

        (_, _, _, string? detail) = await InvokeHandlerAsync(
            sp, new InvalidOperationException("Dev mode exception"));

        detail.ShouldNotBeNull("detail should contain exception info in development mode");
        detail.ShouldContain("Dev mode exception");
    }

    // -------------------------------------------------------------------------
    // UserFriendlyException: title comes from exception message, detail is null
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_BusinessException_TitleEqualsMessage()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (_, _, string? title, _) = await InvokeHandlerAsync(
            sp, new BusinessException("Test:Error", "The business rule was violated."));

        title.ShouldBe("The business rule was violated.");
    }

    [Fact]
    public async Task TryHandleAsync_UserFriendlyException_DetailIsNull()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (_, _, _, string? detail) = await InvokeHandlerAsync(
            sp, new BusinessException("Test:Error", "User-friendly message"));

        detail.ShouldBeNull("detail must be null for user-friendly exceptions");
    }

    // -------------------------------------------------------------------------
    // 4xx non-user-friendly: title uses exception message
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_4xxNonUserFriendly_TitleUsesExceptionMessage()
    {
        // UnauthorizedAccessException is not IUserFriendlyException but maps to 403
        using ServiceProvider sp = BuildServiceProvider();

        (_, _, string? title, _) = await InvokeHandlerAsync(
            sp, new UnauthorizedAccessException("Access denied to resource X"));

        title.ShouldBe("Access denied to resource X");
    }

    [Fact]
    public async Task TryHandleAsync_4xxNonUserFriendly_DetailIsNull()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (_, _, _, string? detail) = await InvokeHandlerAsync(
            sp, new UnauthorizedAccessException("Access denied"));

        detail.ShouldBeNull("detail should be null for 4xx non-user-friendly exceptions");
    }

    // -------------------------------------------------------------------------
    // Localization: prefix-based resource resolution from IHasErrorCode
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_IHasErrorCode_LocalizerFound_TitleIsLocalizedMessage()
    {
        IStringLocalizerFactory mockLocalizerFactory = Substitute.For<IStringLocalizerFactory>();
        IStringLocalizer mockLocalizer = Substitute.For<IStringLocalizer>();
        mockLocalizer["Domain:ErrorCode"].Returns(
            new LocalizedString("Domain:ErrorCode", "Message traduit.", resourceNotFound: false));
        mockLocalizerFactory.Create("Domain", Arg.Any<string>()).Returns(mockLocalizer);

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(mockLocalizerFactory);
        services.AddGranitExceptionHandling();
        using ServiceProvider sp = services.BuildServiceProvider();

        (_, _, string? title, _) = await InvokeHandlerAsync(sp, new DomainException("Domain:ErrorCode"));

        title.ShouldBe("Message traduit.");
    }

    [Fact]
    public async Task TryHandleAsync_IHasErrorCode_LocalizerResourceNotFound_FallsBackToExceptionMessage()
    {
        IStringLocalizerFactory mockLocalizerFactory = Substitute.For<IStringLocalizerFactory>();
        IStringLocalizer mockLocalizer = Substitute.For<IStringLocalizer>();
        mockLocalizer["Domain:MissingCode"].Returns(
            new LocalizedString("Domain:MissingCode", "Domain:MissingCode", resourceNotFound: true));
        mockLocalizerFactory.Create("Domain", Arg.Any<string>()).Returns(mockLocalizer);

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(mockLocalizerFactory);
        services.AddGranitExceptionHandling();
        using ServiceProvider sp = services.BuildServiceProvider();

        (_, _, string? title, _) = await InvokeHandlerAsync(
            sp, new UserFriendlyDomainException("Domain:MissingCode", "Friendly fallback message"));

        // Falls back to exception message since it's IUserFriendlyException
        title.ShouldBe("Friendly fallback message");
    }

    [Fact]
    public async Task TryHandleAsync_IHasErrorCode_NoLocalizerFactory_FallsBackToExceptionMessage()
    {
        // No IStringLocalizerFactory registered
        using ServiceProvider sp = BuildServiceProvider();

        (_, _, string? title, _) = await InvokeHandlerAsync(
            sp, new UserFriendlyDomainException("Test:Code", "User-friendly message"));

        title.ShouldBe("User-friendly message");
    }

    [Fact]
    public async Task TryHandleAsync_IHasErrorCode_ErrorCodeWithoutColon_UsesGranitAsResourcePrefix()
    {
        IStringLocalizerFactory mockLocalizerFactory = Substitute.For<IStringLocalizerFactory>();
        IStringLocalizer mockLocalizer = Substitute.For<IStringLocalizer>();
        mockLocalizer["SimpleErrorCode"].Returns(
            new LocalizedString("SimpleErrorCode", "Localized simple error.", resourceNotFound: false));
        // When no colon is present, ExtractResourcePrefix returns "Granit"
        mockLocalizerFactory.Create("Granit", Arg.Any<string>()).Returns(mockLocalizer);

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(mockLocalizerFactory);
        services.AddGranitExceptionHandling();
        using ServiceProvider sp = services.BuildServiceProvider();

        (_, _, string? title, _) = await InvokeHandlerAsync(
            sp, new DomainException("SimpleErrorCode"));

        title.ShouldBe("Localized simple error.");
    }

    [Fact]
    public async Task TryHandleAsync_IHasErrorCode_ErrorCodeWithMultipleColons_UsesFirstSegmentAsPrefix()
    {
        IStringLocalizerFactory mockLocalizerFactory = Substitute.For<IStringLocalizerFactory>();
        IStringLocalizer mockLocalizer = Substitute.For<IStringLocalizer>();
        mockLocalizer["Module:Sub:Detail"].Returns(
            new LocalizedString("Module:Sub:Detail", "Localized multi-colon error.", resourceNotFound: false));
        // Only the first colon segment is used as prefix
        mockLocalizerFactory.Create("Module", Arg.Any<string>()).Returns(mockLocalizer);

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(mockLocalizerFactory);
        services.AddGranitExceptionHandling();
        using ServiceProvider sp = services.BuildServiceProvider();

        (_, _, string? title, _) = await InvokeHandlerAsync(
            sp, new DomainException("Module:Sub:Detail"));

        title.ShouldBe("Localized multi-colon error.");
    }

    // -------------------------------------------------------------------------
    // BusinessRuleViolationException: more specific than BusinessException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryHandleAsync_BusinessRuleViolationException_ReturnsStatus422()
    {
        using ServiceProvider sp = BuildServiceProvider();

        (int statusCode, _, _, _) = await InvokeHandlerAsync(
            sp, new BusinessRuleViolationException("Rule:Violated"));

        statusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed class DomainException(string errorCode) : Exception("fallback"), IHasErrorCode
    {
        public string ErrorCode { get; } = errorCode;
    }

    private sealed class UserFriendlyDomainException(string errorCode, string message)
        : Exception(message), IHasErrorCode, IUserFriendlyException
    {
        public string ErrorCode { get; } = errorCode;
    }

    private sealed class ArgumentExceptionMapper : IExceptionStatusCodeMapper
    {
        public int? TryGetStatusCode(Exception exception) =>
            exception is ArgumentException ? StatusCodes.Status400BadRequest : null;
    }

    private sealed class NullReturningMapper : IExceptionStatusCodeMapper
    {
        public int? TryGetStatusCode(Exception exception) => null;
    }
}
