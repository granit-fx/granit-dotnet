using System.Collections.Concurrent;
using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Granit.Validation.AspNetCore;

/// <summary>
/// Non-generic endpoint filter that automatically validates all endpoint arguments
/// for which an <see cref="IValidator{T}"/> is registered in the DI container.
/// </summary>
/// <remarks>
/// <para>
/// Applied implicitly via <see cref="GranitEndpointRouteBuilderExtensions.MapGranitGroup"/>,
/// removing the need for explicit <c>.ValidateBody&lt;T&gt;()</c> calls on each endpoint.
/// </para>
/// <para>
/// When validation fails, returns <c>422 Unprocessable Entity</c> with a
/// <c>HttpValidationProblemDetails</c> body whose <c>errors</c> values are fully
/// localized, interpolated messages in the request culture and whose <c>title</c>
/// is the localized <c>Validation:Problem:Title</c> (not the ASP.NET stock string).
/// </para>
/// <para>
/// Arguments of primitive types, strings, enums, <see cref="CancellationToken"/>,
/// <see cref="IFormFile"/>, <see cref="Guid"/>, <see cref="HttpContext"/>, and
/// <see cref="ClaimsPrincipal"/> are skipped. If no validator is registered for
/// a complex argument type, the filter passes through without error.
/// </para>
/// <para>
/// Endpoints decorated with <see cref="SkipAutoValidationAttribute"/> via
/// <c>.WithMetadata(new SkipAutoValidationAttribute())</c> are excluded from
/// automatic validation.
/// </para>
/// </remarks>
internal sealed class FluentValidationAutoEndpointFilter : IEndpointFilter
{
    private const string ProblemTitleKey = "Validation:Problem:Title";
    private const string FallbackProblemTitle = "Validation failed.";

    private static readonly ConcurrentDictionary<Type, Type> ValidatorTypeCache = new();

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        Endpoint? endpoint = context.HttpContext.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<SkipAutoValidationAttribute>() is not null)
        {
            return await next(context).ConfigureAwait(false);
        }

        foreach (object? arg in context.Arguments)
        {
            if (arg is null)
            {
                continue;
            }

            Type argType = arg.GetType();

            if (!ShouldValidate(argType))
            {
                continue;
            }

            Type validatorType = ValidatorTypeCache.GetOrAdd(
                argType,
                static t => typeof(IValidator<>).MakeGenericType(t));

            if (context.HttpContext.RequestServices.GetService(validatorType)
                is not IValidator validator)
            {
                continue;
            }

            IValidationContext validationContext = new ValidationContext<object>(arg);

            ValidationResult result = await validator
                .ValidateAsync(validationContext, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (!result.IsValid)
            {
                // Results.ValidationProblem is needed (not TypedResults.ValidationProblem)
                // to enforce 422 status code.
#pragma warning disable GRAPI001
                return Results.ValidationProblem(
                    result.ToDictionary(),
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: ResolveTitle(context.HttpContext));
#pragma warning restore GRAPI001
            }
        }

        return await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the localized 422 problem title, replacing the ASP.NET stock string
    /// "One or more validation errors occurred." with a clear, request-culture message.
    /// Falls back to <see cref="FallbackProblemTitle"/> when the localizer is unavailable.
    /// </summary>
    private static string ResolveTitle(HttpContext httpContext)
    {
        IStringLocalizer<ValidationLocalizationResource>? localizer = httpContext.RequestServices
            .GetService<IStringLocalizer<ValidationLocalizationResource>>();

        if (localizer is null)
        {
            return FallbackProblemTitle;
        }

        LocalizedString title = localizer[ProblemTitleKey];
        return title.ResourceNotFound ? FallbackProblemTitle : title.Value;
    }

    private static bool ShouldValidate(Type type) =>
        !type.IsPrimitive
        && !type.IsEnum
        && type != typeof(string)
        && type != typeof(Guid)
        && type != typeof(DateTime)
        && type != typeof(DateTimeOffset)
        && type != typeof(DateOnly)
        && type != typeof(TimeOnly)
        && type != typeof(decimal)
        && type != typeof(CancellationToken)
        && type != typeof(HttpContext)
        && !typeof(ClaimsPrincipal).IsAssignableFrom(type)
        && !typeof(IFormFile).IsAssignableFrom(type)
        && !typeof(IFormFileCollection).IsAssignableFrom(type);
}
