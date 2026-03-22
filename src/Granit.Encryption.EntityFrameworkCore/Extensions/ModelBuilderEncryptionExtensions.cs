using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Granit.Encryption.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for Granit field-level encryption conventions.
/// </summary>
public static class ModelBuilderEncryptionExtensions
{
    /// <summary>
    /// Annotation key applied to properties with <c>[Encrypted(KeyIsolation = true)]</c>.
    /// Used by interceptors to quickly identify isolated properties without re-reflecting.
    /// </summary>
    internal const string IsolatedAnnotation = "Granit:EncryptionIsolated";

    /// <summary>
    /// Scans all entity types registered in the model and applies
    /// <see cref="EncryptedStringConverter"/> to every <c>string</c> property
    /// annotated with <see cref="EncryptedAttribute"/>.
    /// <para>
    /// Properties with <see cref="EncryptedAttribute.KeyIsolation"/> set to <c>true</c>
    /// are skipped (no converter applied) — they are handled by
    /// <c>EncryptionIsolationSaveChangesInterceptor</c> and
    /// <c>EncryptionIsolationMaterializationInterceptor</c> instead.
    /// </para>
    /// <para>
    /// Call this method at the end of <c>OnModelCreating</c>, after
    /// <c>modelBuilder.ApplyGranitConventions()</c>:
    /// </para>
    /// <code>
    /// protected override void OnModelCreating(ModelBuilder modelBuilder)
    /// {
    ///     modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    ///     modelBuilder.ApplyEncryptionConventions(_encryptionService);
    /// }
    /// </code>
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="encryptionService">
    /// The <see cref="IStringEncryptionService"/> instance injected into the
    /// <c>DbContext</c> — captured in the converter closure.
    /// </param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplyEncryptionConventions(
        this ModelBuilder modelBuilder,
        IStringEncryptionService encryptionService)
    {
        EncryptedStringConverter converter = new(encryptionService);

        foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableProperty property in entityType.GetProperties())
            {
                if (property.ClrType != typeof(string))
                {
                    continue;
                }

                EncryptedAttribute? attr = property.PropertyInfo?.GetCustomAttribute<EncryptedAttribute>();
                if (attr is null)
                {
                    continue;
                }

                if (attr.KeyIsolation)
                {
                    // Per-entity key isolation: handled by interceptors, not converter.
                    // Annotate so interceptors can identify these properties from metadata.
                    property.SetAnnotation(IsolatedAnnotation, true);
                    continue;
                }

                property.SetValueConverter(converter);
            }
        }

        return modelBuilder;
    }
}
