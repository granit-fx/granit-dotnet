namespace Granit.IpGeolocation.MaxMind.Options;

/// <summary>
/// How the MaxMind <c>.mmdb</c> database file is opened.
/// </summary>
public enum MaxMindFileAccess
{
    /// <summary>
    /// Load the entire database into managed memory at open time. Holds no OS file handle afterwards, so a
    /// scheduled database update can replace the file without an "in use" error. Recommended (the default).
    /// </summary>
    Memory,

    /// <summary>
    /// Memory-map the file. Lower managed-memory footprint for very large databases, but keeps an open handle
    /// that can block an in-place file replacement — pair with an atomic rename and
    /// <see cref="MaxMindIpGeolocationOptions.ReloadOnChange"/>.
    /// </summary>
    MemoryMapped,
}
