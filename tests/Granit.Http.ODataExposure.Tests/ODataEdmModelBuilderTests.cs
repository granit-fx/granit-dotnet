using Granit.Http.ODataExposure.Internal;
using Microsoft.OData.Edm;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests;

/// <summary>
/// Locks the EDM model shape produced by the explicit-whitelist builder
/// (per ADR-050) — one EntitySet per descriptor, EntityType columns
/// strictly limited to the property whitelist passed by
/// <c>ValidateAndResolveWhitelists</c> (resolved from <c>ExportDefinition.GetFields()</c>),
/// key auto-detected on <c>Id</c>. Properties absent from the whitelist —
/// notably the framework-internal <c>DomainEvents</c> /
/// <c>IntegrationEvents</c> collections from <c>AggregateRoot</c> — must
/// not appear in the EDM.
/// </summary>
public sealed class ODataEdmModelBuilderTests
{
    private static Dictionary<Type, IReadOnlyList<string>> Whitelist(Type t, params string[] names) =>
        new() { [t] = names };

    [Fact]
    public void Build_SingleEntitySet_RegistersInTheModel()
    {
        ODataEntitySetDescriptor descriptor = new(
            EntitySetName: "Invoices",
            EntityType: typeof(Invoice),
            QueryDefinitionType: typeof(string),
            RequiredPermission: null);

        IEdmModel model = ODataEdmModelBuilder.Build(
            [descriptor],
            Whitelist(typeof(Invoice), nameof(Invoice.Number), nameof(Invoice.Total)));

        IEdmEntitySet? set = model.EntityContainer.FindEntitySet("Invoices");
        set.ShouldNotBeNull();
        set!.EntityType.Name.ShouldBe(nameof(Invoice));
    }

    [Fact]
    public void Build_MultipleEntitySets_AllAppearInTheContainer()
    {
        ODataEntitySetDescriptor[] descriptors =
        [
            new("Invoices", typeof(Invoice), typeof(string), null),
            new("Customers", typeof(Customer), typeof(string), "OData.Test.Customers.Read"),
        ];

        Dictionary<Type, IReadOnlyList<string>> whitelist = new()
        {
            [typeof(Invoice)] = [nameof(Invoice.Number), nameof(Invoice.Total)],
            [typeof(Customer)] = [nameof(Customer.Name)],
        };

        IEdmModel model = ODataEdmModelBuilder.Build(descriptors, whitelist);

        model.EntityContainer.FindEntitySet("Invoices").ShouldNotBeNull();
        model.EntityContainer.FindEntitySet("Customers").ShouldNotBeNull();
    }

    [Fact]
    public void Build_EntityType_ExposesOnlyWhitelistedProperties()
    {
        // Whitelist drives the shape — the entity has 4 public properties
        // (Id, Number, Total, InternalNotes), the test only allows two.
        // InternalNotes must not appear in the EDM.
        ODataEntitySetDescriptor descriptor = new(
            "Invoices", typeof(InvoiceWithSensitiveField), typeof(string), null);

        IEdmModel model = ODataEdmModelBuilder.Build(
            [descriptor],
            Whitelist(typeof(InvoiceWithSensitiveField),
                nameof(InvoiceWithSensitiveField.Number),
                nameof(InvoiceWithSensitiveField.Total)));

        var entityType = (IEdmEntityType)model.EntityContainer.FindEntitySet("Invoices")!.EntityType;
        IReadOnlyList<string> propertyNames = [.. entityType.Properties().Select(p => p.Name)];

        propertyNames.ShouldContain(nameof(InvoiceWithSensitiveField.Id));
        propertyNames.ShouldContain(nameof(InvoiceWithSensitiveField.Number));
        propertyNames.ShouldContain(nameof(InvoiceWithSensitiveField.Total));
        propertyNames.ShouldNotContain(nameof(InvoiceWithSensitiveField.InternalNotes));
    }

    [Fact]
    public void Build_DomainEventsCollection_NeverAppearsInEdm()
    {
        // ADR-050 anchor test. The leak that drove this ADR: AggregateRoot
        // exposes DomainEvents / IntegrationEvents as public collections to
        // satisfy IDomainEventSource / IIntegrationEventSource. Convention
        // discovery would surface them on $metadata; the explicit whitelist
        // must not.
        ODataEntitySetDescriptor descriptor = new(
            "OrderEntries", typeof(AggregateRootLikeEntity), typeof(string), null);

        IEdmModel model = ODataEdmModelBuilder.Build(
            [descriptor],
            Whitelist(typeof(AggregateRootLikeEntity),
                nameof(AggregateRootLikeEntity.Number)));

        var entityType = (IEdmEntityType)model.EntityContainer.FindEntitySet("OrderEntries")!.EntityType;
        IReadOnlyList<string> propertyNames = [.. entityType.Properties().Select(p => p.Name)];

        propertyNames.ShouldContain(nameof(AggregateRootLikeEntity.Id));
        propertyNames.ShouldContain(nameof(AggregateRootLikeEntity.Number));
        propertyNames.ShouldNotContain(nameof(AggregateRootLikeEntity.DomainEvents));
        propertyNames.ShouldNotContain(nameof(AggregateRootLikeEntity.IntegrationEvents));
    }

