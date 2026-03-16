---
title: "ADR-005: Wolverine + Cronos — Messaging, CQRS and Scheduling"
description: "Selection of Wolverine for messaging and CQRS with PostgreSQL transport, and Cronos for cron scheduling"
sidebar:
  order: 5
  label: "005 - Wolverine + Cronos"
---

> **Date:** 2026-02-22
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Wolverine, Granit.Wolverine.Postgresql, Granit.BackgroundJobs)

## Context

The platform requires:

- **Asynchronous messaging**: sending commands and events between modules
  (domain events, integration events) with delivery guarantees
- **Transactional outbox**: messages must be persisted in the same
  transaction as business changes (eventual consistency without loss)
- **CQRS**: command/query separation with an integrated mediator
- **Background jobs**: execution of recurring tasks (synchronization, cleanup,
  reports) with cron scheduling and multi-instance resilience
- **No external broker**: for the MVP, avoid the operational complexity
  of a RabbitMQ or Kafka — PostgreSQL must suffice as transport

Cronos is used as the cron expression parser in the `Granit.BackgroundJobs`
module for recurring job scheduling.

## Decision

- **Wolverine** (WolverineFx) as message bus, mediator and handler framework
  with PostgreSQL outbox
- **Cronos** as cron expression parser for background job scheduling

## Alternatives considered

### Messaging / Mediator

#### Wolverine (selected)

- **License**: MIT (JasperFx)
- **Outbox**: native EF Core transactional (`WolverineFx.EntityFrameworkCore`)
- **Transport**: native PostgreSQL (`WolverineFx.Postgresql`) — no broker required
- **Pipeline**: composable middleware (validation, retry, DLQ, logging)
- **Handlers**: convention-based (no interface to implement), auto-discovery
- **Integration**: native FluentValidation middleware, multi-tenancy support

#### MassTransit

- **License**: Apache-2.0
- **Advantage**: very mature, large community, multi-transport support
  (RabbitMQ, Azure SB, Amazon SQS, in-memory)
- **Disadvantage**: requires an external broker for production (RabbitMQ
  minimum), more verbose configuration, EF Core outbox available but
  less integrated than Wolverine, no native PostgreSQL-as-transport

#### MediatR

- **License**: Apache-2.0
- **Advantage**: simple, lightweight, pure mediator pattern
- **Disadvantage**: no outbox, no transport, no retry/DLQ,
  no scheduling — only an in-process mediator. Requires combining
  with another tool for asynchronous messaging

#### Brighter

- **License**: MIT
- **Advantage**: outbox support, middleware pipeline
- **Disadvantage**: smaller community, less comprehensive documentation,
  more complex configuration than Wolverine

#### NServiceBus

- **License**: commercial (Particular Software)
- **Advantage**: complete enterprise solution, saga support, monitoring
- **Disadvantage**: paid license, incompatible with the project's OSS strategy

### Scheduling / Cron

#### Cronos (selected)

- **License**: MIT
- **Advantage**: lightweight and fast cron parser, optional seconds support,
  next occurrence calculation without state
- **Usage**: integrated in `RecurringJobAttribute` and `CronSchedulerAgent`

#### Quartz.NET

- **Advantage**: complete scheduler with persistence, clustering, advanced triggers
- **Disadvantage**: oversized (complete scheduler when Wolverine already handles
  execution), responsibility duplication, heavy configuration

#### Hangfire

- **License**: LGPL-3.0 (core), commercial (Pro)
- **Advantage**: built-in dashboard, recurring jobs, automatic retry
- **Disadvantage**: overlap with Wolverine (transport, retry, DLQ), restrictive
  license for advanced features

#### NCrontab

- **Advantage**: simple and lightweight cron parser
- **Disadvantage**: no seconds support, less modern API than Cronos,
  reduced maintenance

## Justification

### Messaging

| Criterion | Wolverine | MassTransit | MediatR | Brighter | NServiceBus |
| --------- | --------- | ----------- | ------- | -------- | ----------- |
| License | MIT | Apache-2.0 | Apache-2.0 | MIT | Commercial |
| EF Core outbox | Native | Yes | No | Yes | Yes |
| PostgreSQL transport | Native | No | N/A | No | No |
| Broker required | No | Yes (prod) | N/A | Yes | Yes |
| Middleware pipeline | Yes | Yes | Yes | Yes | Yes |
| FluentValidation | Native | Third-party | Third-party | No | No |
| Convention-based | Yes | Partial | No | No | No |
| Multi-tenancy | Yes | Yes | No | No | Yes |

### Scheduling

| Criterion | Cronos | Quartz.NET | Hangfire | NCrontab |
| --------- | ------ | ---------- | -------- | -------- |
| License | MIT | Apache-2.0 | LGPL/Commercial | Apache-2.0 |
| Scope | Parser only | Complete scheduler | Complete scheduler | Parser only |
| Seconds | Optional | Yes | No | No |
| Weight | Very light | Heavy | Medium | Light |

## Consequences

### Positive

- No external broker: PostgreSQL suffices as transport (operational simplicity)
- Transactional outbox: zero message loss, guaranteed eventual consistency
- Unified Wolverine pipeline: validation, retry, DLQ, logging, tracing
- Lightweight Cronos: just a parser, orchestration is handled by Wolverine
- MIT license for the entire stack

### Negative

- Wolverine is less known than MassTransit (smaller community)
- Dependency on JasperFx (primary maintainer: Jeremy D. Miller)
- If a broker need arises (RabbitMQ, Kafka), migration required
  (Wolverine supports RabbitMQ and Azure SB, but not Kafka natively)
- PostgreSQL-as-transport has throughput limits vs a dedicated broker
