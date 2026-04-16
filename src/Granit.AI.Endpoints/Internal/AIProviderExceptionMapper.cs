using System.Net;
using Granit.AI.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Granit.AI.Endpoints.Internal;

/// <summary>
/// Maps AI provider exceptions to RFC 7807 ProblemHttpResult responses.
/// </summary>
internal static class AIProviderExceptionMapper
{
    internal static ProblemHttpResult MapException(Exception exception) =>
        MapException(exception, null, null);

    internal static ProblemHttpResult MapException(Exception exception, string? model, string? provider) =>
        exception switch
        {
            AIWorkspaceNotFoundException ex => TypedResults.Problem(
                detail: $"AI workspace '{ex.WorkspaceName}' was not found.",
                statusCode: StatusCodes.Status404NotFound),

            AIWorkspaceNotActiveException ex => TypedResults.Problem(
                detail: $"AI workspace '{ex.WorkspaceName}' is not active.",
                statusCode: StatusCodes.Status422UnprocessableEntity),

            AIProviderNotRegisteredException ex => TypedResults.Problem(
                detail: $"No AI provider is registered for '{ex.ProviderName}'.",
                statusCode: StatusCodes.Status502BadGateway),

            OperationCanceledException => TypedResults.Problem(
                detail: "The AI service request was cancelled.",
                statusCode: StatusCodes.Status503ServiceUnavailable),

            HttpRequestException { StatusCode: HttpStatusCode.NotFound } => TypedResults.Problem(
                detail: model is not null && provider is not null
                    ? $"AI model '{model}' not found on provider '{provider}'. Verify the model is available."
                    : "The requested AI model was not found on the provider.",
                statusCode: StatusCodes.Status422UnprocessableEntity),

            HttpRequestException httpEx => TypedResults.Problem(
                detail: httpEx.StatusCode is not null
                    ? $"The AI service returned HTTP {(int)httpEx.StatusCode} and is currently unavailable."
                    : "The AI service is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable),

            IOException => TypedResults.Problem(
                detail: "The connection to the AI service was interrupted.",
                statusCode: StatusCodes.Status503ServiceUnavailable),

            _ => TypedResults.Problem(
                detail: "An unexpected error occurred while communicating with the AI service.",
                statusCode: StatusCodes.Status502BadGateway),
        };

    internal static string GetErrorMessage(Exception exception, string? model, string? provider) =>
        exception switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.NotFound } when model is not null && provider is not null =>
                $"AI model '{model}' not found on provider '{provider}'. Verify the model is available.",
            HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
                "The requested AI model was not found on the provider.",
            HttpRequestException httpEx when httpEx.StatusCode is not null =>
                $"The AI service returned HTTP {(int)httpEx.StatusCode} and is currently unavailable.",
            OperationCanceledException => "The AI service request was cancelled.",
            IOException => "The connection to the AI service was interrupted.",
            _ => "An unexpected error occurred while communicating with the AI service.",
        };
}
