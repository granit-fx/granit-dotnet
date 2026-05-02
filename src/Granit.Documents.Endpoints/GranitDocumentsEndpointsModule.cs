using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Documents.Endpoints;

/// <summary>
/// Granit module for the Documents HTTP endpoints.
/// </summary>
/// <remarks>
/// Phase 1 surfaces folder hierarchy CRUD with breadcrumb (F2.3); document upload /
/// download / version / share / tag / search / quota endpoints arrive in subsequent
/// stories of the Granit.Documents Epic.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDocumentsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitDocumentsEndpointsModule : GranitModule;
