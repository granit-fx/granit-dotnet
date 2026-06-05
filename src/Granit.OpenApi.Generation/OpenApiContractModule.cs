using Microsoft.AspNetCore.Routing;

namespace Granit.OpenApi.Generation;

/// <summary>One generated OpenAPI document: a module slug and the routes mounted under it.</summary>
/// <param name="Slug">Document name and <c>GroupName</c> stamped on the module's routes.</param>
/// <param name="Map">Mounts the module's <c>MapGranit*</c> endpoints onto the supplied builder.</param>
public sealed record OpenApiContractModule(string Slug, Action<IEndpointRouteBuilder> Map);
