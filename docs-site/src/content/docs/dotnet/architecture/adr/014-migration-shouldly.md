---
title: "ADR-014: Migrate FluentAssertions to Shouldly"
description: "Migration from FluentAssertions to Shouldly due to license change incompatible with commercial use"
sidebar:
  order: 14
  label: "014 - Migrate to Shouldly"
---

> **Date:** 2026-02-28
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet, consuming applications

## Context

FluentAssertions is the assertion library used in all test projects (`*.Tests`)
of granit-dotnet and consuming applications.

Starting with **version 7.x**, FluentAssertions was acquired by **Xceed Software**
and changed its license: from **MIT** to the **Xceed Community License Agreement**.
This new license **prohibits commercial use** without purchasing a paid license.

The platform is a commercial product (healthcare, ISO 27001 certification). Using
FluentAssertions 8.x in this context constitutes a **license non-compliance**.

## Decision

**Migrate to Shouldly** as the assertion library for all test projects.

## Alternatives considered

### Option 1: Shouldly (selected)

- **License**: Apache-2.0 (permissive, compatible with commercial use)
- **Maturity**: stable project, actively maintained, large community
- **Impact**: assertion replacement only, the xUnit test framework
  remains unchanged
- **API**: concise and readable syntax (`actual.ShouldBe(expected)`)

### Option 2: Downgrade to FluentAssertions 6.x (MIT)

- **License**: MIT (last version under permissive license)
- **Advantage**: no code migration required
- **Disadvantage**: end-of-life version, no more security updates or
  patches. Incompatible with a long-term maintenance strategy

### Option 3: TUnit (complete xUnit + FluentAssertions replacement)

- **License**: Apache-2.0
- **Advantage**: modern test framework with built-in assertions
  (`Assert.That(x).IsEqualTo(42)`), native parallelism via source generators
  (no runtime reflection), native DI in tests, attribute-based lifecycle
  (`[Before]`, `[After]`) without `IAsyncLifetime`
- **Performance**: significantly faster than xUnit on large test suites
  thanks to source generators and default parallelism
- **Disadvantage**: implies complete test framework replacement
  (xUnit -> TUnit), not just assertions. Migration: attributes (`[Fact]` ->
  `[Test]`, `[Theory]` -> `[Test]` + `[Arguments]`), lifecycle, DI, CI runner

**Discussion (2026-02-28)**: since the Granit framework is not yet in production
and the test volume is low, the migration cost to TUnit would currently be
minimal. However:

1. **xUnit v3 just released** with substantial improvements (parallelism,
   native `CancellationToken`, better DI) that reduce the performance gap
2. **Asymmetric risk**: if TUnit stagnates (young project, single maintainer),
   the framework ends up on a niche framework without ecosystem or broad
   community support. The .NET ecosystem (Testcontainers, Verify, etc.)
   primarily targets xUnit/NUnit
3. **Separation of concerns**: decoupling the assertion choice (immediate
   license problem) from the framework choice (distinct architectural decision)
   allows addressing the urgency without speculative bets

**Verdict**: TUnit remains an option to re-evaluate when the project reaches
sufficient maturity (v2+, significant adoption, official Testcontainers
documentation). A dedicated ADR can be opened at that point to evaluate a
xUnit -> TUnit migration based on concrete data

### Option 4: Purchase an Xceed commercial license

- **Advantage**: no migration
- **Disadvantage**: recurring cost, dependency on a third-party vendor for a
  test library, risk of further price increases

## Justification

| Criterion | Shouldly | FA 6.x | TUnit | Xceed License |
| --------- | -------- | ------ | ----- | ------------- |
| Permissive license | Apache-2.0 | MIT (EOL) | Apache-2.0 | Paid |
| Migration effort | Medium | None | Very high | None |
| Longevity | Active maintenance | End of life | Recent | Vendor dependency |
| xUnit compatibility | Full | Full | Incompatible | Full |
| GDPR/ISO 27001 compliance | Yes | Risk (EOL) | Yes | Yes |

Shouldly offers the best compliance / migration effort / longevity ratio.

> **Note**: TUnit presents real advantages in performance and modernity, but
> the risk associated with its youth (v1.x, limited ecosystem) does not justify
> coupling the license problem (urgent) to a framework change (strategic).
> Migration to TUnit can be re-evaluated independently via a future ADR.

## Assertion mapping

| FluentAssertions | Shouldly |
| ---------------- | -------- |
| `x.Should().Be(42)` | `x.ShouldBe(42)` |
| `x.Should().NotBeNull()` | `x.ShouldNotBeNull()` |
| `x.Should().BeTrue()` | `x.ShouldBeTrue()` |
| `x.Should().BeFalse()` | `x.ShouldBeFalse()` |
| `x.Should().BeNull()` | `x.ShouldBeNull()` |
| `list.Should().HaveCount(3)` | `list.Count.ShouldBe(3)` |
| `list.Should().BeEmpty()` | `list.ShouldBeEmpty()` |
| `list.Should().Contain(item)` | `list.ShouldContain(item)` |
| `list.Should().BeEquivalentTo(other)` | `list.ShouldBe(other, ignoreOrder: true)` |
| `list.Should().BeInAscendingOrder()` | `list.ShouldBeInOrder(SortDirection.Ascending)` |
| `x.Should().BeGreaterThan(0)` | `x.ShouldBeGreaterThan(0)` |
| `act.Should().Throw<T>()` | `Should.Throw<T>(() => act())` |
| `await act.Should().ThrowAsync<T>()` | `await Should.ThrowAsync<T>(() => act())` |
| `.Because("reason")` | `customMessage: "reason"` |

## Consequences

### Positive

- License compliance restored (Apache-2.0)
- ISO 27001 audit risk eliminated
- Actively maintained library

### Negative

- One-time migration effort on all `*.Tests` projects
- Team training on Shouldly syntax (low learning curve)
- Documentation update (`docs/testing/assertions.md`, `CLAUDE.md`)

## Execution plan

1. Add `Shouldly` to `Directory.Packages.props` (granit-dotnet + consuming applications)
2. Replace assertions in each `*.Tests` project
3. Remove `FluentAssertions` from `Directory.Packages.props`
4. Update `THIRD-PARTY-NOTICES.md`
5. Update `docs/testing/assertions.md`
6. Update `CLAUDE.md` (Tests section)
7. Validate: `dotnet test` passes without failures
