using System.Linq.Expressions;
using Granit.DataExchange.Import.Mapping;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Mapping;

public sealed class ImportDefinitionBuilderEdgeCaseTests
{
    private sealed class Order
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public ICollection<OrderLine> Lines { get; set; } = [];
    }

    private sealed class OrderLine
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    [Fact]
    public void HasCompositeKey_AddsBothKeyProperties()
    {
        ImportDefinitionBuilder<Order> builder = new();

        builder.HasCompositeKey(
            o => o.OrderNumber,
            o => o.CustomerName);

        builder.BusinessKeyProperties.Count.ShouldBe(2);
        builder.BusinessKeyProperties.ShouldContain("OrderNumber");
        builder.BusinessKeyProperties.ShouldContain("CustomerName");
    }

    [Fact]
    public void HasExternalId_SetsFlag()
    {
        ImportDefinitionBuilder<Order> builder = new();

        builder.HasExternalId();

        builder.HasExternalIdFlag.ShouldBeTrue();
    }

    [Fact]
    public void GroupBy_SetsGroupByColumn()
    {
        ImportDefinitionBuilder<Order> builder = new();

        builder.GroupBy("OrderNumber");

        builder.GroupByColumn.ShouldBe("OrderNumber");
    }

    [Fact]
    public void HasMany_AddsChildCollectionProperties()
    {
        ImportDefinitionBuilder<Order> builder = new();

        builder.HasMany<OrderLine>(
            o => o.Lines,
            child =>
            {
                child.Property(l => l.ProductName, p => p.DisplayName("Product"));
                child.Property(l => l.Quantity, p => p.Required());
            });

        builder.Properties.Count.ShouldBe(2);
        builder.Properties[0].PropertyPath.ShouldBe("Lines.ProductName");
        builder.Properties[0].IsChildCollection.ShouldBeTrue();
        builder.Properties[0].DisplayName.ShouldBe("Product");
        builder.Properties[1].PropertyPath.ShouldBe("Lines.Quantity");
        builder.Properties[1].IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Property_WithFormat_SetsFormat()
    {
        ImportDefinitionBuilder<Order> builder = new();

        builder.Property(o => o.OrderNumber, p => p.Format("A-{0}"));

        builder.Properties[0].Format.ShouldBe("A-{0}");
    }

    [Fact]
    public void Property_WithDescription_SetsDescription()
    {
        ImportDefinitionBuilder<Order> builder = new();

        builder.Property(o => o.OrderNumber, p => p.Description("The order reference number"));

        builder.Properties[0].Description.ShouldBe("The order reference number");
    }

    [Fact]
    public void Property_InvalidExpression_ThrowsArgumentException()
    {
        ImportDefinitionBuilder<Order> builder = new();

        Should.Throw<ArgumentException>(() =>
            builder.Property(o => o.OrderNumber.Length));
    }

    [Fact]
    public void ExcludeOnUpdate_AddsExcludedProperty()
    {
        ImportDefinitionBuilder<Order> builder = new();

        builder.ExcludeOnUpdate(o => o.OrderNumber);

        builder.ExcludedOnUpdateProperties.ShouldContain("OrderNumber");
    }

    [Fact]
    public void FluentChaining_AllMethodsReturnBuilder()
    {
        ImportDefinitionBuilder<Order> builder = new();

        ImportDefinitionBuilder<Order> result = builder
            .HasBusinessKey(o => o.OrderNumber)
            .HasExternalId()
            .GroupBy("OrderNumber")
            .Property(o => o.OrderNumber)
            .Property(o => o.CustomerName, p => p.DisplayName("Customer"))
            .ExcludeOnUpdate(o => o.OrderNumber)
            .HasMany<OrderLine>(o => o.Lines, child =>
                child.Property(l => l.ProductName));

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ImportDefinition_GetHasExternalId_ReturnsTrueWhenConfigured()
    {
        ExternalIdDefinition definition = new();

        definition.GetHasExternalId().ShouldBeTrue();
    }

    [Fact]
    public void ImportDefinition_GetGroupByColumn_ReturnsValueWhenConfigured()
    {
        GroupedDefinition definition = new();

        definition.GetGroupByColumn().ShouldBe("OrderNumber");
    }

    [Fact]
    public void ImportDefinition_MaxFileSizeMb_DefaultIs50()
    {
        GroupedDefinition definition = new();

        definition.MaxFileSizeMb.ShouldBe(50);
    }

    // ── Test definitions ─────────────────────────────────────────

    private sealed class ExternalIdDefinition : ImportDefinition<Order>
    {
        public override string Name => "Test.ExternalId";

        protected override void Configure(ImportDefinitionBuilder<Order> builder)
        {
            builder
                .HasExternalId()
                .Property(o => o.OrderNumber);
        }
    }

    private sealed class GroupedDefinition : ImportDefinition<Order>
    {
        public override string Name => "Test.Grouped";

        protected override void Configure(ImportDefinitionBuilder<Order> builder)
        {
            builder
                .GroupBy("OrderNumber")
                .Property(o => o.OrderNumber)
                .HasMany<OrderLine>(o => o.Lines, child =>
                    child.Property(l => l.ProductName));
        }
    }
}