    [Fact]
    public void Build_NavigationProperty_AppearsOnlyWhenInExpandWhitelist()
    {
        // Customer is a navigation-like property (custom class). With it in
        // ExpandWhitelist, the EDM must expose it; without, it must be
        // suppressed even though it is a public property.
        ODataEntitySetDescriptor withExpand = new(
            "Invoices", typeof(InvoiceWithCustomerNav), typeof(string), null,
            ExpandWhitelist: ["Customer"]);

        IEdmModel withModel = ODataEdmModelBuilder.Build(
            [withExpand],
            Whitelist(typeof(InvoiceWithCustomerNav), nameof(InvoiceWithCustomerNav.Number)));

        var entityType = (IEdmEntityType)withModel.EntityContainer.FindEntitySet("Invoices")!.EntityType;
        entityType.NavigationProperties().Select(p => p.Name).ShouldContain("Customer");

        ODataEntitySetDescriptor withoutExpand = new(
            "Invoices2", typeof(InvoiceWithCustomerNav), typeof(string), null,
            ExpandWhitelist: null);

        IEdmModel withoutModel = ODataEdmModelBuilder.Build(
            [withoutExpand],
            Whitelist(typeof(InvoiceWithCustomerNav), nameof(InvoiceWithCustomerNav.Number)));

        var entityType2 = (IEdmEntityType)withoutModel.EntityContainer.FindEntitySet("Invoices2")!.EntityType;
        entityType2.NavigationProperties().Select(p => p.Name).ShouldNotContain("Customer");
    }

    [Fact]
    public void Build_EmptyDescriptorList_Throws()
        => Should.Throw<ArgumentException>(() => ODataEdmModelBuilder.Build([], Whitelist(typeof(Invoice))));

    [Fact]
    public void Build_NullDescriptors_Throws()
        => Should.Throw<ArgumentNullException>(() => ODataEdmModelBuilder.Build(null!, Whitelist(typeof(Invoice))));

    [Fact]
    public void Build_DefaultContainerName_IsTenantContainer()
    {
        ODataEntitySetDescriptor descriptor = new(
            "Invoices", typeof(Invoice), typeof(string), null);

        IEdmModel model = ODataEdmModelBuilder.Build(
            [descriptor],
            Whitelist(typeof(Invoice), nameof(Invoice.Number)));

        model.EntityContainer.Name.ShouldBe(ODataEdmModelBuilder.TenantContainerName);
        model.EntityContainer.Name.ShouldBe("Container");
    }

    [Fact]
    public void Build_HostContainerName_DistinguishesTheFeed()
    {
        ODataEntitySetDescriptor descriptor = new(
            "Tenants", typeof(Invoice), typeof(string), null);

        IEdmModel model = ODataEdmModelBuilder.Build(
            [descriptor],
            Whitelist(typeof(Invoice), nameof(Invoice.Number)),
            ODataEdmModelBuilder.HostContainerName);

        model.EntityContainer.Name.ShouldBe("HostContainer");
        model.EntityContainer.Name.ShouldNotBe(ODataEdmModelBuilder.TenantContainerName);
    }

    private sealed class Invoice
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public decimal Total { get; init; }
    }

    private sealed class Customer
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class InvoiceWithSensitiveField
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public string InternalNotes { get; init; } = string.Empty;  // not in whitelist — must NOT appear in EDM
    }

    /// <summary>
    /// Stand-in for an <c>AggregateRoot</c>-derived entity: surfaces public
    /// <c>DomainEvents</c> / <c>IntegrationEvents</c> collections that the
    /// real <c>AggregateRoot</c> requires for <c>IDomainEventSource</c> /
    /// <c>IIntegrationEventSource</c>. The whitelist must keep them out of
    /// <c>$metadata</c>.
    /// </summary>
    private sealed class AggregateRootLikeEntity
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public IReadOnlyCollection<object> DomainEvents { get; } = [];
        public IReadOnlyCollection<object> IntegrationEvents { get; } = [];
    }

    private sealed class InvoiceWithCustomerNav
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public CustomerNav? Customer { get; init; }
    }

    private sealed class CustomerNav
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
