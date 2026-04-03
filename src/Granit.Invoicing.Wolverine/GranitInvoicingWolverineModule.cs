using Granit.Modularity;
using Granit.Wolverine;

namespace Granit.Invoicing.Wolverine;

/// <summary>Wolverine integration for Granit.Invoicing.</summary>
[DependsOn(
    typeof(GranitInvoicingModule),
    typeof(GranitWolverineModule))]
public sealed class GranitInvoicingWolverineModule : GranitModule;
