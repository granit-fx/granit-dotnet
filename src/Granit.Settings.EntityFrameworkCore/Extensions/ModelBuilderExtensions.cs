using Granit.Settings.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Settings.EntityFrameworkCore.Extensions;

/// <summary>
/// EF Core <see cref="ModelBuilder"/> extensions for the Settings module.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the <c>settings_setting_records</c> table configuration to the model.
    /// </summary>
    /// <remarks>
    /// Call this method in <c>OnModelCreating</c> of the host application's DbContext
    /// that implements <see cref="Internal.ISettingsDbContext"/>.
    /// </remarks>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ConfigureSettingsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SettingRecordConfiguration());
        return modelBuilder;
    }
}
