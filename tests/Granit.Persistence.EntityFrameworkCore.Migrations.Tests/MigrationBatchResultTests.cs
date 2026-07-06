using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationBatchResultTests
{

    [Fact]
    public void Constructor_NullNextCursor_SignalsCompletion()
    {
        MigrationBatchResult result = new(0, null);

        result.ProcessedCount.ShouldBe(0);
        result.NextCursor.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        MigrationBatchResult result1 = new(10, "cursor");
        MigrationBatchResult result2 = new(10, "cursor");

        result1.ShouldBe(result2);
    }

    [Fact]
    public void Equality_DifferentProcessedCount_AreNotEqual()
    {
        MigrationBatchResult result1 = new(10, "cursor");
        MigrationBatchResult result2 = new(20, "cursor");

        result1.ShouldNotBe(result2);
    }
}
