using System.Linq.Expressions;
using System.Reflection;
using Granit.Domain.ValueObjects;

namespace Granit.Entities.Layouts;

/// <summary>
/// Fluent builder for a gallery layout declaration. Mirrors the calendar /
/// kanban builder shape: typed property selectors, opt-ins for
/// <see cref="EntityListLayoutDescriptor.IsDefault"/> and
/// <see cref="EntityListLayoutDescriptor.RequiresPermission"/>, and a
/// <c>Build()</c> step that surfaces missing-required-config as an
/// <see cref="InvalidOperationException"/> at host startup.
/// </summary>
/// <typeparam name="TEntity">The entity rendered in the gallery grid.</typeparam>
public sealed class GalleryLayoutBuilder<TEntity>
{
    private string? _imagePropertyName;
    private string? _titlePropertyName;
    private string? _subtitlePropertyName;
    private GalleryCardSize _cardSize = GalleryCardSize.Medium;
    private bool _isDefault;
    private string? _requiresPermission;

    internal GalleryLayoutBuilder() { }

    /// <summary>
    /// Names the property carrying the card image. The selector type is fixed to
    /// <see cref="BlobReference"/>?: the renderer resolves the value to a
    /// pre-signed URL via the host's blob-storage download endpoint, so a bare
    /// <see cref="string"/> path or a typed image record would silently bypass
    /// the security gate. Required.
    /// </summary>
    public GalleryLayoutBuilder<TEntity> ImageField(Expression<Func<TEntity, BlobReference?>> propertySelector)
    {
        _imagePropertyName = ReadPropertyName(propertySelector, "ImageField");
        return this;
    }

    /// <summary>Names the property used as the card headline. Falls back to the entity's <c>DisplayProperty</c>.</summary>
    public GalleryLayoutBuilder<TEntity> TitleField(Expression<Func<TEntity, string>> propertySelector)
    {
        _titlePropertyName = ReadPropertyName(propertySelector, "TitleField");
        return this;
    }

    /// <summary>Names the optional secondary line shown under the title (e.g. category, tag, status).</summary>
    public GalleryLayoutBuilder<TEntity> SubtitleField(Expression<Func<TEntity, string>> propertySelector)
    {
        _subtitlePropertyName = ReadPropertyName(propertySelector, "SubtitleField");
        return this;
    }

    /// <summary>Picks the card size (defaults to <see cref="GalleryCardSize.Medium"/>).</summary>
    public GalleryLayoutBuilder<TEntity> CardSize(GalleryCardSize size)
    {
        _cardSize = size;
        return this;
    }

    /// <summary>
    /// Marks this layout as the one the renderer picks on first load. At most
    /// one layout per entity may opt in.
    /// </summary>
    public GalleryLayoutBuilder<TEntity> IsDefault()
    {
        _isDefault = true;
        return this;
    }

    /// <summary>
    /// Drops the layout from the manifest payload entirely when the user lacks
    /// this permission — defense in depth, never just hidden.
    /// </summary>
    public GalleryLayoutBuilder<TEntity> RequiresPermission(string permissionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        _requiresPermission = permissionName;
        return this;
    }

    /// <summary>Materialises the descriptor; called by <c>EntityDefinitionBuilder&lt;T&gt;.GalleryView</c>.</summary>
    /// <exception cref="InvalidOperationException">When <see cref="ImageField"/> was not set.</exception>
    internal GalleryLayoutDescriptor Build()
    {
        if (_imagePropertyName is null)
        {
            throw new InvalidOperationException(
                $"Gallery layout for '{typeof(TEntity).FullName}' is missing a required ImageField — call .ImageField(p => p.SomeBlobReference) on the builder.");
        }

        return new GalleryLayoutDescriptor
        {
            Kind = EntityListLayoutKind.Gallery,
            IsDefault = _isDefault,
            RequiresPermission = _requiresPermission,
            ImagePropertyName = _imagePropertyName,
            TitlePropertyName = _titlePropertyName,
            SubtitlePropertyName = _subtitlePropertyName,
            CardSize = _cardSize,
        };
    }

    private static string ReadPropertyName<TValue>(
        Expression<Func<TEntity, TValue>> selector,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression body = selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary
            ? unary.Operand
            : selector.Body;

        if (body is not MemberExpression member || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                $"{parameterName} selector must be a direct property access expression (e.g. p => p.Avatar).",
                nameof(selector));
        }

        return property.Name;
    }
}
