using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Extensions;

/// <summary>
/// Canonical options registration for Granit provider packages (notification channel
/// providers, vault backends, storage providers…): one call wires configuration binding,
/// DataAnnotations validation and fail-fast startup validation identically everywhere.
/// </summary>
/// <remarks>
/// Rule of thumb: express simple constraints as DataAnnotations on the options POCO
/// (<c>[Required]</c>, <c>[Range]</c>, <c>[Url]</c>…) and reserve a hand-written
/// <see cref="IValidateOptions{TOptions}"/> for cross-field rules DataAnnotations cannot
/// express — registered through the two-type-parameter overload so both run at startup.
/// </remarks>
public static class GranitProviderOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TOptions"/> bound to <paramref name="sectionName"/>,
    /// with <c>ValidateDataAnnotations()</c> and <c>ValidateOnStart()</c> — the mandatory
    /// provider triad. Returns the builder for further chaining.
    /// </summary>
    public static OptionsBuilder<TOptions> AddGranitProviderOptions<TOptions>(
        this IServiceCollection services,
        string sectionName)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        return services.AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    /// <summary>
    /// Same as <see cref="AddGranitProviderOptions{TOptions}"/>, additionally registering
    /// <typeparamref name="TValidator"/> for cross-field rules that DataAnnotations cannot
    /// express. The validator participates in <c>ValidateOnStart()</c>.
    /// </summary>
    public static OptionsBuilder<TOptions> AddGranitProviderOptions<TOptions, TValidator>(
        this IServiceCollection services,
        string sectionName)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IValidateOptions<TOptions>, TValidator>();
        return services.AddGranitProviderOptions<TOptions>(sectionName);
    }
}
