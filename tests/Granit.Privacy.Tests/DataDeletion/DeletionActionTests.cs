using Granit.Privacy.DataDeletion;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataDeletion;

public sealed class DeletionActionTests
{
    [Fact]
    public void PhysicalDelete_HasValue_Zero() => ((int)DeletionAction.PhysicalDelete).ShouldBe(0);

    [Fact]
    public void SoftDelete_HasValue_One() => ((int)DeletionAction.SoftDelete).ShouldBe(1);

    [Fact]
    public void Anonymized_HasValue_Two() => ((int)DeletionAction.Anonymized).ShouldBe(2);

    [Fact]
    public void Retained_HasValue_Three() => ((int)DeletionAction.Retained).ShouldBe(3);

    [Fact]
    public void Mixed_HasValue_Four() => ((int)DeletionAction.Mixed).ShouldBe(4);

    [Fact]
    public void CryptoShredding_HasValue_Five() => ((int)DeletionAction.CryptoShredding).ShouldBe(5);

    [Fact]
    public void AllValues_AreDefined()
    {
        DeletionAction[] values = Enum.GetValues<DeletionAction>();

        values.Length.ShouldBe(6);
    }
}
