using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

// ADR-070: EF complex-type columns cannot be optional, so a nullable [QueryableValueObject] column
// is rejected at model build (rather than failing obscurely on a null INSERT — verified behaviour).
public sealed class QueryableValueObjectNullableGuardTests
{
    [Fact]
    public void Nullable_queryable_value_object_column_is_rejected_at_model_build()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        conn.Open();
        DbContextOptions<NullCtx> opts = new DbContextOptionsBuilder<NullCtx>().UseSqlite(conn).Options;

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
        {
            using NullCtx ctx = new(opts);
            _ = ctx.Model; // forces OnModelCreating + ApplyGranitConventions
        });

        ex.Message.ShouldContain("nullable");
        ex.Message.ShouldContain("ADR-070");
    }

    private sealed class MaybeSlug : SingleValueObject<string>
    {
        public override required string Value { get; init; }
        public static MaybeSlug Create(string v) => new() { Value = v };
    }

    private sealed class Thing
    {
        public Guid Id { get; set; }

        [QueryableValueObject]
        public MaybeSlug? Maybe { get; set; }
    }

    private sealed class NullCtx(DbContextOptions<NullCtx> options) : DbContext(options)
    {
        public DbSet<Thing> Things => Set<Thing>();
        protected override void OnModelCreating(ModelBuilder b) => b.ApplyGranitConventions();
    }
}
