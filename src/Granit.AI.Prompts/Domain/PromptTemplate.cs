using Granit.Domain;
using Granit.Domain.ValueObjects;

namespace Granit.AI.Prompts.Domain;

/// <summary>
/// A reusable prompt in the user-facing catalogue behind the <c>/</c> picker (ADR-067) — distinct
/// from the code-first guardrail prompts owned by <c>Granit.AI.Tools</c>. Multi-tenant and owned by
/// a user; framework-seeded generic prompts are flagged <see cref="IsSystem"/> and owned by no user.
/// Carries display decoration (icon + colour) and a <see cref="Version"/> bumped on each edit.
/// </summary>
public sealed class PromptTemplate : FullAuditedAggregateRoot, IMultiTenant, IOwnable
{
    private PromptTemplate()
    {
    }

    /// <summary>Creates a user-owned prompt (version 1, not a system prompt).</summary>
    public static PromptTemplate Create(
        Guid id,
        Guid ownerId,
        string name,
        string shortDescription,
        string content,
        string? icon = null,
        HexColor? iconColor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new PromptTemplate
        {
            Id = id,
            OwnerId = ownerId,
            Name = name,
            ShortDescription = shortDescription,
            Content = content,
            Icon = icon,
            IconColor = iconColor,
            Version = 1,
            IsSystem = false,
        };
    }

    /// <summary>
    /// Creates a framework-seeded system prompt: <see cref="IsSystem"/> is <see langword="true"/>
    /// and it is owned by no user (<see cref="OwnerId"/> is <see cref="Guid.Empty"/>).
    /// </summary>
    public static PromptTemplate CreateSystem(
        Guid id,
        string name,
        string shortDescription,
        string content,
        string? icon = null,
        HexColor? iconColor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new PromptTemplate
        {
            Id = id,
            OwnerId = Guid.Empty,
            Name = name,
            ShortDescription = shortDescription,
            Content = content,
            Icon = icon,
            IconColor = iconColor,
            Version = 1,
            IsSystem = true,
        };
    }

    /// <summary>Display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>A one-line description for the catalogue.</summary>
    public string ShortDescription { get; private set; } = string.Empty;

    /// <summary>The prompt instruction text.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>An icon identifier for the catalogue, or <see langword="null"/>.</summary>
    public string? Icon { get; private set; }

    /// <summary>
    /// The icon's colour as a hex code with a leading <c>#</c> — <c>#RRGGBB</c> (7 chars) or
    /// <c>#RRGGBBAA</c> (9, with alpha) — or <see langword="null"/>. Format is validated at the endpoint.
    /// </summary>
    public HexColor? IconColor { get; private set; }

    /// <summary>The revision, starting at 1 and bumped on each <see cref="Edit"/>.</summary>
    public int Version { get; private set; }

    /// <summary>Whether this is a framework-seeded prompt (read-only, owned by no user).</summary>
    public bool IsSystem { get; private set; }

    /// <summary>The user who owns this prompt; <see cref="Guid.Empty"/> for a system prompt.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Owning tenant; stamped by the interceptor.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>The category links (many-to-many) for this prompt. A prompt may sit in several categories.</summary>
    public List<PromptTemplateCategory> CategoryLinks { get; private set; } = [];

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Assigns the prompt to a category (idempotent — a repeated assignment is ignored).</summary>
    public void AssignCategory(Guid linkId, Guid categoryId)
    {
        if (!CategoryLinks.Any(l => l.CategoryId == categoryId))
        {
            CategoryLinks.Add(PromptTemplateCategory.Create(linkId, Id, categoryId));
        }
    }

    /// <summary>Removes all category assignments.</summary>
    public void ClearCategories() => CategoryLinks.Clear();

    /// <summary>Updates the editable fields and bumps the <see cref="Version"/>.</summary>
    public void Edit(string name, string shortDescription, string content, string? icon, HexColor? iconColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Name = name;
        ShortDescription = shortDescription;
        Content = content;
        Icon = icon;
        IconColor = iconColor;
        Version++;
    }
}
