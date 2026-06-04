using System.Reflection;
using System.Text.Json.Nodes;
using Asp.Versioning;
using Granit.Http.ApiDocumentation.Options;
using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Extensions;

/// <summary>
/// Extensions for registering Granit OpenAPI documentation services.
/// </summary>
public static class ApiDocumentationServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenAPI document generation by reading the <c>"ApiDocumentation"</c> section from configuration.
    /// Each integer in <c>ApiDocumentation:MajorVersions</c> generates one distinct OpenAPI document.
    /// Call <c>app.UseGranitApiDocumentation()</c> in <c>Program.cs</c> to expose the Scalar UI.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="IHostApplicationBuilder"/> because OpenAPI document endpoints must be
    /// registered at startup (one per major version), which requires reading configuration before
    /// the DI container is built.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitApiDocumentation(
        this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<ApiDocumentationOptions>()
            .BindConfiguration(ApiDocumentationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        ApiDocumentationOptions options = ReadOptions(builder.Configuration);
        if (options.MajorVersions.Count == 0)
        {
            options.MajorVersions.Add(1);
        }

        RegisterTransformerServices(builder.Services);

        foreach (int majorVersion in options.MajorVersions)
        {
            ApiVersion apiVersion = new(majorVersion);
            RegisterGranitDocument(
                builder.Services,
                documentName: $"v{majorVersion}",
                title: options.Title,
                version: apiVersion.ToString(),
                options,
                shouldInclude: null);
        }

        return builder;
    }

    /// <summary>
    /// Registers a single additional OpenAPI document with the full Granit transformer chain
    /// (int32 normalization, problem-details schema, sorted tags, security schemes, …) and a custom
    /// <paramref name="shouldInclude"/> predicate. Use this to slice the API surface into per-module
    /// (or per-audience) documents while keeping every document faithful to the framework's served
    /// OpenAPI — without it, a bare <c>AddOpenApi</c> emits the raw ASP.NET Core output (e.g.
    /// <c>type: ["integer","string"]</c> on every <c>int</c>).
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="documentName">OpenAPI document name (e.g. a module slug). Becomes the route at <c>/openapi/{documentName}.json</c>.</param>
    /// <param name="title">Document title shown in the OpenAPI info block and Scalar UI.</param>
    /// <param name="shouldInclude">Predicate selecting which endpoints belong to this document — typically <c>d => d.GroupName == "&lt;slug&gt;"</c>.</param>
    public static IHostApplicationBuilder AddGranitOpenApiDocument(
        this IHostApplicationBuilder builder,
        string documentName,
        string title,
        Func<ApiDescription, bool> shouldInclude)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(shouldInclude);

        RegisterTransformerServices(builder.Services);
        RegisterGranitDocument(
            builder.Services,
            documentName,
            title,
            version: "1",
            ReadOptions(builder.Configuration),
            shouldInclude);

        return builder;
    }

    private static ApiDocumentationOptions ReadOptions(IConfiguration configuration)
    {
        // The .NET configuration binder appends to existing IList values instead of replacing them.
        // Clearing MajorVersions before Bind prevents duplicates when config mirrors the default value.
        ApiDocumentationOptions options = new() { MajorVersions = [] };
        configuration.GetSection(ApiDocumentationOptions.SectionName).Bind(options);
        return options;
    }

    private static void RegisterTransformerServices(IServiceCollection services)
    {
        // Transformers must be registered before AddOpenApi to be resolved via DI. TryAdd keeps this
        // idempotent so AddGranitApiDocumentation and AddGranitOpenApiDocument can both be called.
        services.TryAddTransient<JwtBearerSecuritySchemeTransformer>();
        services.TryAddTransient<OAuth2SecuritySchemeTransformer>();
        services.TryAddTransient<ProblemDetailsSchemaDocumentTransformer>();
        services.TryAddTransient<InternalApiDocumentTransformer>();
        services.TryAddTransient<SortedTagsDocumentTransformer>();
        services.TryAddTransient<TenantHeaderOperationTransformer>();
        services.TryAddTransient<WolverineOpenApiOperationTransformer>();
        services.TryAddTransient<ProblemDetailsResponseOperationTransformer>();
        services.TryAddTransient<DictionarySchemaExampleOperationTransformer>();
        services.TryAddTransient<SecurityRequirementOperationTransformer>();
        services.TryAddTransient<NullableIntSchemaOperationTransformer>();
        services.TryAddTransient<ParameterDescriptionOperationTransformer>();
        services.TryAddTransient<SchemaExampleSchemaTransformer>();
        services.TryAddTransient<JsonElementSchemaTransformer>();
        services.TryAddTransient<Int32SchemaTransformer>();

        DiscoverSchemaExampleProviders(services);
    }

    private static void RegisterGranitDocument(
        IServiceCollection services,
        string documentName,
        string title,
        string version,
        ApiDocumentationOptions options,
        Func<ApiDescription, bool>? shouldInclude)
    {
        services.AddOpenApi(documentName, openApiOptions =>
        {
            if (shouldInclude is not null)
            {
                openApiOptions.ShouldInclude = shouldInclude;
            }

            openApiOptions.AddDocumentTransformer((doc, ctx, cancellationToken) =>
            {
                doc.Info = new OpenApiInfo
                {
                    Title = title,
                    Version = version,
                    Description = options.Description,
                    Contact = options.ContactEmail is not null
                        ? new OpenApiContact { Email = options.ContactEmail }
                        : null,
                };

                if (!string.IsNullOrEmpty(options.LogoUrl))
                {
                    doc.Info.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    doc.Info.Extensions["x-logo"] = new JsonNodeExtension(new JsonObject
                    {
                        ["url"] = options.LogoUrl,
                        ["altText"] = title,
                    });
                }

                return Task.CompletedTask;
            });

            openApiOptions.AddDocumentTransformer<ProblemDetailsSchemaDocumentTransformer>();
            openApiOptions.AddDocumentTransformer<JwtBearerSecuritySchemeTransformer>();
            openApiOptions.AddDocumentTransformer<OAuth2SecuritySchemeTransformer>();
            openApiOptions.AddDocumentTransformer<InternalApiDocumentTransformer>();
            openApiOptions.AddDocumentTransformer<SortedTagsDocumentTransformer>();
            openApiOptions.AddOperationTransformer<TenantHeaderOperationTransformer>();
            openApiOptions.AddOperationTransformer<WolverineOpenApiOperationTransformer>();
            openApiOptions.AddOperationTransformer<ProblemDetailsResponseOperationTransformer>();
            openApiOptions.AddOperationTransformer<DictionarySchemaExampleOperationTransformer>();
            openApiOptions.AddOperationTransformer<SecurityRequirementOperationTransformer>();
            openApiOptions.AddOperationTransformer<NullableIntSchemaOperationTransformer>();
            openApiOptions.AddOperationTransformer<ParameterDescriptionOperationTransformer>();
            openApiOptions.AddSchemaTransformer<SchemaExampleSchemaTransformer>();
            openApiOptions.AddSchemaTransformer<JsonElementSchemaTransformer>();
            openApiOptions.AddSchemaTransformer<Int32SchemaTransformer>();
        });
    }

    private static void DiscoverSchemaExampleProviders(IServiceCollection services)
    {
        Type interfaceType = typeof(ISchemaExampleProvider);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetExportedTypes();
            }
            catch (Exception ex) when (ex is ReflectionTypeLoadException or FileNotFoundException or FileLoadException or TypeLoadException)
            {
                // An assembly in the load context references a dependency that is not present
                // (e.g. a module exposing Wolverine-derived types when Wolverine is not flowed).
                // Skip it — its example providers, if any, simply will not be discovered.
                continue;
            }

            foreach (Type type in types.Where(t =>
                         t is { IsAbstract: false, IsInterface: false }
                         && interfaceType.IsAssignableFrom(t)))
            {
                services.TryAddEnumerable(
                    ServiceDescriptor.Singleton(interfaceType, type));
            }
        }
    }
}
