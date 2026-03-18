---
title: "Testing Guide \u2014 xUnit, NSubstitute & Testcontainers"
description: Testing conventions for Granit packages — xUnit, Shouldly, NSubstitute, Bogus, Testcontainers integration tests, FakeTimeProvider, and NetArchTest architecture rules.
sidebar:
  label: Testing Guide
  order: 4
---

Every Granit package has a matching `*.Tests` project under `tests/`. This is
enforced by the architecture test `Every_src_package_should_have_a_test_project`.

## Stack

| Library | Role |
| ------- | ---- |
| **xUnit** | Test framework |
| **Shouldly** | Assertions |
| **NSubstitute** | Mocking |
| **Bogus** | Test data generation |
| **FakeTimeProvider** | Deterministic time control (`Microsoft.Extensions.TimeProvider.Testing`) |

## Naming

Test classes follow the pattern `{ClassUnderTest}Tests`:

| Source class | Test class |
| ------------ | ---------- |
| `Clock` | `ClockTests` |
| `AuditedEntityInterceptor` | `AuditedEntityInterceptorTests` |
| `CurrentUserService` | `CurrentUserServiceTests` |

Test methods follow `Method_Scenario_ExpectedBehavior`:

```csharp
[Fact]
public void Now_ReturnsUtcFromTimeProvider() { ... }

[Fact]
public async Task SaveChangesAsync_OnAdd_SetsCreatedFields() { ... }

[Fact]
public void UserId_WithAuthenticatedUser_ReturnsSubClaim() { ... }
```

## AAA pattern

All tests follow Arrange-Act-Assert strictly:

```csharp
[Fact]
public void Normalize_ConvertsLocalOffsetToUtc()
{
    // Arrange - DateTimeOffset with +02:00 offset (Europe/Brussels in summer)
    var localTime = new DateTimeOffset(2026, 6, 15, 14, 30, 0, TimeSpan.FromHours(2));

    // Act
    var normalized = _clock.Normalize(localTime);

    // Assert - Must be converted to UTC (+00:00), same instant
    normalized.Offset.ShouldBe(TimeSpan.Zero);
    normalized.ShouldBe(new DateTimeOffset(2026, 6, 15, 12, 30, 0, TimeSpan.Zero));
}
```

## Test class structure

Every test class is `public sealed class`. No inheritance hierarchies, no shared
base classes. Dependencies are initialized in the constructor:

```csharp
public sealed class ClockTests
{
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly ICurrentTimezoneProvider _timezoneProvider;
    private readonly Clock _clock;

    public ClockTests()
    {
        _fakeTimeProvider = new FakeTimeProvider();
        _timezoneProvider = Substitute.For<ICurrentTimezoneProvider>();
        _clock = new Clock(_fakeTimeProvider, _timezoneProvider);
    }

    // ... tests
}
```

## Descriptive file headers

Each test file starts with a header describing what is tested and the approach:

```csharp
// =============================================================================
// Tests - AuditedEntityInterceptor
// =============================================================================
// Verifies that ISO 27001 audit fields are correctly populated
// during entity creation and modification.
//
// Approach: register the interceptor in the DbContext and call
// SaveChangesAsync directly, which triggers the interceptor naturally.
// IClock is mocked for exact assertions.
// =============================================================================
```

## CancellationToken in tests

:::caution[Warning]
Always use `TestContext.Current.CancellationToken` in async tests. Never use
`CancellationToken.None`. This ensures tests are cancelled properly when the
test runner times out or is stopped.
:::

```csharp
[Fact]
public async Task GetAsync_ReturnsEntity()
{
    // Arrange
    var store = CreateStore();

    // Act
    var result = await store.GetAsync(entityId, TestContext.Current.CancellationToken);

    // Assert
    result.ShouldNotBeNull();
}
```

## Assertions with Shouldly

Common patterns:

```csharp
// Exact value
entity.CreatedAt.ShouldBe(fixedNow);

// Not empty
guid.ShouldNotBe(Guid.Empty);

// Collection
sut.Roles.ShouldBe(new[] { "admin", "practitioner" });

// Boolean
sut.IsAuthenticated.ShouldBeTrue();

// Null
sut.UserId.ShouldBeNull();

// Count
guids.Count().ShouldBe(10_000, "all GUIDs should be unique");

// Contextual message for non-obvious assertions
now.Offset.ShouldBe(TimeSpan.Zero,
    "Clock must always return UTC (ISO 27001 compliance)");
```

