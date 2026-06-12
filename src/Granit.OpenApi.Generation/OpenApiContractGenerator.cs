using System.Reflection;
using Granit.Extensions;
using Granit.Http.ApiDocumentation.Extensions;
using Granit.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.OpenApi.Generation;

/// <summary>
/// Build-time OpenAPI contract generator shared by every Granit bounded context (the framework and
/// every downstream repo). Composes the supplied <see cref="OpenApiContractModule"/> set through the
/// Granit module system and emits one OpenAPI 3.1 document per module — no running host, no
/// infrastructure. The single home for the doc-gen quirks every generator would otherwise re-discover.
/// </summary>
/// <remarks>
/// Drive it from a <c>Microsoft.NET.Sdk.Web</c> project with <c>OpenApiGenerateDocuments=true</c>: the
/// build invokes <c>GetDocument.Insider</c>, which runs <c>Host.StartAsync()</c> against an inert
/// server. All <see cref="IHostedService"/>, <see cref="IStartupValidator"/> and
/// <see cref="IValidateOptions{T}"/> registrations are stripped so that start-up is a no-op and no
/// module reaches for real infrastructure (DB / Vault / object store / message bus).
/// <para>
/// This helper deliberately does <b>not</b> work around handler-injected application services. A
/// contract-only generator composes only the <c>.Endpoints</c> layer, so services whose impl lives in
/// a persistence/caching/provider module are unregistered; minimal-API binding then mis-infers such an
/// interface parameter as a request body and throws on GET/DELETE. The correct fix is at the source —
/// annotate every application-service handler parameter with <c>[FromServices]</c>, which forces service
/// binding regardless of registration. Where that discipline is not yet in place, pass the leaking
/// contracts to <see cref="Extensions.OpenApiContractServiceCollectionExtensions.AddContractServiceStubs"/> from
/// the <c>configure</c> hook — an explicit, self-documenting list, never a blanket mask.
/// </para>
/// </remarks>
public static class OpenApiContractGenerator
{
    /// <summary>Composes <paramref name="modules"/> under <typeparamref name="TRootModule"/> and runs the doc-gen host.</summary>
    /// <typeparam name="TRootModule">Root Granit module whose <c>[DependsOn]</c> graph pulls in every endpoint module.</typeparam>
    /// <param name="args">Process arguments forwarded to <see cref="WebApplication.CreateBuilder(string[])"/>.</param>
    /// <param name="modules">The slug-to-route-mapping pairs; one generated document each.</param>
    /// <param name="configure">
    /// Optional hook run after the base services are registered and before module composition — for
    /// modules that expose an explicit <c>AddGranit*Endpoints</c> service registration, to stub a
    /// service the contract needs (e.g. a frontend entry so BFF emits paths), or to declare leaking
    /// application-service contracts via <c>AddContractServiceStubs</c>.
    /// </param>
    public static async Task RunAsync<TRootModule>(
        string[] args,
        IReadOnlyList<OpenApiContractModule> modules,
        Action<WebApplicationBuilder>? configure = null)
        where TRootModule : GranitModule
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(modules);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Cheap, infra-free ASP.NET services the Granit OpenAPI transformers assume a host wires
        // (e.g. JwtBearerSecuritySchemeTransformer constructor-injects IAuthenticationSchemeProvider).
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorizationBuilder();

        configure?.Invoke(builder);

        // One OpenAPI document per module, sliced by the GroupName stamped on its routes below, each
        // carrying the full Granit transformer chain so the artifacts match the framework's served
        // output (int32 normalized, int64 string-union kept, problem-details enriched, tags sorted).
        foreach (string slug in modules.Select(module => module.Slug))
        {
            builder.AddGranitOpenApiDocument(slug, slug, description => description.GroupName == slug);
        }

        await builder.AddGranitAsync<TRootModule>().ConfigureAwait(false);

