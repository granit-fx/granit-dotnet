---
title: "ADR-003: Testing Stack — xUnit v3, NSubstitute and Bogus"
description: "Selection of xUnit v3, NSubstitute, and Bogus as the foundational testing stack"
sidebar:
  order: 3
  label: "003 - Testing Stack"
---

> **Date:** 2026-02-21
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet, consuming applications

## Context

The Granit framework applies the "tests are part of the DoD" principle: each
package has a `*.Tests` project and no code can be shipped without test coverage.
The choice of testing stack is therefore foundational for the entire platform.

Requirements:

- **Test framework**: parallelism, native CancellationToken, DI in tests,
  xUnit.v3 support for the new APIs
- **Mocking**: dependency substitution (services, repositories, HTTP clients)
  with a clear API and no license issues
- **Test data**: realistic and localized (FR) data generation
- **Coverage**: code coverage collection for CI (Cobertura/OpenCover)
- **CI**: result export in TRX/JUnit format for CI integration

## Decision

| Role | Library | License |
| ---- | ------- | ------- |
| Test framework | xUnit v3 | Apache-2.0 |
| Mocking | NSubstitute | BSD-3-Clause |
| Test data | Bogus | MIT |
| Coverage | coverlet.collector | MIT |
| CI report | JunitXml.TestLogger | MIT |

## Alternatives considered

### Test framework

#### xUnit v3 (selected)

- Native CancellationToken (`TestContext.Current.CancellationToken`)
- Parallelism by default (test collections)
- Dominant adoption in the .NET open source ecosystem
- Native `IAsyncLifetime` support for async setup/teardown

#### NUnit

- Mature framework, rich in attributes (`[TestCase]`, `[SetUp]`, `[TearDown]`)
- Disadvantage: less natural parallelism, modern .NET community leans more toward
  xUnit, no native CancellationToken in tests

#### MSTest

- Official Microsoft framework
- Disadvantage: limited features compared to xUnit/NUnit, low adoption
  in .NET open source, less expressive API

#### TUnit

- Recent framework based on source generators (no runtime reflection)
- Disadvantage: young project (v1.x), single maintainer, limited ecosystem
  (Testcontainers, Verify primarily target xUnit/NUnit)
- Re-evaluation planned via a future ADR when the project reaches sufficient
  maturity (cf. [ADR-014](014-migration-shouldly.md))

### Mocking

#### NSubstitute (selected)

- Clear and readable API (`service.Method().Returns(value)`)
- BSD-3-Clause — no license issues
- No verbose `Setup`/`Verify` syntax

#### Moq

- Most popular historical library
- **License issue**: SponsorLink (v4.20+) injected telemetry code
  into builds, creating a compliance risk and a community trust crisis.
  Incompatible with GDPR/ISO 27001 security policy

#### FakeItEasy

- Pleasant fluent API (`A.CallTo(() => ...).Returns(...)`)
- Disadvantage: more verbose syntax than NSubstitute, smaller community

### Test data

#### Bogus (selected)

- Realistic data generation with locales (fr, fr_BE, en, etc.)
- Fluent API (`new Faker<T>().RuleFor(...)`)
- Support for complex types and business rules

#### AutoFixture

- Automatic generation without configuration
- Disadvantage: unrealistic data (random strings), less control
  over business values, less intuitive syntax

#### Faker.NET

- Port of Faker.js
- Disadvantage: less rich API than Bogus, less maintained

## Justification

### Test framework

| Criterion | xUnit v3 | NUnit | MSTest | TUnit |
| --------- | -------- | ----- | ------ | ----- |
| Native CancellationToken | Yes | No | No | Yes |
| Default parallelism | Yes | Partial | Partial | Yes |
| .NET OSS adoption | Dominant | Strong | Low | Emerging |
| Maturity | 15+ years | 20+ years | 20+ years | < 2 years |
| License | Apache-2.0 | MIT | MIT | Apache-2.0 |

### Mocking

| Criterion | NSubstitute | Moq | FakeItEasy |
| --------- | ----------- | --- | ---------- |
| License | BSD-3-Clause | MIT (+ SponsorLink) | Apache-2.0 |
| SponsorLink risk | No | Yes | No |
| API conciseness | Excellent | Good | Medium |
| Community | Large | Very large | Medium |

### Test data

| Criterion | Bogus | AutoFixture | Faker.NET |
| --------- | ----- | ----------- | --------- |
| FR locales | Yes (fr, fr_BE) | No | Partial |
| Realistic data | Yes | No (random) | Yes |
| Fluent API | Yes | Partial | No |
| Active maintenance | Yes | Yes | Low |

## Consequences

### Positive

- Coherent and modern stack, adopted by the majority of the .NET ecosystem
- Zero license risk (no SponsorLink, no commercial license)
- Native CancellationToken xUnit v3: interruptible tests, faster CI
- Realistic and localized test data (French names, SIRET, etc.)
- Cobertura coverage for SonarQube/SonarCloud and CI

### Negative

- Migration from another test framework would be costly if necessary
- xUnit v3 is recent: some third-party tools may have a support lag
- Bogus generates pseudo-random data (non-deterministic by default —
  use `Seed` for reproducibility)
