using Granit.Authentication.ApiKeys.Endpoints.Extensions;
using Granit.BlobStorage.Endpoints.Extensions;
using Granit.Geocoding;
using Granit.Identity.Endpoints.Extensions;
using Granit.OpenApi.Generation;
using Granit.OpenApi.Generator;
using Granit.Workflow.Endpoints.Extensions;
using Granit.Workflow.Extensions;

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

        // Geocoding endpoints are capability-gated on the registered provider set, so a provider-less graph
        // emits an empty geocoding contract. Register a no-op provider covering all three capabilities so the
        // generated document includes the geocoding paths without pulling in a real (billable, config-bound)
        // provider package.
        builder.Services.AddSingleton<GeneratorStubGeocodingProvider>();
        builder.Services.AddSingleton<IGeocodingProvider>(
            static sp => sp.GetRequiredService<GeneratorStubGeocodingProvider>());
        builder.Services.AddSingleton<IReverseGeocodingProvider>(
            static sp => sp.GetRequiredService<GeneratorStubGeocodingProvider>());
        builder.Services.AddSingleton<IAddressAutocompleteProvider>(
            static sp => sp.GetRequiredService<GeneratorStubGeocodingProvider>());

        // Same rationale for workflow: the two /transitions routes are mapped by an extension generic
        // over the host's state enum and backed by IWorkflowManager<TState>, so a workflow-less graph
        // emits the history route alone. Register a placeholder state machine so the generated document
        // covers the full workflow surface (see GeneratorStubWorkflowDefinition).
        builder.Services.AddWorkflow<WorkflowState>(new GeneratorStubWorkflowDefinition());
    });
