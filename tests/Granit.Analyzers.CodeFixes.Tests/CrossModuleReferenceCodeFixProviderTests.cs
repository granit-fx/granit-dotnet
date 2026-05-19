using Xunit;

namespace Granit.Analyzers.CodeFixes.Tests;

public sealed class CrossModuleReferenceCodeFixProviderTests
{
    private const string ContractsTypeStub = """
        namespace MyApp.Modules.Orders.Contracts
        {
            public interface IOrderSummary { }
        }
        """;

    private const string InternalTypeStub = """
        namespace MyApp.Modules.Orders.Internal
        {
            public class IOrderSummary { }
        }
        """;

    [Fact]
    public async Task Replaces_internal_using_with_contracts_using()
    {
        string source = """
            using MyApp.Modules.Orders.Internal;

            namespace MyApp.Modules.Billing
            {
                public class InvoiceService
                {
                    public IOrderSummary Get() => null;
                }
            }
            """;

        string expected = """
            using MyApp.Modules.Orders.Contracts;

            namespace MyApp.Modules.Billing
            {
                public class InvoiceService
                {
                    public IOrderSummary Get() => null;
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<CrossModuleReferenceAnalyzer, CrossModuleReferenceCodeFixProvider>(
            source, expected, [InternalTypeStub, ContractsTypeStub]);
    }

    [Fact]
    public async Task No_diagnostic_when_both_usings_present_and_contracts_type_resolves()
    {
        // When both usings are present, the compiler resolves the Contracts interface type
        // (not the Internal class) because IOrderSummary is an interface in Contracts.
        // No cross-module diagnostic should fire.
        string source = """
            using MyApp.Modules.Orders.Contracts;
            using MyApp.Modules.Orders.Internal;

            namespace MyApp.Modules.Billing
            {
                public class InvoiceService
                {
                    public IOrderSummary Get() => null;
                }
            }
            """;

        await CodeFixTestHelpers.VerifyNoCodeFixAsync<CrossModuleReferenceAnalyzer, CrossModuleReferenceCodeFixProvider>(
            source, [InternalTypeStub, ContractsTypeStub]);
    }

    [Fact]
    public async Task No_diagnostic_for_same_module_reference()
    {
        string source = """
            using MyApp.Modules.Orders.Internal;

            namespace MyApp.Modules.Orders.Services
            {
                public class OrderProcessor
                {
                    public IOrderSummary Get() => null;
                }
            }
            """;

        await CodeFixTestHelpers.VerifyNoCodeFixAsync<CrossModuleReferenceAnalyzer, CrossModuleReferenceCodeFixProvider>(
            source, [InternalTypeStub, ContractsTypeStub]);
    }

    [Fact]
    public async Task No_diagnostic_when_using_contracts_namespace()
    {
        string contractsUsageStub = """
            namespace MyApp.Modules.Orders.Contracts
            {
                public class OrderSummaryDto { }
            }
            """;

        string source = """
            using MyApp.Modules.Orders.Contracts;

            namespace MyApp.Modules.Billing
            {
                public class InvoiceService
                {
                    public OrderSummaryDto Get() => null;
                }
            }
            """;

        await CodeFixTestHelpers.VerifyNoCodeFixAsync<CrossModuleReferenceAnalyzer, CrossModuleReferenceCodeFixProvider>(
            source, [contractsUsageStub]);
    }
}
