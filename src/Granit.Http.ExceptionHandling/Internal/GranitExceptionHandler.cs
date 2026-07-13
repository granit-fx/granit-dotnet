using System.Diagnostics;
using System.Text.Json;
using Granit.Exceptions;
using Granit.Http.ExceptionHandling.Options;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.ExceptionHandling.Internal;

/// <summary>
/// Centralized exception handler for ASP.NET Core APIs.
/// Implements the native <see cref="IExceptionHandler"/> interface (.NET 8+).
/// </summary>
/// <remarks>
/// Pipeline:
/// <list type="number">
///   <item><c>OperationCanceledException</c> — logs Information, returns true without writing a response.</item>
///   <item>Resolves HTTP status code via all registered <see cref="IExceptionStatusCodeMapper"/> (chain of responsibility).</item>
///   <item>Logs at the appropriate level (5xx → Error, 4xx → Warning, 499 → Information).</item>
///   <item>Builds a <see cref="ProblemDetails"/> object (RFC 7807) with <c>traceId</c>.</item>
///   <item>Writes the response via <see cref="IProblemDetailsService.TryWriteAsync"/>.</item>
/// </list>
/// <para>
/// <b>ISO 27001 security rule:</b> messages of non-<see cref="IUserFriendlyException"/> exceptions
/// are NEVER forwarded to the client in production. The original exception is always logged.
/// </para>
/// </remarks>
internal sealed partial class GranitExceptionHandler(
    IEnumerable<IExceptionStatusCodeMapper> statusCodeMappers,
    IProblemDetailsService problemDetailsService,
    IOptions<ExceptionHandlingOptions> options,
    ILoggerFactory loggerFactory,
    ValidationErrorsSanitizer validationErrorsSanitizer,
    IStringLocalizerFactory? localizerFactory = null) : IExceptionHandler
{
    private const string FallbackTitle = "An unexpected error occurred.";

    // Generic 4xx titles — returned when ExposeInternalErrorDetails is false
    // and the exception is not IUserFriendlyException. Avoids leaking business
    // logic details, PII, or tenant identifiers embedded in raw exception
    // messages (ISO 27001, GDPR Art. 32, OWASP ASVS V7.4.1).
    private const string FallbackTitle404 = "Resource not found.";
    private const string FallbackTitle403 = "Forbidden.";
    private const string FallbackTitle409 = "Conflict.";
    private const string FallbackTitle422 = "Validation failed.";
    private const string FallbackTitle4xx = "Invalid request.";

    // Stable error code surfaced when a request body fails to bind (malformed JSON,
    // invalid enum, wrong shape). Exposed as-is on the ProblemDetails; it is not an
    // IHasErrorCode-backed code, so no localization key is required for it.
    private const string MalformedRequestBodyErrorCode = "Http:MalformedRequestBody";

    private readonly ILogger _logger = loggerFactory.CreateLogger<GranitExceptionHandler>();

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // OperationCanceledException: client closed the connection.
        // No response is written; log at Information to avoid polluting error alerting.
        if (exception is OperationCanceledException)
        {
            LogRequestCancelled("OperationCanceledException");
            return true;
        }

        int statusCode = ResolveStatusCode(exception);

        LogException(exception, statusCode);

        ProblemDetails problemDetails = BuildProblemDetails(httpContext, exception, statusCode);

        // Explicitly set the HTTP status code before writing the response body.
        // ExceptionHandlerMiddleware resets it to 500; ProblemDetailsService may not
        // propagate ProblemDetails.Status back to Response.StatusCode in all scenarios.
        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        }).ConfigureAwait(false);
    }

    private int ResolveStatusCode(Exception exception)
    {
        foreach (IExceptionStatusCodeMapper mapper in statusCodeMappers)
        {
            int? code = mapper.TryGetStatusCode(exception);
            if (code.HasValue)
            {
                return code.Value;
            }
        }

        return StatusCodes.Status500InternalServerError;
    }

    private void LogException(Exception exception, int statusCode)
    {
        string exceptionType = exception.GetType().Name;
        if (statusCode >= 500)
        {
            LogUnhandledException(exception, exceptionType);
        }
        else if (statusCode == 499)
        {
            LogRequestCancelled(exceptionType);
        }
        else
        {
            LogHandledException(exception, exceptionType, statusCode);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception: {ExceptionType}")]
    private partial void LogUnhandledException(Exception exception, string exceptionType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request cancelled: {ExceptionType}")]
    private partial void LogRequestCancelled(string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handled exception: {ExceptionType} \u2192 {StatusCode}")]
    private partial void LogHandledException(Exception exception, string exceptionType, int statusCode);

    private ProblemDetails BuildProblemDetails(HttpContext httpContext, Exception exception, int statusCode)
    {
        string title = ResolveTitle(exception, statusCode);
        string? detail = ResolveDetail(exception, statusCode);
        string traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            // RFC 7807 `instance` identifies the occurrence — the request path aids
            // correlation without leaking anything the client does not already know.
            // `Type` is left null: ProblemDetailsDefaults fills the RFC 9110 URI for
            // the status code when the response is written.
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;

        if (exception is IHasErrorCode hasErrorCode)
        {
            problemDetails.Extensions["errorCode"] = hasErrorCode.ErrorCode;
        }

        if (exception is IHasValidationErrors hasValidationErrors)
        {
            problemDetails.Extensions["errors"] =
                validationErrorsSanitizer.Sanitize(hasValidationErrors.ValidationErrors);
        }

        // Body binding failure (e.g. invalid enum in the JSON payload) arrives as a
        // BadHttpRequestException wrapping a JsonException. Surface a stable error code
        // and the JSON path of the faulty member so clients can pinpoint it. Only the
        // schema path is exposed — never the received value or raw message — so the
        // ISO 27001 masking of non-IUserFriendlyException 4xx titles stays intact.
        if (exception is BadHttpRequestException { InnerException: JsonException json })
        {
            problemDetails.Extensions["errorCode"] = MalformedRequestBodyErrorCode;
            if (json.Path is { Length: > 0 } jsonPath)
            {
                problemDetails.Extensions["jsonPath"] = jsonPath;
            }
        }

        return problemDetails;
    }

    private string ResolveTitle(Exception exception, int statusCode)
    {
        // Try localized title via error code if the localizer is available.
        // The resource is derived from the error code prefix: "Features:NotEnabled" → "Features".
        // JsonStringLocalizerFactory.Create(string, string) resolves by [LocalizationResourceName] name.
        if (exception is IHasErrorCode hasErrorCode && localizerFactory is not null)
        {
            string errorCode = hasErrorCode.ErrorCode;
            string resourceName = ExtractResourcePrefix(errorCode);
            string assemblyName = typeof(GranitExceptionHandler).Assembly.GetName().Name!;

            IStringLocalizer localizer = localizerFactory.Create(resourceName, assemblyName);
            LocalizedString localized = localizer[errorCode];
            if (!localized.ResourceNotFound)
            {
                return localized.Value;
            }
        }

        // For user-friendly exceptions, use the exception message as title.
        if (exception is IUserFriendlyException)
        {
            return exception.Message;
        }

        // Dev/staging: expose raw message for debugging (all status codes).
        if (options.Value.ExposeInternalErrorDetails)
        {
            return exception.Message;
        }

        // Production: mask messages for both 5xx AND 4xx non-user-friendly.
        // 4xx exception messages may contain internal context (user ids, tenant
        // ids, SQL fragments, internal paths) that must not reach the client.
        // Opt into exposing a specific 4xx message by implementing
        // IUserFriendlyException on the exception type.
        return statusCode switch
        {
            >= 500 => FallbackTitle,
            StatusCodes.Status404NotFound => FallbackTitle404,
            StatusCodes.Status403Forbidden => FallbackTitle403,
            StatusCodes.Status409Conflict => FallbackTitle409,
            StatusCodes.Status422UnprocessableEntity => FallbackTitle422,
            _ => FallbackTitle4xx,
        };
    }

    /// <summary>
    /// Extracts the resource name prefix from an error code.
    /// For example, <c>"Features:NotEnabled"</c> returns <c>"Features"</c>.
    /// Falls back to <c>"Granit"</c> when no prefix is present.
    /// </summary>
    private static string ExtractResourcePrefix(string errorCode)
    {
        int colonIndex = errorCode.IndexOf(':', StringComparison.Ordinal);
        return colonIndex > 0 ? errorCode[..colonIndex] : "Granit";
    }

    private string? ResolveDetail(Exception exception, int statusCode)
    {
        // For user-friendly exceptions, detail is null (title already carries the message).
        if (exception is IUserFriendlyException)
        {
            return null;
        }

        // For internal errors: only expose stack trace in development/staging.
        if (statusCode >= 500)
        {
            return options.Value.ExposeInternalErrorDetails ? exception.ToString() : null;
        }

        return null;
    }
}
