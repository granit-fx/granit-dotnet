---
title: Concepts
description: Core concepts and design principles behind Granit
sidebar:
  order: 0
---

This section explains the foundational concepts that inform how Granit is designed
and how its modules interact.

Understanding these concepts will help you make informed decisions about which
modules to use and how to configure them for your application.

## Core architecture

- [Module System](./module-system/) -- `[DependsOn]`, topological sort, lifecycle hooks
- [Dependency Injection](./dependency-injection/) -- Granit DI conventions, Options pattern
- [Configuration](./configuration/) -- configuration sources, layering, typed options
- [Bundles](./bundles/) -- meta-packages and the fluent `GranitBuilder` API

## Data and infrastructure

- [Persistence](./persistence/) -- isolated DbContext, interceptors, conventions
- [Multi-Tenancy](./multi-tenancy/) -- 3 isolation strategies, soft dependency
- [Messaging](./messaging/) -- Wolverine, Channel fallback, transactional outbox
- [Wolverine Optionality](./wolverine-optionality/) -- what works without Wolverine

## Security and compliance

- [Security Model](./security-model/) -- authentication, authorization, encryption
- [Compliance](./compliance/) -- GDPR, ISO 27001 enforcement

## Architecture decisions

- [Modular Monolith vs Microservices](./modular-monolith-vs-microservices/) -- when to stay monolith, when to extract
