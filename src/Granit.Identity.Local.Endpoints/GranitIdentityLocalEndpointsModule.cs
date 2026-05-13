using Granit.Authorization;
using Granit.Caching;
using Granit.Http.ApiDocumentation;
using Granit.Identity.Local;
using Granit.Identity.Local.Endpoints.Endpoints;
using Granit.Identity.Local.Endpoints.Workspaces;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Local.Endpoints;

/// <summary>
/// Module for local identity account self-service endpoints (<c>/api/account</c>)
/// and admin impersonation (<c>/api/admin</c>).
/// </summary>
/// <remarks>
/// <para>Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.</para>
/// <para>Validators are auto-discovered by <c>GranitValidationModule</c>.</para>
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitCachingModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitIdentityLocalModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitIdentityLocalEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<IdentityLocalConfigProvider>();
        context.Services.AddWorkspaceContribution<IdentityLocalWorkspaceContribution>();
    }
}
