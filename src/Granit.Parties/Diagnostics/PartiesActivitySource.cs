using System.Diagnostics;

namespace Granit.Parties.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Parties distributed tracing.
/// </summary>
internal static class PartiesActivitySource
{
    internal const string Name = "Granit.Parties";

    internal static readonly ActivitySource Source = new(Name);

    internal const string CreateParty = "parties.create";
    internal const string UpdateParty = "parties.update";
    internal const string SuspendParty = "parties.suspend";
    internal const string ActivateParty = "parties.activate";
    internal const string ArchiveParty = "parties.archive";
    internal const string PseudonymizeParty = "parties.pseudonymize";
    internal const string AddExternalMapping = "parties.external_mapping.add";
    internal const string SetTaxStatus = "parties.tax_status.set";
    internal const string ResolveDefault = "parties.default.resolve";
    internal const string SeedDefault = "parties.default.seed";
}
