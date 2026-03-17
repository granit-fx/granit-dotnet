namespace Granit.Validation.AspNetCore;

/// <summary>
/// Marker attribute that excludes an endpoint from automatic FluentValidation.
/// </summary>
/// <remarks>
/// Apply via endpoint metadata to opt out of the <see cref="FluentValidationAutoEndpointFilter"/>
/// that is applied by <see cref="GranitEndpointRouteBuilderExtensions.MapGranitGroup"/>:
/// <code>
/// group.MapPost("/special", Handler)
///     .WithMetadata(new SkipAutoValidationAttribute());
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SkipAutoValidationAttribute : Attribute;
