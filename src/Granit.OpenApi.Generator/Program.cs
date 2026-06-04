using Granit.Authentication.ApiKeys.Endpoints.Extensions;
using Granit.BlobStorage.Endpoints.Extensions;
using Granit.Extensions;
using Granit.Http.ApiDocumentation.Extensions;
using Granit.Identity.Endpoints.Extensions;
using Granit.OpenApi.Generator;
using Granit.Workflow.Endpoints.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Cheap, infra-free ASP.NET services the Granit OpenAPI transformers assume a host wires
// (e.g. JwtBearerSecuritySchemeTransformer constructor-injects IAuthenticationSchemeProvider).
builder.Services.AddAuthentication();
builder.Services.AddAuthorizationBuilder();

// The four endpoint modules that expose a host-builder/service registration method.
builder.AddGranitBlobStorageEndpoints();
builder.Services.AddGranitApiKeysEndpoints();
builder.Services.AddGranitIdentityEndpoints();
builder.Services.AddGranitWorkflowEndpoints();

// One OpenAPI document per module, sliced by the GroupName stamped on its routes below, each
// carrying the full Granit transformer chain so the artifacts match the framework's served output
// (int32 normalized, int64 string-union kept, problem-details enriched, tags sorted).
foreach (EndpointModule module in GeneratorEndpoints.All)
{
    string slug = module.Slug;
    builder.AddGranitOpenApiDocument(slug, slug, description => description.GroupName == slug);
}

await builder.AddGranitAsync<GeneratorModule>();

// Doc-gen neutralization: the OpenAPI document is built from endpoint metadata in the request
// pipeline, never from hosted services or options validation. Strip all three so Host.StartAsync()
// is a no-op and no module reaches for real infrastructure (DB / Vault / object store / messaging).
// This keeps the generator config-free and independent of how many modules are composed.
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
// WithGroupName propagates to nested groups, so shared-helper endpoints (e.g. MapGranitQuery<T>,
// whose handler lives in Granit.QueryEngine.AspNetCore) still land in the owning module's document.
foreach (EndpointModule module in GeneratorEndpoints.All)
{
    module.Map(app.MapGroup(string.Empty).WithGroupName(module.Slug));
}

await app.RunAsync();
