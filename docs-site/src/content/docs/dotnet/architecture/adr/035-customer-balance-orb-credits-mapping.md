---
title: "ADR-035: Granit.CustomerBalance ↔ ORB Credits mapping"
description: "Document why Granit.CustomerBalance is the framework's answer to ORB Credits, the term-by-term concept mapping, and the deliberate non-goals (drawdown ordering, distributed sagas) — so future contributors don't propose a rewrite that would invalidate the existing module."
sidebar:
  order: 35
  label: "035 - CustomerBalance ↔ ORB Credits"
---

> **Date:** 2026-04-25
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.CustomerBalance`, `Granit.CustomerBalance.EntityFrameworkCore`,
> `Granit.CustomerBalance.Endpoints`, `Granit.CustomerBalance.BackgroundJobs`,
> `Granit.Invoicing` (consumer)

## Context

The ORB alignment audit ([EPIC #1155](https://github.com/granit-fx/granit-dotnet/issues/1155)) flagged "Credits" as a major ORB capability. `Granit.CustomerBalance` already exists and was originally designed as a generic per-tenant credit ledger (see EPIC #874, ~2025). The audit raised an obvious question:

> *Is `Granit.CustomerBalance` ORB Credits, or do we need a new `Granit.Credits` module?*

This ADR resolves that question once and for all, in writing, so the next contributor who reads ORB's docs and reaches for a rewrite stops at a 5-minute read.

## Decision

**`Granit.CustomerBalance` IS Granit's ORB Credits implementation.** No new `Granit.Credits` module. Phase 4 closes the residual gaps in place via four surgical additions:

- [Story #1176](https://github.com/granit-fx/granit-dotnet/issues/1176) — admin debit endpoint (`POST /balance/debit`) for manual drawdowns outside the invoice flow
- [Story #1177](https://github.com/granit-fx/granit-dotnet/issues/1177) — daily `CreditPreExpirationScanJob` + `CreditNearExpirationEto` event for early-warning notifications
- [Story #1178](https://github.com/granit-fx/granit-dotnet/issues/1178) — backlog (drawdown ordering strategies — deferred, see *Non-goals* below)
- [Story #1179](https://github.com/granit-fx/granit-dotnet/issues/1179) — this ADR + module doc update

Effort estimate: **1–2 weeks** for the four stories, vs **3–4 weeks** for a from-scratch `Granit.Credits` module that would duplicate `BalanceAccount`, `BalanceTransaction`, the expiration job, and the `Invoicing` integration. ~50 % savings, plus we retain years of in-production audit-trail data for existing consumers.

## Term-by-term mapping

| ORB term | Granit.CustomerBalance term | Notes |
| --- | --- | --- |
| **Credit** | `BalanceTransaction` with `Type = Credit` | Same shape: amount + currency + source + optional expiration |
| **Credit grant** (admin-issued) | `Credit(... source: Promotional, expiresAt: …)` | `IAdminCreditService.ApplyAsync(...)` exposes it via API |
| **Drawdown** (consumed by an invoice) | `Debit(... source: InvoiceDeduction)` | Driven by `CustomerBalancePrePaymentProcessor` (`IInvoicePrePaymentProcessor`) — runs **before** PSP charge |
| **Manual drawdown** (admin) | `Debit(... source: ManualAdjustment)` | New `IAdminDebitService` from #1176 |
| **Refund credit** (post-payment) | `Credit(... source: RefundCredit)` | Already supported |
| **Overpayment** (customer paid too much) | `Credit(... source: Overpayment)` | Already wired via `OverpaymentCreditHandler` consuming `OverpaymentDetectedEto` |
| **Expiration** (credit lifecycle ends) | `Debit(... source: Expiration)` + `CreditExpiredEto` | `CreditExpirationScanJob` (cron `0 */6 * * *`) handles it idempotently |
| **Pre-expiration notification** (ORB-style early warning) | `CreditNearExpirationEto` published by `CreditPreExpirationScanJob` (cron `0 9 * * *`) | New in #1177; configurable via `CustomerBalanceOptions.PreExpirationWarningDays` (default 7) |
| **Adjustment** (corrective add/remove) | `Credit` or `Debit` with `source: ManualAdjustment` | Same source flag both ways |
| **Per-customer balance** | `BalanceAccount` keyed on `(TenantId, Currency)` | `IConcurrencyAware` for optimistic concurrency |
| **Multi-currency** | One `BalanceAccount` per `(TenantId, Currency)` pair | ISO 4217, normalised to upper-case at creation |
| **Append-only ledger** | `BalanceAccount.Transactions` (each `BalanceTransaction` is `Entity`, never mutated) | Audit trail for ISO 27001 |
| **Webhooks / outbox** | Wolverine `IDistributedEventBus` publishes `BalanceCreditedEto` / `BalanceDebitedEto` / `CreditExpiredEto` / `CreditNearExpirationEto` | Apps route to webhooks via `Granit.Webhooks` if needed |
| **Idempotency on retries** | `(ReferenceId, Source)` lookup before `Debit` | Same pattern in `CustomerBalancePrePaymentProcessor` and `DefaultAdminDebitService` |

The list above is the authoritative cross-reference. If a future ORB feature does not appear here, it is either: (a) covered by an existing Granit primitive named differently (update this table), or (b) a deliberate non-goal (see below).

## Non-goals

Each entry below is a feature ORB has and `Granit.CustomerBalance` deliberately does not. The rationale matters: future contributors reading ORB's docs should not propose adding any of these without re-opening this ADR with concrete evidence that a Granit user needs them.

### 1. Drawdown ordering strategies (FIFO / EarliestExpiringFirst / LargestFirst)

ORB lets the admin choose how concurrent credit grants are consumed: FIFO, earliest-expiring-first (minimises waste), largest-first, etc. Today `Granit.CustomerBalance` debits the global running balance — the per-grant accounting that ordering strategies require is not modelled.

**Why deferred** (see [#1178](https://github.com/granit-fx/granit-dotnet/issues/1178) backlog):

- The majority of B2B SaaS treat all credits as fungible — "$100 of credit" rather than "$50 expiring in March + $50 expiring in June, debited in this order"
- Implementation requires either decomposing every `Debit` into per-grant sub-debits (ledger explodes), or a separate "consumed-from" projection table maintained per debit (extra schema + write path)
- Existing users have asked zero times. Re-evaluate when at least three independent users request it.

### 2. Distributed saga for invoice ↔ balance coordination

ORB's docs hint at a 2-phase commit between credit drawdown and invoice settlement. Granit explicitly does not need this:

- `CustomerBalancePrePaymentProcessor` runs `BalanceAccount.Debit` and the call to `IInvoiceCreditApplier.ApplyCreditAsync` in the **same logical unit of work** — both inside the orchestrator transaction or both rolled back together
- Idempotency on retries is handled by the `(ReferenceId, Source = InvoiceDeduction)` lookup before the second debit attempt — no compensating action needed because the first one is detected and skipped

The "saga" framing only applies when crossing a network boundary that cannot share a transaction. The Granit invoice + balance flow does not cross such a boundary.

### 3. Per-period drawdown quotas

ORB lets you cap how much credit a customer can consume per day/month. No Granit user has asked. If raised, the implementation would extend `IInvoicePrePaymentProcessor` with an additional check before `Debit` — non-invasive, but unjustified today.

### 4. Storno / reversal as a first-class operation

ORB has an explicit "reversal" of a previous transaction. Granit covers the same outcome with `ManualAdjustment` in the inverse direction — same audit trail, fewer concepts.

### 5. Customisable rounding rules

ORB allows non-default rounding modes per ledger. Granit uses `decimal` end-to-end and the ISO 4217 default (banker's rounding to currency precision). Custom rules would add complexity for a 0.01 % use case.

### 6. Reconciliation with external balance providers (Stripe Customer Balance, etc.)

Belongs to consuming apps via `Granit.Payments.Stripe` (and friends), not to the framework. The framework owns the *internal* ledger; bridging to a provider's view of "their" balance is application-layer logic.

## Consequences

**Positive:**

- One module to maintain instead of two; no documentation conflict between `CustomerBalance` and a hypothetical `Credits`
- Existing `OverpaymentCreditHandler`, `CreditExpirationScanJob`, and `CustomerBalancePrePaymentProcessor` continue to work without touching them
- The four Phase 4 additions are surgical (new types, no existing-type signature changes); upgrading consumer apps requires only the standard EF migration for the new `LastPreExpirationNoticedAt` column
- ORB's vocabulary now maps cleanly onto Granit's vocabulary — sales conversations and code reviews stop tripping on naming differences

**Negative:**

- The `BalanceTransaction` immutability invariant is now nuanced — `LastPreExpirationNoticedAt` is mutable side-channel state. Documented inline on the entity; future contributors should NOT extend this pattern to other fields without a similar ADR.
- Consumers expecting the ORB term verbatim (e.g. searching for `Credit` as a type) won't find it — they need to know the mapping. This ADR + the updated module doc page address that.

**Neutral:**

- Per-tenant `PreExpirationWarningDays` is not configurable today (single global value via `CustomerBalanceOptions`). Wire through `Granit.Settings` if a consumer needs it.

## Alternatives considered

### A. Create a brand-new `Granit.Credits` module

Rejected. Would duplicate `BalanceAccount`, `BalanceTransaction`, the expiration job, the `Invoicing` integration, the metrics, the documentation page — without producing any user-visible improvement. The only motivation would be "the namespace matches ORB's" — not a sufficient reason to invalidate years of in-production audit-trail data.

### B. Rename `Granit.CustomerBalance` → `Granit.Credits`

Rejected. Breaking change for every existing consumer, no semantic gain. The `Customer Balance` name has a long history in B2B billing (predates ORB by decades); ORB borrowed the concept and renamed it. Sticking with the older industry term is the conservative call.

### C. Implement drawdown ordering as part of Phase 4

Rejected (see Non-goal #1). Defer to a real user request.

## References

- EPIC [#1155](https://github.com/granit-fx/granit-dotnet/issues/1155) — ORB alignment programme
- Feature [#1160](https://github.com/granit-fx/granit-dotnet/issues/1160) — Phase 4 (this work)
- Stories [#1176](https://github.com/granit-fx/granit-dotnet/issues/1176) (admin debit), [#1177](https://github.com/granit-fx/granit-dotnet/issues/1177) (pre-expiration job), [#1178](https://github.com/granit-fx/granit-dotnet/issues/1178) (backlog: drawdown ordering), [#1179](https://github.com/granit-fx/granit-dotnet/issues/1179) (this ADR)
- Pre-existing [EPIC #874](https://github.com/granit-fx/granit-dotnet/issues/874) — original `Granit.CustomerBalance` design
- ORB documentation — <https://docs.withorb.com>
