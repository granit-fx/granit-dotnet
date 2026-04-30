using System.Text.Json;
using System.Text.Json.Nodes;
using Granit.Entities.Views.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Entities.Views.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core configuration for the <see cref="EntityView"/> aggregate. Persists the
/// JSON state and shared-with audience as text columns through value-converters
/// (provider-portable, no JSONB dependency at this layer).
/// </summary>
internal sealed class EntityViewConfiguration : IEntityTypeConfiguration<EntityView>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<EntityView> builder)
    {
        builder.ToTable(
            GranitEntitiesViewsDbProperties.DbTablePrefix + "entity_views",
            GranitEntitiesViewsDbProperties.DbSchema);

        builder.HasKey(v => v.Id);

        builder.Property(v => v.EntityName).HasMaxLength(256).IsRequired();
        builder.Property(v => v.BasedOn).HasMaxLength(128).IsRequired();
        builder.Property(v => v.Kind).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Description).HasMaxLength(2000);
        builder.Property(v => v.Icon).HasMaxLength(64);
        builder.Property(v => v.Visibility).IsRequired();
        builder.Property(v => v.OwnerId);
        builder.Property(v => v.IsPinned).IsRequired();
        builder.Property(v => v.IsDefault).IsRequired();
        builder.Property(v => v.IsPersonalDefault).IsRequired();
        builder.Property(v => v.SortOrder).IsRequired();

        // JSON state — serialised as text. Provider-specific JSON column types can
        // override this in a downstream module (e.g. PostgreSQL `jsonb`).
        builder.Property(v => v.State)
            .HasConversion(
                state => Serialize(state),
                json => Deserialize(json) ?? new JsonObject(),
                new ValueComparer<JsonObject>(
                    (left, right) => Serialize(left) == Serialize(right),
                    state => state.ToJsonString().GetHashCode(StringComparison.Ordinal),
                    state => state.DeepClone().AsObject()))
            .HasMaxLength(32_000)
            .IsRequired();

        builder.Property(v => v.SharedWith)
            .HasConversion(
                audience => SerializeAudience(audience),
                json => DeserializeAudience(json),
                new ValueComparer<EntityViewSharedWith?>(
                    (left, right) => SerializeAudience(left) == SerializeAudience(right),
                    audience => audience == null ? 0 : audience.Roles.Count + (audience.Users.Count * 31),
                    audience => audience))
            .HasMaxLength(8_000);

        // Index pairs (TenantId, EntityName) for the list query, plus (OwnerId) for
        // personal-views look-up by user.
        builder.HasIndex(v => new { v.TenantId, v.EntityName });
        builder.HasIndex(v => v.OwnerId);
    }

    private static string Serialize(JsonObject? state) =>
        state is null ? "{}" : state.ToJsonString(SerializerOptions);

    private static JsonObject? Deserialize(string json) =>
        string.IsNullOrEmpty(json)
            ? new JsonObject()
            : JsonNode.Parse(json)?.AsObject();

    private static string? SerializeAudience(EntityViewSharedWith? audience) =>
        audience is null ? null : JsonSerializer.Serialize(audience, SerializerOptions);

    private static EntityViewSharedWith? DeserializeAudience(string? json) =>
        string.IsNullOrEmpty(json)
            ? null
            : JsonSerializer.Deserialize<EntityViewSharedWith>(json, SerializerOptions);
}
