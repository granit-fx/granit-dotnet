using Granit.Persistence.EntityFrameworkCore;
using Granit.Timeline.Domain;
using Granit.Timeline.EntityFrameworkCore;
using Granit.Timeline.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

public sealed class ReactionConfigurationTests
{
    private static IEntityType GetEntityType()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();
        DbContextOptions<TimelineDbContext> options = new DbContextOptionsBuilder<TimelineDbContext>()
            .UseSqlite(connection)
            .Options;
        using TimelineDbContext ctx = new(options, GranitDesignTime.CurrentTenant);
        return ctx.Model.FindEntityType(typeof(Reaction))
            ?? throw new InvalidOperationException("Reaction entity type missing from model");
    }

    [Fact]
    public void Table_uses_configured_prefix()
    {
        IEntityType type = GetEntityType();
        type.GetTableName().ShouldBe(GranitTimelineDbProperties.DbTablePrefix + "reactions");
    }

    [Fact]
    public void Emoji_property_has_max_length_32()
    {
        IProperty prop = GetEntityType().FindProperty(nameof(Reaction.Emoji))!;
        prop.GetMaxLength().ShouldBe(32);
        prop.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Unique_index_covers_entry_user_emoji()
    {
        IIndex unique = GetEntityType().GetIndexes().Single(i => i.IsUnique);
        unique.Properties.Select(p => p.Name).ShouldBe(["EntryId", "UserId", "Emoji"]);
        unique.GetDatabaseName().ShouldBe($"ix_{GranitTimelineDbProperties.DbTablePrefix}reactions_unique");
    }

    [Fact]
    public void By_entry_index_present_for_batch_reads()
    {
        IIndex byEntry = GetEntityType().GetIndexes()
            .Single(i => i.GetDatabaseName() == $"ix_{GranitTimelineDbProperties.DbTablePrefix}reactions_by_entry");
        byEntry.Properties.Select(p => p.Name).ShouldBe(["EntryId", "TenantId"]);
        byEntry.IsUnique.ShouldBeFalse();
    }

    [Fact]
    public void Foreign_key_to_TimelineEntry_cascade_deletes()
    {
        IEntityType type = GetEntityType();
        IForeignKey fk = type.GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(TimelineEntry));
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }
}
