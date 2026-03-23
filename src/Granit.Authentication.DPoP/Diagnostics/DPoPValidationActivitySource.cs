using System.Diagnostics;

namespace Granit.Authentication.DPoP.Diagnostics;

/// <summary>
/// ActivitySource for DPoP proof validation operations.
/// </summary>
internal static class DPoPValidationActivitySource
{
    internal const string Name = "Granit.Authentication.DPoP";
    internal static readonly ActivitySource Source = new(Name);

    internal const string Validate = "dpop.validate";
}
