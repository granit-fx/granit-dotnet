using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Validation;

namespace Granit.ReferenceData.Endpoints;

/// <summary>
/// Granit module for reference data Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This module does not auto-map routes. The host application must call
/// <c>app.MapReferenceDataEndpoints&lt;TEntity&gt;()</c> for each entity type.
/// </para>
/// <para>Validators are auto-discovered by <c>GranitValidationModule</c>.</para>
/// <para>Permissions are auto-discovered by <c>GranitAuthorizationModule</c>.</para>
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitGuidsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitReferenceDataModule),
    typeof(GranitValidationModule))]
public sealed class GranitReferenceDataEndpointsModule : GranitModule;

