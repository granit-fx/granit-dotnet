using FluentValidation;
using Granit.Validation.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Validation.Extensions;

/// <summary>
/// Extension methods for registering Granit validation services.
/// </summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers Granit validation services and configures FluentValidation
    /// to emit structured error codes instead of human-readable messages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sets <c>ValidatorOptions.Global.LanguageManager</c> to
    /// <c>GranitErrorCodeLanguageManager</c>, which resolves built-in validator error
    /// codes to localized, interpolated messages once the localizer is wired at
    /// application initialization. Until then it degrades to the bare
    /// <c>Validation:{ValidatorName}</c> code.
    /// </para>
    /// <para>
    /// Sets <c>ValidatorOptions.Global.DisplayNameResolver</c> to humanize PascalCase
    /// member names (<c>NewPassword</c> → <c>New password</c>) so the <c>{PropertyName}</c>
    /// placeholder reads naturally in every validation message.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitValidation(this IServiceCollection services)
    {
        ValidatorOptions.Global.LanguageManager = new GranitErrorCodeLanguageManager();
        ValidatorOptions.Global.DisplayNameResolver = static (_, member, _) =>
            member is null ? null : PropertyNameHumanizer.Humanize(member.Name);
        return services;
    }

    /// <summary>
    /// Registers all <see cref="IValidator{T}"/> implementations from the assembly
    /// containing <typeparamref name="T"/> as scoped services.
    /// </summary>
    /// <typeparam name="T">A type in the assembly to scan for validators.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitValidatorsFromAssemblyContaining<T>(
        this IServiceCollection services) =>
        services.AddValidatorsFromAssemblyContaining<T>(ServiceLifetime.Scoped, includeInternalTypes: true);
}
