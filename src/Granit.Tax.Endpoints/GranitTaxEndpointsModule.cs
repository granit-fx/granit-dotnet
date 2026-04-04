using Granit.Authorization;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Tax.Endpoints;

/// <summary>Granit module for tax administration HTTP endpoints.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitTaxModule),
    typeof(GranitValidationModule))]
public sealed class GranitTaxEndpointsModule : GranitModule;
