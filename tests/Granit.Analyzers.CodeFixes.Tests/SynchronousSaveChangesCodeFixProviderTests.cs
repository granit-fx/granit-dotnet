using Xunit;

namespace Granit.Analyzers.CodeFixes.Tests;

public sealed class SynchronousSaveChangesCodeFixProviderTests
{
    private const string DbContextStub = """
        namespace Microsoft.EntityFrameworkCore
        {
            public abstract class DbContext
            {
                public int SaveChanges() => 0;
                public int SaveChanges(bool acceptAllChangesOnSuccess) => 0;
                public System.Threading.Tasks.Task<int> SaveChangesAsync(
                    System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(0);
                public System.Threading.Tasks.Task<int> SaveChangesAsync(
                    bool acceptAllChangesOnSuccess,
                    System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(0);
            }
        }
        """;

    [Fact]
    public async Task Replaces_SaveChanges_in_void_method_with_async_Task()
    {
        string source = """
            using Microsoft.EntityFrameworkCore;
            public class MyRepo : DbContext
            {
                public void Process()
                {
                    this.SaveChanges();
                }
            }
            """;

        string expected = """
            using Microsoft.EntityFrameworkCore;
            using System.Threading.Tasks;

            public class MyRepo : DbContext
            {
                public async Task Process()
                {
                    await this.SaveChangesAsync();
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<SynchronousSaveChangesAnalyzer, SynchronousSaveChangesCodeFixProvider>(
            source, expected, [DbContextStub]);
    }

    [Fact]
    public async Task Adds_await_only_when_method_already_async()
    {
        string source = """
            using System.Threading.Tasks;
            using Microsoft.EntityFrameworkCore;
            public class MyRepo : DbContext
            {
                public async Task Process()
                {
                    this.SaveChanges();
                }
            }
            """;

        string expected = """
            using System.Threading.Tasks;
            using Microsoft.EntityFrameworkCore;
            public class MyRepo : DbContext
            {
                public async Task Process()
                {
                    await this.SaveChangesAsync();
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<SynchronousSaveChangesAnalyzer, SynchronousSaveChangesCodeFixProvider>(
            source, expected, [DbContextStub]);
    }

    [Fact]
    public async Task Preserves_bool_argument_in_SaveChanges_bool()
    {
        string source = """
            using Microsoft.EntityFrameworkCore;
            public class MyRepo : DbContext
            {
                public void Process()
                {
                    this.SaveChanges(true);
                }
            }
            """;

        string expected = """
            using Microsoft.EntityFrameworkCore;
            using System.Threading.Tasks;

            public class MyRepo : DbContext
            {
                public async Task Process()
                {
                    await this.SaveChangesAsync(true);
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<SynchronousSaveChangesAnalyzer, SynchronousSaveChangesCodeFixProvider>(
            source, expected, [DbContextStub]);
    }

    [Fact]
    public async Task Transforms_int_return_type_to_Task_int()
    {
        string source = """
            using Microsoft.EntityFrameworkCore;
            public class MyRepo : DbContext
            {
                public int Process()
                {
                    return this.SaveChanges();
                }
            }
            """;

        string expected = """
            using Microsoft.EntityFrameworkCore;
            using System.Threading.Tasks;

            public class MyRepo : DbContext
            {
                public async Task<int> Process()
                {
                    return await this.SaveChangesAsync();
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<SynchronousSaveChangesAnalyzer, SynchronousSaveChangesCodeFixProvider>(
            source, expected, [DbContextStub]);
    }

    [Fact]
    public async Task Does_not_duplicate_using_System_Threading_Tasks()
    {
        string source = """
            using System.Threading.Tasks;
            using Microsoft.EntityFrameworkCore;
            public class MyRepo : DbContext
            {
                public void Process()
                {
                    this.SaveChanges();
                }
            }
            """;

        string expected = """
            using System.Threading.Tasks;
            using Microsoft.EntityFrameworkCore;
            public class MyRepo : DbContext
            {
                public async Task Process()
                {
                    await this.SaveChangesAsync();
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<SynchronousSaveChangesAnalyzer, SynchronousSaveChangesCodeFixProvider>(
            source, expected, [DbContextStub]);
    }
}
