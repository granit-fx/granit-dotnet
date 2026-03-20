using Granit.AI.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Granit.AI.Endpoints.Internal;

/// <summary>
/// Maps AI provider exceptions to RFC 7807 ProblemHttpResult responses.
/// </summary>
internal static class AIProviderExceptionMapper
{
    internal static ProblemHttpResult MapException(Exception exception) => exception switch
    {
        AIWorkspaceNotFoundException ex => TypedResults.Problem(
            detail: $"AI workspace '{ex.WorkspaceName}' was not found.",
            statusCode: StatusCodes.Status404NotFound),

        AIProviderNotRegisteredException ex => TypedResults.Problem(
            detail: $"No AI provider is registered for '{ex.ProviderName}'.",
            statusCode: StatusCodes.Status502BadGateway),

        OperationCanceledException => TypedResults.Problem(
            detail: "The AI service request was cancelled.",
            statusCode: StatusCodes.Status503ServiceUnavailable),

        HttpRequestException => TypedResults.Problem(
            detail: "The AI service is currently unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable),

        _ => TypedResults.Problem(
            detail: "An unexpected error occurred while communicating with the AI service.",
            statusCode: StatusCodes.Status502BadGateway),
    };
}
