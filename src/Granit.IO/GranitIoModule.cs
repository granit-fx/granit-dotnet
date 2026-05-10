using Granit.IO.Extensions;
using Granit.Modularity;
using Granit.MultiTenancy;

namespace Granit.IO;

/// <summary>
/// Granit module for the <c>Granit.IO</c> infrastructure package.
/// </summary>
/// <remarks>
/// <para>
/// Registers <see cref="ITempFileFactory"/> and the temp-file janitor:
/// </para>
/// <list type="bullet">
///   <item>POSIX <c>0600</c> on files and <c>0700</c> on directories (Linux/macOS)</item>
///   <item>NTFS ACL restricted to the current user (Windows)</item>
///   <item><see cref="FileOptions.DeleteOnClose"/> for auto-cleanup on stream disposal</item>
///   <item>Tenant-partitioned directories (<c>t-{tenantId}/{category}</c>)</item>
///   <item>Size cap via <c>LimitedStream</c> (256 MiB default)</item>
///   <item>Background janitor purging files older than <c>MaxLifetime</c> (1 h default)</item>
/// </list>
/// <para>
/// Standards: OWASP ASVS V12.4.1, ISO 27001 A.5.34, GDPR Art. 32.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitMultiTenancyModule))]
public sealed class GranitIoModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddGranitTempFiles();
    }
}
