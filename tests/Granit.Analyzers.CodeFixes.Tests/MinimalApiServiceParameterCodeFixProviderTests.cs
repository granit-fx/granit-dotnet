using Xunit;

namespace Granit.Analyzers.CodeFixes.Tests;

public sealed class MinimalApiServiceParameterCodeFixProviderTests
{
    private static readonly string[] ApiStubs =
    [
        Analyzers.Tests.AnalyzerTestHelpers.MinimalApiResultsStub,
        Analyzers.Tests.AnalyzerTestHelpers.MinimalApiBindingStub,
    ];

    [Fact]
    public async Task Adds_FromServices_to_interface_parameter()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            public interface IMyService { }
            public static class Endpoint
            {
                public static Ok Handle(IMyService service) => null;
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;

            public interface IMyService { }
            public static class Endpoint
            {
                public static Ok Handle([FromServices] IMyService service) => null;
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<MinimalApiServiceParameterAnalyzer, MinimalApiServiceParameterCodeFixProvider>(
            source, expected, ApiStubs);
    }

    [Fact]
    public async Task Adds_FromServices_with_async_Task_return_type()
    {
        string source = """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            public interface IMyService { }
            public static class Endpoint
            {
                public static Task<Ok> HandleAsync(IMyService service) => null;
            }
            """;

        string expected = """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;

            public interface IMyService { }
            public static class Endpoint
            {
                public static Task<Ok> HandleAsync([FromServices] IMyService service) => null;
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<MinimalApiServiceParameterAnalyzer, MinimalApiServiceParameterCodeFixProvider>(
            source, expected, ApiStubs);
    }

    [Fact]
    public async Task No_diagnostic_when_FromServices_already_present()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            public interface IMyService { }
            public static class Endpoint
            {
                public static Ok Handle([FromServices] IMyService service) => null;
            }
            """;

        await CodeFixTestHelpers.VerifyNoCodeFixAsync<MinimalApiServiceParameterAnalyzer, MinimalApiServiceParameterCodeFixProvider>(
            source, ApiStubs);
    }

    [Fact]
    public async Task No_diagnostic_for_non_static_method()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            public interface IMyService { }
            public class Endpoint
            {
                public Ok Handle(IMyService service) => null;
            }
            """;

        await CodeFixTestHelpers.VerifyNoCodeFixAsync<MinimalApiServiceParameterAnalyzer, MinimalApiServiceParameterCodeFixProvider>(
            source, ApiStubs);
    }

    [Fact]
    public async Task No_diagnostic_for_exempt_IFormFile_parameter()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            public static class Endpoint
            {
                public static Ok Handle(IFormFile file) => null;
            }
            """;

        await CodeFixTestHelpers.VerifyNoCodeFixAsync<MinimalApiServiceParameterAnalyzer, MinimalApiServiceParameterCodeFixProvider>(
            source, ApiStubs);
    }

    [Fact]
    public async Task Does_not_duplicate_using_when_Mvc_already_imported()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            public interface IMyService { }
            public interface IOtherService { }
            public static class Endpoint
            {
                public static Ok Handle([FromServices] IMyService svc, IOtherService other) => null;
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            public interface IMyService { }
            public interface IOtherService { }
            public static class Endpoint
            {
                public static Ok Handle([FromServices] IMyService svc, [FromServices] IOtherService other) => null;
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<MinimalApiServiceParameterAnalyzer, MinimalApiServiceParameterCodeFixProvider>(
            source, expected, ApiStubs);
    }
}
