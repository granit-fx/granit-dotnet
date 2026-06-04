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
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Validation;

/// <summary>
/// Granit module for input validation.
/// </summary>
/// <remarks>
/// <para>
/// Registers <c>AddGranitValidation()</c>, which configures FluentValidation to
/// emit structured error codes (<c>Validation:*</c>) instead of
/// human-readable messages. The SPA resolves codes from its local localization
/// dictionary served by <c>GET /api/{version}/localization</c>.
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
                .Add<ValidationLocalizationResource>("fr")
                .AddJson(
                    typeof(ValidationLocalizationResource).Assembly,
                    "Granit.Validation.Localization.Validation");
        });

        // Auto-discover validators from all loaded module assemblies.
        foreach (Assembly assembly in context.ModuleAssemblies)
        {
            context.Services.AddValidatorsFromAssembly(
                assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
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

        // Enrich OpenAPI schemas with FluentValidation constraints
        // (maxLength, minLength, pattern, required, etc.)
        context.Services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer<FluentValidationSchemaTransformer>();
        });
    }
}
