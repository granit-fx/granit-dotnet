using Granit.Authorization;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.ReferenceData.Endpoints;

/// <summary>
/// Granit module for reference data Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This module does not auto-map routes. The host application must call
/// <c>app.MapGranitReferenceData&lt;TEntity&gt;()</c> for each entity type.
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

