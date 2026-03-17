using System.Diagnostics;
using Granit.Core.Exceptions;
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
    IStringLocalizerFactory? localizerFactory = null) : IExceptionHandler
{
    private static readonly string FallbackTitle = "An unexpected error occurred.";

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
            Detail = detail
        };

        problemDetails.Extensions["traceId"] = traceId;

        if (exception is IHasErrorCode hasErrorCode)
        {
            problemDetails.Extensions["errorCode"] = hasErrorCode.ErrorCode;
        }

        if (exception is IHasValidationErrors hasValidationErrors)
        {
            problemDetails.Extensions["errors"] = hasValidationErrors.ValidationErrors;
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

        // For internal errors (5xx): mask the message in production.
        // Only expose if ExposeInternalErrorDetails is true (development/staging).
        if (statusCode >= 500)
        {
            return options.Value.ExposeInternalErrorDetails ? exception.Message : FallbackTitle;
        }

        // For 4xx without user-friendly marker: use the message (technical but not sensitive).
        return exception.Message;
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
