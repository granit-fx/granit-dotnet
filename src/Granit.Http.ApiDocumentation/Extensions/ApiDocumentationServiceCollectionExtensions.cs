using System.Reflection;
using System.Text.Json.Nodes;
using Asp.Versioning;
using Granit.Http.ApiDocumentation.Internal;
using Granit.Http.ApiDocumentation.Options;
using Granit.Http.ApiDocumentation.Transformers;
using Granit.Http.ApiDocumentation.Transformers.Compatibility;
using Granit.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Extensions;

/// <summary>
/// Extensions for registering Granit OpenAPI documentation and API versioning services.
/// </summary>
public static class ApiDocumentationServiceCollectionExtensions
{
    /// <summary>
    /// <c>true</c> when Wolverine.Http is resolvable in the application's dependency
    /// closure. <see cref="WolverineOpenApiOperationTransformer"/> only rewrites
    /// metadata whose runtime types live in the <c>Wolverine.Http</c> assembly
    /// (<c>HttpChain</c> endpoint metadata, <c>[WolverinePost]</c> attributes), so
    /// registering it in a Wolverine-less app is pure per-operation overhead.
    /// <c>Type.GetType</c> with an assembly-qualified name probes the exact assembly
    /// the transformer reflects on, without adding a package reference.
    /// </summary>
    private static readonly bool IsWolverineHttpPresent =
        Type.GetType("Wolverine.Http.WolverineHttpOptions, Wolverine.Http") is not null;

    /// <summary>
    /// Adds OpenAPI document generation and URL-based API versioning by reading the
    /// <c>"Http:ApiDocumentation"</c> section from configuration.
    /// Each integer in <c>Http:ApiDocumentation:MajorVersions</c> generates one distinct OpenAPI
    /// document and is a routable <c>/api/v{version:apiVersion}/…</c> URL segment
    /// (query string <c>?api-version=</c> fallback).
    /// Call <c>app.MapGranitOpenApiDocuments()</c> in <c>Program.cs</c> to expose the JSON
    /// endpoints, or <c>app.UseGranitApiDocumentation()</c> from the
    /// <c>Granit.Http.ApiDocumentation.Scalar</c> package to also host the interactive UI.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="IHostApplicationBuilder"/> because OpenAPI document endpoints must be
    /// registered at startup (one per major version), which requires reading configuration before
    /// the DI container is built.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="moduleAssemblies">
    /// Assemblies scanned for <see cref="ISchemaExampleProvider"/> implementations — the Granit
    /// module system passes its module assembly registry. When <c>null</c> or empty (direct
    /// host-builder usage outside the module system), falls back to
    /// <see cref="AppDomain.CurrentDomain"/>.
    /// </param>
    public static IHostApplicationBuilder AddGranitApiDocumentation(
        this IHostApplicationBuilder builder,
        IReadOnlyList<Assembly>? moduleAssemblies = null)
    {
        builder.Services
            .AddOptions<ApiDocumentationOptions>()
            .BindConfiguration(ApiDocumentationOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                static options => EffectiveMajorVersions(options).Contains(options.DefaultMajorVersion),
                $"{nameof(ApiDocumentationOptions.DefaultMajorVersion)} must be one of " +
                $"{nameof(ApiDocumentationOptions.MajorVersions)} — the default version cannot " +
                "point to an undocumented, unroutable API version.")
            .ValidateOnStart();

        AddGranitApiVersioning(builder.Services);

        ApiDocumentationOptions options = ReadOptions(builder.Configuration);
        if (options.MajorVersions.Count == 0)
        {
            options.MajorVersions.Add(1);
        }

        RegisterTransformerServices(builder.Services, moduleAssemblies);

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
    /// <param name="title">Document title shown in the OpenAPI info block.</param>
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

        RegisterTransformerServices(builder.Services, moduleAssemblies: null);
        RegisterGranitDocument(
            builder.Services,
            documentName,
            title,
            version: "1",
            ReadOptions(builder.Configuration),
            shouldInclude);

        return builder;
    }

