using Granit.Authorization;
using Granit.Documents.Endpoints.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Taxonomy.Authorization;
using Granit.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.Endpoints;

/// <summary>
/// Granit module for the Documents HTTP endpoints.
/// </summary>
/// <remarks>
/// Phase 1 surfaces folder hierarchy CRUD with breadcrumb (F2.3) and the document
/// upload/version flow. T6.1 adds tag proxy endpoints under
/// <c>/api/v1/documents/{id}/tags</c> that delegate to <c>Granit.Taxonomy</c>,
/// and replaces the framework-default <see cref="ITaggablePermissionResolver"/>
/// with one that requires <c>Documents.Documents.Manage</c> for tag-assignment
/// writes against a <c>Document</c> target.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDocumentsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitDocumentsEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        context.Services.Replace(ServiceDescriptor.Singleton<
            ITaggablePermissionResolver,
            DocumentTaggablePermissionResolver>());
    }
}
