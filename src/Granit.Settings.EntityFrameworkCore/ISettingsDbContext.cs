using Granit.Settings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Settings.EntityFrameworkCore;

/// <summary>
/// Interface to implement on the host application's <see cref="DbContext"/>
/// to enable EF Core persistence for Granit settings.
/// </summary>
/// <remarks>
/// <para>
/// The host application's DbContext must implement this interface and call
/// <c>modelBuilder.ConfigureSettingsModule()</c> in <c>OnModelCreating</c>.
/// </para>
/// <example>
/// <code>
/// public class AppDbContext : DbContext, ISettingsDbContext
/// {
///     public DbSet&lt;SettingRecord&gt; SettingRecords =&gt; Set&lt;SettingRecord&gt;();
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         base.OnModelCreating(modelBuilder);
///         modelBuilder.ConfigureSettingsModule();
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public interface ISettingsDbContext
{
    /// <summary>Setting records table.</summary>
    DbSet<SettingRecord> SettingRecords { get; }
}