    /// <summary>
    /// Registers <c>Asp.Versioning</c> with URL segment and query string readers.
    /// Primary reader: <c>/api/v{version:apiVersion}/resource</c>.
    /// Fallback reader: <c>?api-version=1.0</c> (visible in access logs).
    /// <see cref="ApiDocumentationOptions"/> is the single source of truth:
    /// <c>DefaultMajorVersion</c> drives <see cref="ApiVersioningOptions.DefaultApiVersion"/> and
    /// <c>ReportApiVersions</c> drives the <c>api-supported-versions</c> response headers.
    /// </summary>
    private static void AddGranitApiVersioning(IServiceCollection services)
    {
        services.AddApiVersioning()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        // Deferred configuration: ApiVersioningOptions reads ApiDocumentationOptions at resolution time.
        services
            .AddOptions<ApiVersioningOptions>()
            .Configure<IOptions<ApiDocumentationOptions>>((options, docOpts) =>
            {
                ApiDocumentationOptions docs = docOpts.Value;
                options.DefaultApiVersion = new ApiVersion(docs.DefaultMajorVersion);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = docs.ReportApiVersions;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new QueryStringApiVersionReader("api-version"));
            });
    }

    /// <summary>
    /// The version list the documents are generated from: <c>MajorVersions</c> as configured,
    /// or the implicit <c>[1]</c> when the list is empty (mirrors <see cref="ReadOptions"/>).
    /// </summary>
    private static IEnumerable<int> EffectiveMajorVersions(ApiDocumentationOptions options) =>
        options.MajorVersions.Count == 0 ? [1] : options.MajorVersions;

    private static ApiDocumentationOptions ReadOptions(IConfiguration configuration)
    {
        // The .NET configuration binder appends to existing IList values instead of replacing them.
        // Clearing MajorVersions before Bind prevents duplicates when config mirrors the default value.
        ApiDocumentationOptions options = new() { MajorVersions = [] };
        configuration.GetSection(ApiDocumentationOptions.SectionName).Bind(options);
        return options;
    }

    private static void RegisterTransformerServices(
        IServiceCollection services,
        IReadOnlyList<Assembly>? moduleAssemblies)
    {
        // Transformers must be registered before AddOpenApi to be resolved via DI. TryAdd keeps this
        // idempotent so AddGranitApiDocumentation and AddGranitOpenApiDocument can both be called.
        services.TryAddTransient<JwtBearerSecuritySchemeTransformer>();
        services.TryAddTransient<OAuth2SecuritySchemeTransformer>();
        services.TryAddTransient<ProblemDetailsSchemaDocumentTransformer>();
        services.TryAddTransient<InternalApiDocumentTransformer>();
        services.TryAddTransient<SortedTagsDocumentTransformer>();
        services.TryAddTransient<TenantHeaderOperationTransformer>();
        services.TryAddTransient<ProblemDetailsResponseOperationTransformer>();
        services.TryAddTransient<SecurityRequirementOperationTransformer>();
        services.TryAddTransient<ParameterDescriptionOperationTransformer>();
        services.TryAddTransient<DeprecationOperationTransformer>();
        services.TryAddTransient<SchemaExampleSchemaTransformer>();
        services.TryAddTransient<SingleValueObjectSchemaTransformer>();

        // Compatibility shims — normalize raw ASP.NET Core / Wolverine output. See
        // Transformers/Compatibility/. The Wolverine shim is gated on actual Wolverine
        // presence (see IsWolverineHttpPresent).
        services.TryAddTransient<DictionarySchemaExampleOperationTransformer>();
        services.TryAddTransient<NullableIntSchemaOperationTransformer>();
        services.TryAddTransient<BinaryResponseContentTypeOperationTransformer>();
        services.TryAddTransient<JsonElementSchemaTransformer>();
        services.TryAddTransient<Int32SchemaTransformer>();
        if (IsWolverineHttpPresent)
        {
            services.TryAddTransient<WolverineOpenApiOperationTransformer>();
        }

        // RFC 8594: emit Deprecation/Sunset/Link headers from DeprecatedAttribute metadata
        // with zero extra host wiring.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IStartupFilter, DeprecationHeadersStartupFilter>());

        DiscoverSchemaExampleProviders(services, moduleAssemblies);
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
            if (IsWolverineHttpPresent)
            {
                openApiOptions.AddOperationTransformer<WolverineOpenApiOperationTransformer>();
            }

            openApiOptions.AddOperationTransformer<ProblemDetailsResponseOperationTransformer>();
            openApiOptions.AddOperationTransformer<DictionarySchemaExampleOperationTransformer>();
            openApiOptions.AddOperationTransformer<SecurityRequirementOperationTransformer>();
            openApiOptions.AddOperationTransformer<NullableIntSchemaOperationTransformer>();
            openApiOptions.AddOperationTransformer<ParameterDescriptionOperationTransformer>();
            openApiOptions.AddOperationTransformer<BinaryResponseContentTypeOperationTransformer>();
            openApiOptions.AddOperationTransformer<DeprecationOperationTransformer>();
            openApiOptions.AddSchemaTransformer<SchemaExampleSchemaTransformer>();
            openApiOptions.AddSchemaTransformer<SingleValueObjectSchemaTransformer>();
            openApiOptions.AddSchemaTransformer<JsonElementSchemaTransformer>();
            openApiOptions.AddSchemaTransformer<Int32SchemaTransformer>();
        });
    }

    /// <summary>
    /// Discovers <see cref="ISchemaExampleProvider"/> implementations. Prefers the Granit
    /// module assembly registry (deterministic — exactly the assemblies composed into the
    /// application); falls back to the <see cref="AppDomain"/> scan for direct host-builder
    /// usage, where lazy assembly loading may miss providers whose assembly is not yet
    /// loaded. <c>SchemaExampleSchemaTransformer</c> logs at Debug when the resulting
    /// provider set is empty, so a miss is never silent.
    /// </summary>
    private static void DiscoverSchemaExampleProviders(
        IServiceCollection services,
        IReadOnlyList<Assembly>? moduleAssemblies)
    {
        Type interfaceType = typeof(ISchemaExampleProvider);

        IEnumerable<Assembly> assemblies = moduleAssemblies is { Count: > 0 }
            ? moduleAssemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetLoadableExportedTypes().Where(t =>
                         t is { IsAbstract: false, IsInterface: false }
                         && interfaceType.IsAssignableFrom(t)))
            {
                services.TryAddEnumerable(
                    ServiceDescriptor.Singleton(interfaceType, type));
            }
        }
    }
}
