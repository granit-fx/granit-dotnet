---
title: Guides
description: Step-by-step how-to guides for common tasks
sidebar:
  order: 0
---

Task-oriented guides that show you how to accomplish specific goals with Granit.

Each guide assumes you have a working Granit application. If you are starting
from scratch, begin with [Getting Started](/dotnet/getting-started/).

## Modules and endpoints

- [Create a Module](./create-a-module/) -- build a new Granit module from scratch
- [Add an Endpoint](./add-an-endpoint/) -- Minimal API with validation and Problem Details
- [Configure Multi-Tenancy](./configure-multi-tenancy/) -- shared DB, per-schema, or per-database

## Messaging and events

- [Set Up Notifications](./set-up-notifications/) -- 6-channel notification engine
- [Implement Data Import](./implement-data-import/) -- CSV/Excel import pipeline
- [Add Background Jobs](./add-background-jobs/) -- recurring and delayed jobs
- [Configure Blob Storage](./configure-blob-storage/) -- S3-compatible file storage
- [Implement Webhooks](./implement-webhooks/) -- event delivery with retry

## Features and settings

- [Add Feature Flags](./add-feature-flags/) -- toggle, numeric, and selection features
- [Set Up Localization](./set-up-localization/) -- 17 cultures, source-generated keys
- [Use Reference Data](./use-reference-data/) -- i18n reference tables
- [Manage Application Settings](./manage-application-settings/) -- runtime settings store

## Documents and workflow

- [Create Document Templates](./create-document-templates/) -- Scriban, PDF, Excel
- [Implement Workflow](./implement-workflow/) -- FSM engine, publication lifecycle

## Caching, versioning, and API

- [Configure Caching](./configure-caching/) -- memory, Redis, HybridCache
- [Add API Versioning](./add-api-versioning/) -- URL segment and header versioning
- [Configure Idempotency](./configure-idempotency/) -- Idempotency-Key middleware

## Security and observability

- [Encrypt Sensitive Data](./encrypt-sensitive-data/) -- Vault Transit and AES-256
- [Implement Audit Timeline](./implement-audit-timeline/) -- entity change tracking
- [End-to-End Tracing](./end-to-end-tracing/) -- OpenTelemetry distributed tracing
