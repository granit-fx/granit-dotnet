namespace Granit.Entities.Forms;

/// <summary>
/// Sub-descriptor that turns a <see cref="SectionDescriptor"/> into an owned-collection
/// section — the section renders as a list of items (one entry per element of the owner's
/// collection navigation), each item built from its own <see cref="ItemFields"/> schema.
/// </summary>
/// <remarks>
/// Per ADR-040 the renderer treats a section with <c>OwnedCollection != null</c> as a
/// list-of-cards layout: each item gets the form schema described by <see cref="ItemFields"/>,
/// optionally headlined by the item's <see cref="ItemDisplayProperty"/>. The owner's
/// scalar <see cref="SectionDescriptor.Fields"/> list MUST be empty in this case — the
/// builder enforces mutual exclusion.
/// </remarks>
public sealed record OwnedCollectionDescriptor
{
    /// <summary>Property name on the owning entity that exposes the collection (e.g. <c>"Addresses"</c>).</summary>
    public required string PropertyName { get; init; }

    /// <summary>CLR type of the items in the collection (e.g. <c>typeof(PartyAddress)</c>).</summary>
    public required Type ItemType { get; init; }

    /// <summary>Per-item form schema, in declaration order.</summary>
    public required IReadOnlyList<FieldDescriptor> ItemFields { get; init; }

    /// <summary>
    /// Optional property on the item used as the collapsed-row headline (e.g. <c>"Line1"</c>
    /// for a <c>PartyAddress</c>). Resolved client-side; the field itself must also appear
    /// in <see cref="ItemFields"/> for the value to be in the payload.
    /// </summary>
    public string? ItemDisplayProperty { get; init; }

    /// <summary>Optional renderer hint capping how many items the renderer expands inline before paging.</summary>
    public int? MaxRendered { get; init; }
}
