using Granit.Activities.Domain;
using Granit.Activities.EntityFrameworkCore;
using Granit.Activities.EntityFrameworkCore.Internal;
using Granit.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Activities.EntityFrameworkCore.Tests.Configurations;

public sealed class ActivityConfigurationTests
{
    private static IEntityType GetActivityEntityType()
    {
        DbContextOptions<ActivitiesDbContext> options = new DbContextOptionsBuilder<ActivitiesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using ActivitiesDbContext ctx = new(options, new PassthroughEncryption());
        return ctx.Model.FindEntityType(typeof(Activity))
            ?? throw new InvalidOperationException("Activity entity type missing from model");
    }

    private sealed class PassthroughEncryption : IStringEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string? Decrypt(string cipherText) => cipherText;
    }

    [Fact]
    public void Activity_table_uses_configured_prefix()
    {
        IEntityType type = GetActivityEntityType();
        type.GetTableName().ShouldBe(GranitActivitiesDbProperties.DbTablePrefix + "activities");
    }

    [Fact]
    public void EntityType_property_has_max_length_256()
    {
        IProperty prop = GetActivityEntityType().FindProperty(nameof(Activity.EntityType))!;
        prop.GetMaxLength().ShouldBe(256);
        prop.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Type_property_has_max_length_64()
    {
        IProperty prop = GetActivityEntityType().FindProperty(nameof(Activity.Type))!;
        prop.GetMaxLength().ShouldBe(64);
        prop.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Description_property_has_max_length_2000_and_nullable()
    {
        IProperty prop = GetActivityEntityType().FindProperty(nameof(Activity.Description))!;
        prop.GetMaxLength().ShouldBe(2_000);
        prop.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void OverdueNotifiedAt_is_nullable()
    {
        IProperty prop = GetActivityEntityType().FindProperty(nameof(Activity.OverdueNotifiedAt))!;
        prop.IsNullable.ShouldBeTrue();
    }

    [Theory]
    [InlineData("entity")]
    [InlineData("inbox")]
    [InlineData("overdue_scan")]
    public void Indexes_for_three_query_paths_are_present(string suffix)
    {
        IEntityType type = GetActivityEntityType();
        string expectedName = $"ix_{GranitActivitiesDbProperties.DbTablePrefix}activities_{suffix}";
        type.GetIndexes().Any(i => i.GetDatabaseName() == expectedName).ShouldBeTrue(
            $"index '{expectedName}' missing from Activity model");
    }
}
