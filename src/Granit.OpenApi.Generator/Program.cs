using Granit.Authentication.ApiKeys.Endpoints.Extensions;
using Granit.BlobStorage.Endpoints.Extensions;
using Granit.Identity.Endpoints.Extensions;
using Granit.OpenApi.Generation;
using Granit.OpenApi.Generator;
using Granit.Workflow.Endpoints.Extensions;

// All doc-gen orchestration (per-module document slicing, infra neutralization, the minimal-API
// binding probe) lives in the shared Granit.OpenApi.Generation helper, so this entry point only
// declares what is specific to the framework's own contract: the module graph (GeneratorModule),
// the slug-to-routes pairing (GeneratorEndpoints.All), and the four modules that expose an explicit
// service-registration method rather than registering purely via [DependsOn].
await OpenApiContractGenerator.RunAsync<GeneratorModule>(
    args,
    GeneratorEndpoints.All,
    builder =>
    {
        builder.AddGranitBlobStorageEndpoints();
        builder.Services.AddGranitApiKeysEndpoints();
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddGranitWorkflowEndpoints();
    });
