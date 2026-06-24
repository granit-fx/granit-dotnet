using System.Reflection;
using FluentValidation;
using Granit.Http.ExceptionHandling;
using Granit.Localization;
using Granit.Localization.Options;
using Granit.Modularity;
using Granit.Reflection;
using Granit.Validation.Extensions;
using Granit.Validation.Internal;
using Granit.Validation.JsonSchema;
using Granit.Validation.OpenApi;
using Granit.Validation.ServerValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Granit.Validation;

/// <summary>
/// Granit module for input validation.
/// </summary>
/// <remarks>
/// <para>
/// Registers <c>AddGranitValidation()</c> and, at initialization, wires the
/// <c>Validation</c> localizer into the global FluentValidation language manager so
/// validator error codes (<c>Validation:*</c>) resolve to fully localized, interpolated
/// messages in the request culture. Error codes remain available on
/// <c>ValidationFailure.ErrorCode</c> for programmatic handling.
/// </para>
/// <para>
/// Auto-discovers all <see cref="IValidator{T}"/> implementations from loaded
/// module assemblies. Modules no longer need to call
/// <c>AddGranitValidatorsFromAssemblyContaining&lt;T&gt;()</c> manually.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitHttpExceptionHandlingModule))]
[DependsOn(typeof(GranitLocalizationModule))]
public sealed class GranitValidationModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitValidation();

        context.Services.AddSingleton<IExceptionStatusCodeMapper, FluentValidationExceptionStatusCodeMapper>();

        context.Services.Configure<GranitLocalizationOptions>(options =>
        {
            options.Resources
                .Add<ValidationLocalizationResource>("en")
                .AddJson(
                    typeof(ValidationLocalizationResource).Assembly,
                    "Granit.Validation.Localization.Validation");
        });

        // Auto-discover validators from all loaded module assemblies.
        foreach (Assembly assembly in context.ModuleAssemblies)
        {
            assembly.TryScan(a => context.Services.AddValidatorsFromAssembly(
                a, ServiceLifetime.Scoped, includeInternalTypes: true));
        }

        // Auto-discover IServerValidatorContributor from all loaded module assemblies.
        foreach (Assembly assembly in context.ModuleAssemblies)
        {
            IEnumerable<Type> contributorTypes = assembly.GetLoadableTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                    && typeof(IServerValidatorContributor).IsAssignableFrom(t));

            foreach (Type contributorType in contributorTypes)
            {
                context.Services.AddSingleton(typeof(IServerValidatorContributor), contributorType);
            }
        }

        context.Services.AddSingleton<ServerValidatorRegistry>();

        // Reusable JSON Schema writer that projects FluentValidation rules onto a
        // JSON Schema Draft 7 fragment. Consumed by the OpenAPI transformer below
        // and by Granit.Entities (manifest schema facet).
        context.Services.AddSingleton<IJsonSchemaWriter, JsonSchemaWriter>();

        // Enrich OpenAPI schemas with FluentValidation constraints (maxLength, minLength, pattern,
        // required, etc.) across ALL registered documents without creating a spurious "v1" document.
        context.Services.ConfigureAll<OpenApiOptions>(options =>
            options.AddSchemaTransformer<FluentValidationSchemaTransformer>());
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Wires the <c>Validation</c> localizer into the global
    /// <see cref="GranitErrorCodeLanguageManager"/> so built-in validator error codes
    /// resolve to localized, interpolated messages. Deferred to initialization because the
    /// <see cref="IStringLocalizerFactory"/> is only resolvable after the container is built.
    /// </remarks>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        IStringLocalizerFactory factory =
            context.ServiceProvider.GetRequiredService<IStringLocalizerFactory>();

        IStringLocalizer localizer = factory.Create(typeof(ValidationLocalizationResource));

        if (ValidatorOptions.Global.LanguageManager is GranitErrorCodeLanguageManager manager)
        {
            manager.Localizer = localizer;
        }
        else
        {
            ValidatorOptions.Global.LanguageManager =
                new GranitErrorCodeLanguageManager { Localizer = localizer };
        }
    }
}
