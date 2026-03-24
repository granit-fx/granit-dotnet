namespace Granit.Domain;

/// <summary>
/// Interface for entities that have translatable properties stored in a separate
/// translation table. The translations are accessed via the <see cref="Translations"/>
/// navigation property.
/// </summary>
/// <remarks>
/// <para>Two resolution modes exist on the same data model:</para>
/// <list type="bullet">
///   <item>
///     <term>Translatable (fallback)</term>
///     <description>
///       Returns the best match for a culture using the chain:
///       exact culture → parent culture → default culture → first available.
///       Use case: document title, product name.
///     </description>
///   </item>
///   <item>
///     <term>Culture-variant (strict)</term>
///     <description>
///       Returns <c>null</c> if the exact culture does not exist.
///       Use case: email template, legal notice.
///     </description>
///   </item>
/// </list>
/// <para>
/// The framework provides the <b>mechanism</b> (storage + resolution);
/// the application decides the <b>policy</b> (fallback or strict) via the
/// <c>useFallback</c> parameter on <see cref="TranslatableExtensions.GetTranslation{TTranslation}"/>.
/// </para>
/// </remarks>
/// <typeparam name="TTranslation">The translation entity type.</typeparam>
public interface ITranslatable<TTranslation> where TTranslation : class, ITranslation
{
    /// <summary>Collection of translations for this entity.</summary>
    ICollection<TTranslation> Translations { get; }
}
