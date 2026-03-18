---
title: Frontend Pattern Library
description: Catalogue of 8 design patterns used in the @granit/* TypeScript and React packages — Factory, Module Singleton, Adapter, Interceptor, Strategy, Observer, Provider, and Hook Composition.
sidebar:
  order: 0
  badge:
    text: "8"
    variant: note
---

A catalogue of design patterns used in the Granit frontend SDK, organized by category.

Each pattern documents the general concept, how it is implemented in the `@granit/*`
packages, and concrete code examples from the SDK.

## Creation patterns

| Pattern | Description |
| ------- | ----------- |
| [Factory](./factory/) | Hide instance creation complexity behind simple functions |
| [Module Singleton](./module-singleton/) | Cross-package state sharing via ES module cache |

## Structural patterns

| Pattern | Description |
| ------- | ----------- |
| [Adapter](./adapter/) | Convert 3rd-party APIs into React-compatible interfaces |

## Behavioral patterns

| Pattern | Description |
| ------- | ----------- |
| [Interceptor](./interceptor/) | Transparent HTTP request/response pipeline processing |
| [Strategy](./strategy/) | Pluggable implementations behind a common interface |
| [Observer](./observer/) | Event notification without direct coupling |

## React patterns

| Pattern | Description |
| ------- | ----------- |
| [Provider](./provider/) | Context-based dependency injection with typed hooks |
| [Hook Composition](./hook-composition/) | Layer framework + application logic via hook wrapping |
