---
title: "Getting Started with Granit for .NET 10"
description: Build a production-ready REST API with Granit for .NET 10 in 5 progressive steps — module system, EF Core PostgreSQL persistence, Keycloak authentication, and OpenTelemetry observability.
sidebar:
  label: Getting Started
  order: 0
---

This tutorial walks you through building a complete REST API with Granit — from an empty project to a production-ready service with persistence, authentication, and observability.

## Prerequisites

- **.NET 10 SDK** (or later)
- **Docker** (for PostgreSQL)
- A code editor (Rider, VS Code, or Visual Studio)

## What you will build

A Task Management API with:

- CRUD endpoints using Minimal API
- EF Core persistence with automatic audit trails
- JWT authentication via Keycloak
- OpenTelemetry observability (logs, traces, metrics)

Each step builds on the previous one. By the end, you will have a production-ready service that follows Granit conventions.

## Steps

1. [Your First API](./your-first-api/) — Create a module, wire it into `Program.cs`, define a domain model, and expose your first endpoint.
2. [Adding Persistence](./adding-persistence/) — Add EF Core with PostgreSQL, automatic audit fields, and soft delete.
3. [Adding Authentication](./adding-authentication/) — Secure endpoints with JWT Bearer tokens and Keycloak.
4. [Project Templates](./project-templates/) — Use `dotnet new` templates to scaffold new Granit projects.
5. [Next Steps](./next-steps/) — Explore advanced modules: notifications, workflows, blob storage, and more.