### Exception assertions

```csharp
// Sync
InvalidOperationException ex = Should.Throw<InvalidOperationException>(act);
ex.Message.ShouldContain("expected text");

// Async
InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(act);
ex.Message.ShouldContain("expected text");
```

## Mocking with NSubstitute

### Interface mocking

```csharp
var clock = Substitute.For<IClock>();
var fixedNow = new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
clock.Now.Returns(fixedNow);
```

### IOptions mocking

```csharp
// Using NSubstitute
var options = Substitute.For<IOptions<GuidGeneratorOptions>>();
options.Value.Returns(new GuidGeneratorOptions
{
    DefaultSequentialGuidType = SequentialGuidType.SequentialAsString
});

// Using the Options helper (simpler)
var options = Microsoft.Extensions.Options.Options.Create(new VaultOptions
{
    TransitMountPoint = "transit"
});
```

### Mocking strategy

| Dependency | Strategy | Reason |
| ---------- | -------- | ------ |
| `IClock` | Mock | Deterministic time assertions |
| `IGuidGenerator` | Mock | Control generated identifiers |
| `ICurrentUserService` | Mock | Simulate different user contexts |
| `TimeProvider` | `FakeTimeProvider` (real implementation) | Provided by Microsoft for time tests |
| `DbContext` | In-memory (real implementation) | Test EF Core interceptors with real pipeline |
| `IVaultClient` | Mock | No Vault in unit tests |

## Deterministic time

Use `FakeTimeProvider` for time-dependent tests:

```csharp
FakeTimeProvider fakeTimeProvider = new();
ICurrentTimezoneProvider timezoneProvider = Substitute.For<ICurrentTimezoneProvider>();
Clock clock = new(fakeTimeProvider, timezoneProvider);

// Pin time to a fixed instant
DateTimeOffset fixedTime = new(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
fakeTimeProvider.SetUtcNow(fixedTime);

clock.Now.ShouldBe(fixedTime);
```

:::tip[Pro tip]
Never use `DateTimeOffset.UtcNow` in assertions. It introduces
non-determinism and forces approximate comparisons. Always pin time with
`FakeTimeProvider` and assert exact values.
:::

## EF Core integration tests

Integration tests use `UseInMemoryDatabase` with a unique name per test for
isolation:

```csharp
private TestDbContext CreateContext()
{
    var interceptor = new AuditedEntityInterceptor(
        _currentUserService, _clock, _guidGenerator);
    var options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .AddInterceptors(interceptor)
        .Options;
    return new TestDbContext(options);
}
```

Define test entities and DbContexts as internal types within the test class:

```csharp
private sealed class TestEntity : AuditedEntity
{
    public string Name { get; set; } = string.Empty;
}

private sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options) { }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>()
            .Property(e => e.Id).ValueGeneratedNever();
    }
}
```

## Architecture tests

`Granit.ArchitectureTests` enforces structural rules across all packages using
ArchUnitNET (assembly analysis) and source code scanning (regex). These tests
cover:

- **Class design** -- sealed DbContexts, no MVC controllers, sealed Options,
  no public types in `Internal` namespaces
- **Layer dependencies** -- Core/Timing/Guids must not depend on EF Core,
  Endpoints must not depend on EF Core, no `IQueryable` outside persistence
- **Naming conventions** -- interfaces start with `I`, Reader/Writer suffixes,
  no `Dto` suffix in endpoints
- **Module conventions** -- modules must be sealed, names must start with
  `Granit` and end with `Module`
- **File organization** -- modules at root, extensions in `Extensions/`,
  DTOs in `Dtos/`, etc.
- **Anti-patterns** -- no `async void`, no `throw ex;`, namespace matches
  directory structure

Run them with:

```bash
dotnet test tests/Granit.ArchitectureTests
```

:::caution[Warning]
Never modify `Granit.ArchitectureTests` or `Granit.ArchitectureTests.Abstractions`
to work around a test failure. These are architectural guardrails. If a rule
produces a false positive, mark it as won't fix.
:::

## Running tests

```bash
# All tests
dotnet test

# Specific package
dotnet test tests/Granit.Security.Tests

# Architecture tests only
dotnet test tests/Granit.ArchitectureTests

# With detailed output
dotnet test --logger "console;verbosity=detailed"
```
