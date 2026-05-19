using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class CrossModuleReferenceAnalyzerTests
{
    private const string ModuleAInternalStub = """
        namespace MyApp.Modules.Inventory.Services
        {
            public class InventoryService
            {
                public void Reserve(int quantity) { }
            }
        }
        """;

    private const string ModuleAContractsStub = """
        namespace MyApp.Modules.Inventory.Contracts.Events
        {
            public record ItemReserved(int Quantity);
        }
        """;

    private const string ModuleAContractsRootStub = """
        namespace MyApp.Modules.Inventory.Contracts
        {
            public interface IInventoryReader
            {
                int GetStock(string sku);
            }
        }
        """;

    private const string ModuleADomainStub = """
        namespace MyApp.Modules.Inventory.Domain
        {
            public class InventoryItem
            {
                public string Sku { get; set; }
                public int Quantity { get; set; }
            }
        }
        """;

    [Fact]
    public async Task GRMOD001_fires_on_cross_module_internal_type_reference()
    {
        string source = """
            using MyApp.Modules.Inventory.Services;

            namespace MyApp.Modules.Orders.Handlers
            {
                public class OrderHandler
                {
                    private readonly InventoryService _inventory;

                    public OrderHandler(InventoryService inventory)
                    {
                        _inventory = inventory;
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleAInternalStub }, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRMOD001_silent_on_contracts_reference()
    {
        string source = """
            using MyApp.Modules.Inventory.Contracts.Events;

            namespace MyApp.Modules.Orders.Handlers
            {
                public class OrderHandler
                {
                    public void Handle()
                    {
                        var evt = new ItemReserved(5);
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleAContractsStub }, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRMOD001_silent_on_same_module_reference()
    {
        string source = """
            using MyApp.Modules.Inventory.Services;

            namespace MyApp.Modules.Inventory.Handlers
            {
                public class StockHandler
                {
                    private readonly InventoryService _service;

                    public StockHandler(InventoryService service)
                    {
                        _service = service;
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleAInternalStub }, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRMOD001_silent_on_non_module_namespace()
    {
        string source = """
            using System;
            using System.Collections.Generic;

            namespace MyApp.Modules.Orders.Handlers
            {
                public class OrderHandler
                {
                    private readonly List<string> _items = new();

                    public void Handle()
                    {
                        Console.WriteLine("hello");
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRMOD001_fires_on_base_class_inheritance()
    {
        string source = """
            using MyApp.Modules.Inventory.Domain;

            namespace MyApp.Modules.Orders.Domain
            {
                public class OrderItem : InventoryItem
                {
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleADomainStub }, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRMOD001_fires_on_constructor_parameter_type()
    {
        string source = """
            using MyApp.Modules.Inventory.Domain;

            namespace MyApp.Modules.Orders.Handlers
            {
                public class OrderHandler
                {
                    public OrderHandler(InventoryItem item) { }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleADomainStub }, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRMOD001_silent_on_contracts_nested_namespace()
    {
        string source = """
            using MyApp.Modules.Inventory.Contracts.Events;

            namespace MyApp.Modules.Orders.Handlers
            {
                public class OrderHandler
                {
                    public ItemReserved Process()
                    {
                        return new ItemReserved(10);
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleAContractsStub }, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRMOD001_fires_on_field_type()
    {
        string source = """
            using MyApp.Modules.Inventory.Domain;

            namespace MyApp.Modules.Orders.Domain
            {
                public class Order
                {
                    private InventoryItem _item;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleADomainStub }, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRMOD001_silent_when_no_modules_exist()
    {
        string source = """
            namespace MyApp.Services
            {
                public class OrderService
                {
                    public void Process() { }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRMOD001_fires_with_correct_module_names_in_message()
    {
        string source = """
            using MyApp.Modules.Inventory.Services;

            namespace MyApp.Modules.Orders.Handlers
            {
                public class OrderHandler
                {
                    public void Handle(InventoryService svc) { }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleAInternalStub }, TestContext.Current.CancellationToken);

        Diagnostic diagnostic = diagnostics.First(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId);
        string message = diagnostic.GetMessage();
        message.ShouldContain("Inventory");
        message.ShouldContain("Orders");
        message.ShouldContain("InventoryService");
    }

    [Fact]
    public async Task GRMOD001_fires_on_generic_type_argument()
    {
        string source = """
            using System.Collections.Generic;
            using MyApp.Modules.Inventory.Domain;

            namespace MyApp.Modules.Orders.Handlers
            {
                public class OrderHandler
                {
                    private readonly List<InventoryItem> _items = new();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleADomainStub }, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRMOD001_silent_on_contracts_root_namespace()
    {
        string source = """
            using MyApp.Modules.Inventory.Contracts;

            namespace MyApp.Modules.Orders.Handlers
            {
                public class OrderHandler
                {
                    private readonly IInventoryReader _reader;

                    public OrderHandler(IInventoryReader reader)
                    {
                        _reader = reader;
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<CrossModuleReferenceAnalyzer>(
                source, new[] { ModuleAContractsRootStub }, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == CrossModuleReferenceAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }
}
