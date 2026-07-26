using System.Diagnostics;

namespace Granit.Authentication.Mtls.Diagnostics;

/// <summary>
/// ActivitySource for mutual-TLS certificate-bound token validation operations.
/// </summary>
internal static class MtlsValidationActivitySource
{
    internal const string Name = "Granit.Authentication.Mtls";
    internal static readonly ActivitySource Source = new(Name);

    internal const string Validate = "mtls.validate";
}
