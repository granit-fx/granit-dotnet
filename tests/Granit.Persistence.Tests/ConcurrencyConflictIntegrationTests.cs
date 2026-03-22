// =============================================================================
// Integration Tests - Concurrency Conflict → HTTP 409
// =============================================================================
// End-to-end verification that a DbUpdateConcurrencyException thrown by
// EF Core's concurrency token check is surfaced as an HTTP 409 Conflict
// response with RFC 7807 ProblemDetails through the GranitExceptionHandler
// pipeline.
//
// Story: #290 — Map DbUpdateConcurrencyException to HTTP 409 Conflict
// =============================================================================

using System.Net;
using System.Net.Http.Json;
using Granit.Core.Domain;
using Granit.Http.ExceptionHandling.Extensions;
using Granit.Persistence.Extensions;
using Granit.Persistence.Interceptors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class ConcurrencyConflictIntegrationTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public ConcurrencyConflictIntegrationTests()
    {
        // Shared in-memory SQLite connection (persists across DbContext instances)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Register exception handling + persistence (wires EfCoreExceptionStatusCodeMapper)
        builder.Services.AddGranitExceptionHandling();
        builder.Services.AddGranitPersistence();

        // Register the test DbContext with ConcurrencyStampInterceptor
        builder.Services.AddDbContextFactory<ConcurrencyTestDbContext>((sp, options) =>
        {
            options.UseSqlite(_connection);
            options.AddInterceptors(sp.GetRequiredService<ConcurrencyStampInterceptor>());
        }, ServiceLifetime.Scoped);

        _app = builder.Build();

        _app.UseGranitExceptionHandling();

        // GET /products/{id} — returns current entity state
        _app.MapGet("/products/{id:guid}", async Task<Microsoft.AspNetCore.Http.HttpResults.Results<
            Microsoft.AspNetCore.Http.HttpResults.Ok<ProductResponse>,
            Microsoft.AspNetCore.Http.HttpResults.NotFound>> (Guid id, ConcurrencyTestDbContext db, CancellationToken ct) =>
        {
            ProductEntity? product = await db.Products.FindAsync([id], ct);
            if (product is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(new ProductResponse(product.Id, product.Name, product.ConcurrencyStamp));
        });

        // PUT /products/{id} — simulates a disconnected update with concurrency check
        _app.MapPut("/products/{id:guid}", async Task<Microsoft.AspNetCore.Http.HttpResults.Results<
            Microsoft.AspNetCore.Http.HttpResults.Ok<ProductResponse>,
            Microsoft.AspNetCore.Http.HttpResults.NotFound>> (Guid id, UpdateProductRequest request, ConcurrencyTestDbContext db, CancellationToken ct) =>
        {
            ProductEntity? product = await db.Products.FindAsync([id], ct);
            if (product is null)
            {
                return TypedResults.NotFound();
            }

            // Set OriginalValue to the stamp from the client (disconnected CQRS pattern)
            db.Entry(product).Property(e => e.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;

            product.Name = request.Name;
            await db.SaveChangesAsync(ct);

            return TypedResults.Ok(new ProductResponse(product.Id, product.Name, product.ConcurrencyStamp));
        });

        _app.StartAsync().GetAwaiter().GetResult();

        // Ensure schema exists
        using IServiceScope scope = _app.Services.CreateScope();
        ConcurrencyTestDbContext db = scope.ServiceProvider.GetRequiredService<ConcurrencyTestDbContext>();
        db.Database.EnsureCreated();

        _client = _app.GetTestClient();
    }

    // =========================================================================
    // Happy path: sequential update with fresh stamp → 200 OK
    // =========================================================================

    [Fact]
    public async Task PutProduct_WithFreshStamp_Returns200()
    {
        // Arrange — seed a product
        Guid productId = await SeedProductAsync("Widget");
        ProductResponse initial = await GetProductAsync(productId);

        // Act — update with the current stamp
        var request = new UpdateProductRequest("Widget v2", initial.ConcurrencyStamp);
        HttpResponseMessage response = await _client.PutAsJsonAsync(
            $"/products/{productId}", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ProductResponse updated = (await response.Content.ReadFromJsonAsync<ProductResponse>(
            TestContext.Current.CancellationToken))!;
        updated.Name.ShouldBe("Widget v2");
        updated.ConcurrencyStamp.ShouldNotBe(initial.ConcurrencyStamp, "stamp must rotate after update");
    }

    // =========================================================================
    // Conflict: stale stamp → HTTP 409 with ProblemDetails
    // =========================================================================

    [Fact]
    public async Task PutProduct_WithStaleStamp_Returns409WithProblemDetails()
    {
        // Arrange — seed a product and capture the initial stamp
        Guid productId = await SeedProductAsync("Gadget");
        ProductResponse initial = await GetProductAsync(productId);

        // First update succeeds and rotates the stamp
        var firstUpdate = new UpdateProductRequest("Gadget v2", initial.ConcurrencyStamp);
        HttpResponseMessage firstResponse = await _client.PutAsJsonAsync(
            $"/products/{productId}", firstUpdate, TestContext.Current.CancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act — second update with the now-stale initial stamp
        var staleUpdate = new UpdateProductRequest("Gadget v3", initial.ConcurrencyStamp);
        HttpResponseMessage conflictResponse = await _client.PutAsJsonAsync(
            $"/products/{productId}", staleUpdate, TestContext.Current.CancellationToken);

        // Assert — 409 Conflict with RFC 7807 ProblemDetails
        conflictResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        conflictResponse.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        ProblemDetails? problem = await conflictResponse.Content.ReadFromJsonAsync<ProblemDetails>(
            TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status409Conflict);
        problem.Extensions.ShouldContainKey("traceId");
    }

    // =========================================================================
    // Verify entity is unmodified after conflict
    // =========================================================================

    [Fact]
    public async Task PutProduct_AfterConflict_EntityRetainsLastSuccessfulUpdate()
    {
        // Arrange
        Guid productId = await SeedProductAsync("Doohickey");
        ProductResponse initial = await GetProductAsync(productId);

        // First update succeeds
        var firstUpdate = new UpdateProductRequest("Doohickey v2", initial.ConcurrencyStamp);
        await _client.PutAsJsonAsync(
            $"/products/{productId}", firstUpdate, TestContext.Current.CancellationToken);

        // Second update with stale stamp → 409
        var staleUpdate = new UpdateProductRequest("Doohickey CONFLICT", initial.ConcurrencyStamp);
        HttpResponseMessage conflictResponse = await _client.PutAsJsonAsync(
            $"/products/{productId}", staleUpdate, TestContext.Current.CancellationToken);
        conflictResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Assert — entity still has the first update's name
        ProductResponse current = await GetProductAsync(productId);
        current.Name.ShouldBe("Doohickey v2", "the conflicting update must not have been persisted");
    }

    // =========================================================================
    // Test helpers
    // =========================================================================

    private async Task<Guid> SeedProductAsync(string name)
    {
        var productId = Guid.NewGuid();
        using IServiceScope scope = _app.Services.CreateScope();
        ConcurrencyTestDbContext db = scope.ServiceProvider.GetRequiredService<ConcurrencyTestDbContext>();
        db.Products.Add(new ProductEntity { Id = productId, Name = name });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return productId;
    }

    private async Task<ProductResponse> GetProductAsync(Guid id)
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/products/{id}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(
            TestContext.Current.CancellationToken))!;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // =========================================================================
    // Test infrastructure
    // =========================================================================

    private sealed record ProductResponse(Guid Id, string Name, string ConcurrencyStamp);

    private sealed record UpdateProductRequest(string Name, string ConcurrencyStamp) : IConcurrencyStampRequest;

    private sealed class ProductEntity : Entity, IConcurrencyAware
    {
        public string Name { get; set; } = string.Empty;
        public string ConcurrencyStamp { get; set; } = string.Empty;
    }

    private sealed class ConcurrencyTestDbContext(DbContextOptions<ConcurrencyTestDbContext> options)
        : DbContext(options)
    {
        public DbSet<ProductEntity> Products => Set<ProductEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProductEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
                b.Property(e => e.ConcurrencyStamp).HasMaxLength(36).IsConcurrencyToken();
            });
        }
    }
}
