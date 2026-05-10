using System.Net;

namespace Granit.Http.Security.Internal;

/// <summary>
/// Testable abstraction over <see cref="Dns.GetHostAddressesAsync(string, System.Threading.CancellationToken)"/>.
/// Lets unit tests simulate DNS rebinding by returning a different IP set on each call.
/// </summary>
internal interface IDnsResolver
{
    ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct);
}