        // GranitHttpApiDocumentationModule registers a "v1" document (and any other major-version
        // documents) as part of normal module composition. Strip all OpenAPI service descriptors for
        // document names not in the slug set so the generator emits exactly the set declared in
        // `modules` — no spurious root artifact. Two registration shapes must be removed:
        // (1) Keyed services (OpenApiDocumentService etc.) — resolved at generation time.
        // (2) IConfigureNamedOptions<OpenApiOptions> — used by GetDocumentNames() to enumerate
        //     document names; leaving these causes the tool to attempt generation and then fail on (1).
        HashSet<string> slugSet = new(modules.Select(m => m.Slug), StringComparer.OrdinalIgnoreCase);
        Assembly openApiAssembly = typeof(OpenApiOptions).Assembly;

        for (int i = builder.Services.Count - 1; i >= 0; i--)
        {
            if (ShouldStripOpenApiDescriptor(builder.Services[i], slugSet, openApiAssembly))
            {
                builder.Services.RemoveAt(i);
            }
        }

        // Doc-gen neutralization: the OpenAPI document is built from endpoint metadata in the request
        // pipeline, never from hosted services or options validation. Strip all three so Host.StartAsync()
        // is a no-op and no module reaches for real infrastructure (DB / Vault / object store / messaging).
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.RemoveAll<IStartupValidator>();
        foreach (ServiceDescriptor validator in builder.Services
            .Where(d => d.ServiceType.IsGenericType
                && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>))
            .ToList())
        {
            builder.Services.Remove(validator);
        }

        WebApplication app = builder.Build();

        // Each module's routes are mounted under an empty-prefix group stamped with the module slug.
        // WithGroupName propagates to nested groups, so shared-helper endpoints (e.g. MapGranitQuery<T>)
        // still land in the owning module's document.
        foreach (OpenApiContractModule module in modules)
        {
            module.Map(app.MapGroup(string.Empty).WithGroupName(module.Slug));
        }

        await app.RunAsync().ConfigureAwait(false);
    }

    // True when a service descriptor belongs to the OpenAPI assembly for a document name
    // that is NOT in the requested slug set — those stale registrations must be stripped so
    // the generator emits exactly the declared documents. Three registration shapes exist.
    private static bool ShouldStripOpenApiDescriptor(
        ServiceDescriptor d, HashSet<string> slugSet, Assembly openApiAssembly)
    {
        // (1) Keyed services from the OpenAPI assembly (OpenApiDocumentService, IOpenApiDocumentProvider,
        //     OpenApiSchemaService, …). These are resolved at generation time — one set per document.
        if (d.IsKeyedService
            && d.ServiceKey is string docKey
            && !slugSet.Contains(docKey)
            && d.ServiceType.Assembly == openApiAssembly)
        {
            return true;
        }

        // (2) Non-keyed NamedService<OpenApiDocumentService> instances (internal, OpenAPI assembly).
        //     OpenApiDocumentProvider.GetDocumentNames() iterates these to build its list; leaving
        //     a "v1" entry here causes GetDocumentNames() to return "v1" even after (1) is stripped,
        //     which then fails in GenerateAsync when the keyed service can't be resolved.
        if (!d.IsKeyedService
            && d.ImplementationInstance is not null
            && d.ServiceType.Assembly == openApiAssembly)
        {
            PropertyInfo? nameProp = d.ImplementationInstance.GetType()
                .GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            if (nameProp?.GetValue(d.ImplementationInstance) is string svcName
                && !slugSet.Contains(svcName))
            {
                return true;
            }
        }

        // (3) IConfigureNamedOptions<OpenApiOptions> for non-slug document names. A stale entry
        //     here is harmless for GetDocumentNames() but keep removal for completeness.
        return !d.IsKeyedService
            && d.ServiceType == typeof(IConfigureOptions<OpenApiOptions>)
            && d.ImplementationInstance is ConfigureNamedOptions<OpenApiOptions> namedOpts
            && namedOpts.Name is not null   // null == ConfigureAll → keep
            && !slugSet.Contains(namedOpts.Name);
    }
}
