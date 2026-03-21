using Granit.Validation.AspNetCore;
using Granit.Validation.Endpoints.Dtos;
using Granit.Validation.Endpoints.Options;
using Granit.Validation.ServerValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Validation.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping server-side field validation endpoints.
/// </summary>
public static class ValidationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps server-side field validation endpoints under <c>/{prefix}/validation</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned <see cref="RouteGroupBuilder"/> has <strong>no authentication</strong>
    /// by default — suitable for public forms (registration, contact).
    /// Chain <c>.RequireAuthorization()</c> for authenticated applications.
    /// </para>
    /// <para>
    /// <strong>Rate limiting is strongly recommended</strong> for public endpoints.
    /// Chain <c>.RequireGranitRateLimiting("validation")</c> to protect against abuse.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="ValidationEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining (auth, rate limiting, bulkhead).</returns>
    public static RouteGroupBuilder MapGranitValidation(
        this IEndpointRouteBuilder endpoints,
        Action<ValidationEndpointsOptions>? configure = null)
    {
        ValidationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        MapValidateEndpoints(group);

        return group;
    }

    private static void MapValidateEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/validate", HandleValidate)
             .WithName("ValidateField")
             .WithSummary("Validates a single field value against a registered server-side validator.")
             .WithDescription("Looks up the validator by error code and returns the validation result. Returns 404 if no validator is registered for the specified error code. Use the GET /validators endpoint to discover available validators.")
             .Produces<ValidationFieldValidateResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/validate-batch", HandleValidateBatch)
             .WithName("ValidateFieldBatch")
             .WithSummary("Validates multiple field values in a single round-trip (max 20).")
             .WithDescription("For each field, looks up the validator by error code and returns the result. Unknown error codes return ValidatorNotFound status instead of failing the entire batch.")
             .Produces<ValidationFieldValidateBatchResponse>();

        group.MapGet("/validators", HandleGetValidators)
             .WithName("GetRegisteredValidators")
             .WithSummary("Lists all registered server-side validator error codes.")
             .WithDescription("Returns the list of error codes that can be used with the validate and validate-batch endpoints. Useful for frontend discovery: only error codes listed here can be validated in real-time.")
             .Produces<IReadOnlyList<string>>();
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    internal static Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> HandleValidate(
        ValidationFieldValidateRequest request,
        [FromServices] ServerValidatorRegistry registry)
    {
        IServerValidator? validator = registry.GetOrNull(request.ErrorCode);

        if (validator is null)
        {
            return ValidatorNotFound(request.ErrorCode);
        }

        ValidationFieldStatus status = validator.Validate(request.Value)
            ? ValidationFieldStatus.Valid
            : ValidationFieldStatus.Invalid;

        return TypedResults.Ok(new ValidationFieldValidateResponse(request.ErrorCode, status));
    }

    internal static Ok<ValidationFieldValidateBatchResponse> HandleValidateBatch(
        ValidationFieldValidateBatchRequest request,
        [FromServices] ServerValidatorRegistry registry)
    {
        List<ValidationFieldValidateResponse> results = new(request.Fields.Count);

        foreach (ValidationFieldValidateRequest field in request.Fields)
        {
            IServerValidator? validator = registry.GetOrNull(field.ErrorCode);

            ValidationFieldStatus status;
            if (validator is null)
            {
                status = ValidationFieldStatus.ValidatorNotFound;
            }
            else
            {
                status = validator.Validate(field.Value)
                    ? ValidationFieldStatus.Valid
                    : ValidationFieldStatus.Invalid;
            }

            results.Add(new ValidationFieldValidateResponse(field.ErrorCode, status));
        }

        return TypedResults.Ok(new ValidationFieldValidateBatchResponse(results));
    }

    internal static Ok<IReadOnlyList<string>> HandleGetValidators(
        [FromServices] ServerValidatorRegistry registry) =>
        TypedResults.Ok<IReadOnlyList<string>>(registry.GetAllErrorCodes().Order().ToList());

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ProblemHttpResult ValidatorNotFound(string errorCode) =>
        TypedResults.Problem(
            detail: $"No server validator is registered for error code '{errorCode}'.",
            statusCode: StatusCodes.Status404NotFound);
}
