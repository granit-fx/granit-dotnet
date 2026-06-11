using Granit.Validation.Endpoints.Diagnostics;
using Granit.Validation.Endpoints.Dtos;
using Granit.Validation.ServerValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Validation.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for single-field and batch server-side validation.
/// </summary>
internal static class ValidationEndpoints
{
    /// <summary>Maps all server-side validation endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapValidationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/validate", HandleValidate)
             .WithName("ValidateField")
             .WithSummary("Validates a single field value against a registered server-side validator.")
             .WithDescription("Looks up the validator by error code and returns the validation result. Returns 404 if no validator is registered for the specified error code. Use the GET /validators endpoint to discover available validators.")
             .Produces<ValidationFieldValidateResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesValidationProblem();

        group.MapPost("/validate-batch", HandleValidateBatch)
             .WithName("ValidateFieldBatch")
             .WithSummary("Validates multiple field values in a single round-trip (max 20).")
             .WithDescription("For each field, looks up the validator by error code and returns the result. Unknown error codes return ValidatorNotFound status instead of failing the entire batch.")
             .Produces<ValidationFieldValidateBatchResponse>()
             .ProducesValidationProblem();

        group.MapGet("/validators", HandleGetValidators)
             .WithName("GetRegisteredValidators")
             .WithSummary("Lists all registered server-side validator error codes.")
             .WithDescription("Returns the list of error codes that can be used with the validate and validate-batch endpoints. Useful for frontend discovery: only error codes listed here can be validated in real-time.")
             .Produces<IReadOnlyList<string>>();

        return group;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    internal static Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> HandleValidate(
        ValidationFieldValidateRequest request,
        [FromServices] ServerValidatorRegistry registry,
        [FromServices] ValidationMetrics metrics,
        HttpContext httpContext)
    {
        IServerValidator? validator = ResolveValidator(registry, request.ErrorCode, httpContext);

        if (validator is null)
        {
            // Unknown (or sensitive-and-hidden) code: record without the client-supplied code to cap cardinality.
            metrics.RecordFieldValidated(tenantId: null, errorCode: null, ValidationFieldStatus.ValidatorNotFound);
            return ValidatorNotFound();
        }

        ValidationFieldStatus status = validator.Validate(request.Value)
            ? ValidationFieldStatus.Valid
            : ValidationFieldStatus.Invalid;

        metrics.RecordFieldValidated(tenantId: null, request.ErrorCode, status);

        return TypedResults.Ok(new ValidationFieldValidateResponse(request.ErrorCode, status));
    }

    internal static Ok<ValidationFieldValidateBatchResponse> HandleValidateBatch(
        ValidationFieldValidateBatchRequest request,
        [FromServices] ServerValidatorRegistry registry,
        [FromServices] ValidationMetrics metrics,
        HttpContext httpContext)
    {
        List<ValidationFieldValidateResponse> results = new(request.Fields.Count);

        foreach (ValidationFieldValidateRequest field in request.Fields)
        {
            IServerValidator? validator = ResolveValidator(registry, field.ErrorCode, httpContext);

            ValidationFieldStatus status;
            if (validator is null)
            {
                status = ValidationFieldStatus.ValidatorNotFound;
                metrics.RecordFieldValidated(tenantId: null, errorCode: null, status);
            }
            else
            {
                status = validator.Validate(field.Value)
                    ? ValidationFieldStatus.Valid
                    : ValidationFieldStatus.Invalid;
                metrics.RecordFieldValidated(tenantId: null, field.ErrorCode, status);
            }

            results.Add(new ValidationFieldValidateResponse(field.ErrorCode, status));
        }

        return TypedResults.Ok(new ValidationFieldValidateBatchResponse(results));
    }

    internal static Ok<IReadOnlyList<string>> HandleGetValidators(
        [FromServices] ServerValidatorRegistry registry,
        HttpContext httpContext)
    {
        bool isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;

        return TypedResults.Ok<IReadOnlyList<string>>(
            registry.GetAll()
                .Where(v => !v.IsSensitive || isAuthenticated)
                .Select(v => v.ErrorCode)
                .Order()
                .ToList());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves a validator by error code. Sensitive validators are hidden
    /// from unauthenticated callers (returns <see langword="null"/>).
    /// </summary>
    private static IServerValidator? ResolveValidator(
        ServerValidatorRegistry registry,
        string errorCode,
        HttpContext httpContext)
    {
        IServerValidator? validator = registry.GetOrNull(errorCode);

        if (validator is null)
        {
            return null;
        }

        if (validator.IsSensitive && httpContext.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return validator;
    }

    private static ProblemHttpResult ValidatorNotFound() =>
        TypedResults.Problem(
            detail: "No server validator is registered for the specified error code.",
            statusCode: StatusCodes.Status404NotFound);
}
