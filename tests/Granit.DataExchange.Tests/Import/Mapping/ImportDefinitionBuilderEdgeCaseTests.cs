using Granit.DataExchange.Import;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Mapping;

public sealed class ImportDefinitionBuilderEdgeCaseTests
{
    private sealed class Order
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
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
            .Property(o => o.OrderNumber)
            .Property(o => o.CustomerName, p => p.DisplayName("Customer"))
            .ExcludeOnUpdate(o => o.OrderNumber);

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ImportDefinition_GetHasExternalId_ReturnsTrueWhenConfigured()
    {
        ExternalIdDefinition definition = new();

        definition.GetHasExternalId().ShouldBeTrue();
    }

    [Fact]
    public void ImportDefinition_MaxFileSizeMb_DefaultIs50()
    {
        ExternalIdDefinition definition = new();

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

}
