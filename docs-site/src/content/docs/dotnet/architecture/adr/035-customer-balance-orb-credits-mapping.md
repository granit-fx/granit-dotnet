---
title: "ADR-035: Granit.CustomerBalance ↔ ORB Credits term-by-term mapping"
description: "Document the conceptual mapping between the existing Granit.CustomerBalance module and ORB's Credits feature, formalize the additions made to close the residual gap (admin debit endpoint, pre-expiration notification job), and call out the deliberate non-goals (drawdown ordering, period quotas, distributed sagas)."
sidebar:
  order: 35
  label: "035 - CustomerBalance ↔ ORB Credits"
---

> **Date:** 2026-04-25
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.CustomerBalance`, `Granit.CustomerBalance.Endpoints`,
> `Granit.CustomerBalance.BackgroundJobs`,
> `Granit.CustomerBalance.EntityFrameworkCore`

## Context

`Granit.CustomerBalance` shipped before the ORB-alignment audit
(EPIC #1155). When the audit surveyed each SaaS module against ORB feature
parity, CustomerBalance came back at **85–90% coverage** of ORB's
Credits feature — close enough that recreating the module from scratch
would be wasteful, but with three concrete gaps that would force every
adopter to rebuild the same workarounds:

1. **No admin debit endpoint.** Debits happened only through the
   pre-payment processor (invoice deduction). Manual corrections,
   scheduled drawdowns, and non-invoice adjustments required a SQL
   surgery.
2. **No pre-expiration warning.** Promotional credits expired silently
   on the configured date. ORB notifies the tenant N days before
   expiration so they can use the credit or contact support; without
   this, expiring credits surprise everyone.
3. **No documented mapping.** Adopters integrating with ORB-style
   billing flows had no reference for "what's the Granit name for an
   ORB Drawdown?" — every team rederived it.

The decision: **extend in place**, not rewrite. Document the mapping,
ship the two missing operations, and explicitly enumerate the
non-goals so future contributors don't re-relitigate them.

## Decision

### 1. Term-by-term mapping (the existing 85% surface)

| ORB concept | Granit concept | Notes |
| ----------- | -------------- | ----- |
| Customer | `BalanceAccount` | Per-tenant aggregate root, keyed by `(TenantId, Currency)` (composite business key, unique index) |
| Currency-scoped balance | `BalanceAccount.Balance` | Cached running total; protected by `IConcurrencyAware` (optimistic concurrency token) |
| Credit grant | `BalanceTransaction { Type = Credit }` | Append-only entry; never updated or deleted |
| Drawdown / consumption | `BalanceTransaction { Type = Debit }` | Append-only entry; idempotent via `(ReferenceId, Source)` lookup |
| Credit source — Promotional | `TransactionSource.Promotional` | |
| Credit source — Customer overpayment | `TransactionSource.Overpayment` | Auto-credited by `OverpaymentCreditHandler` consuming `OverpaymentDetectedEto` |
| Credit source — Manual adjustment | `TransactionSource.ManualAdjustment` | Admin-driven |
| Credit source — Refund | `TransactionSource.RefundCredit` | |
| Drawdown — Invoice deduction | `TransactionSource.InvoiceDeduction` | Auto-applied by `CustomerBalancePrePaymentProcessor` (`IInvoicePrePaymentProcessor` implementation) |
| Drawdown — Expiration | `TransactionSource.Expiration` | Auto-applied by `CreditExpirationScanJob` (`0 */6 * * *` cron) |
| Credit expiration policy | `BalanceTransaction.ExpiresAt` | Per-credit (`null` for non-expiring) |
| Credit creation event | `BalanceCreditedEto` | Wolverine integration event |
| Credit consumption event | `BalanceDebitedEto` | Wolverine integration event |
| Credit expiration event | `CreditExpiredEto` | Wolverine integration event, raised by the expiration job |
| Strategy injection — billing flow | `IInvoicePrePaymentProcessor` | Without CustomerBalance: `PassThroughPrePaymentProcessor` (full amount). With CustomerBalance: `CustomerBalancePrePaymentProcessor` (deducts credit first). Wired via `services.Replace(...)` |

The mapping is term-by-term, not behavior-by-behavior — naming choices
were made for Granit's broader vocabulary (`BalanceAccount` instead of
`Customer` because Granit already has a `Customer` aggregate in
upstream tenant-management), but the operations are 1:1.

### 2. Additions to close the residual gap

**`POST /api/granit/customer-balance/balance/debit`** (admin debit) —
allows manual drawdown for corrections, scheduled debits, and non-invoice
adjustments. Idempotent via `(ReferenceId, Source = ManualAdjustment)`
lookup: a previous debit with the same `ReferenceId` short-circuits the
call. Permission: `CustomerBalance.Credits.Manage` (the existing one;
no new permission needed because debit is the inverse of credit and
both are `Manage`-level destructive operations).

**`CreditPreExpirationScanJob`** (`0 9 * * *` daily) — scans for credits
expiring within `CustomerBalanceOptions.PreExpirationWarningDays`
(default 7) and publishes `CreditNearExpirationEto` per (tenant, credit)
pair. Idempotent via `BalanceTransaction.LastPreExpirationNoticedAt`:
a credit already noticed on the current calendar day won't generate a
duplicate event. Notification routing (email / in-app / Slack) lives in
the consuming app, not in Granit.

### 3. Idempotency strategy: lookup-then-act, not saga

The transactional risk surfaced during the audit — *what if the wallet
debit succeeds but the invoice credit-applier fails?* — is closed by:

1. **Lookup before debit.** `(ReferenceId, Source = InvoiceDeduction)`
   uniquely identifies a debit-for-an-invoice. If the lookup finds an
   existing row, the processor skips the debit and proceeds to the
   credit-application step. Wolverine's at-least-once retry replays
   the whole flow safely.
2. **Same UoW for debit + credit-applier.** The
   `CustomerBalancePrePaymentProcessor` and the `IInvoiceCreditApplier`
   share the same EF Core `DbContext` and commit together. A partial
   failure is impossible under the standard transaction guarantees.

A distributed saga (compensating debit on credit-application failure)
was considered and rejected: the lookup + same-UoW pattern eliminates
the failure mode that a saga would compensate for, and adds zero
Wolverine surface.

## Consequences

### Positive

- **Same module, broader surface.** Adopters integrating against ORB
  semantics can now use `Granit.CustomerBalance` without writing any
  custom code for admin drawdown or pre-expiration warnings.
- **Documented vocabulary.** The mapping table is the authoritative
  reference for "where does X from ORB live in Granit?" — eliminates
  per-team rediscovery.
- **No new permissions.** `Credits.Manage` covers both directions;
  smaller permission surface is easier to grant correctly.

### Negative

- **Naming asymmetry.** ORB calls them "credits"; Granit's resource
  permissions still say `Credits.Manage` even though the new endpoint
  is a debit. Kept this way deliberately — it's the customer-balance
  manage permission, the verb is implementation detail.
- **Notification routing left to the app.** The pre-expiration job
  publishes the event but doesn't send the email itself. Consequence:
  app authors must wire `CreditNearExpirationEto` to a notification
  channel; documented in `saas/customer-balance.mdx`.

### Non-goals (explicitly out of scope)

- **No drawdown ordering / FIFO / expiry-first policies.** ORB lets
  admins configure which credit gets consumed first. Granit deducts
  from the running total and selects which credit "expired" through
  the order of `BalanceTransaction.ExpiresAt`. The drawdown-ordering
  feature would require a per-credit balance ledger; revisit if the
  use case appears.
- **No period-bound quotas.** ORB caps "$X per month from this credit
  source"; Granit treats credits as a single bucket. The same effect is
  achievable by issuing N monthly credits.
- **No distributed saga.** See §3 above — the lookup + same-UoW
  pattern eliminates the failure mode.
- **No three-way reconciliation with the PSP.** Granit doesn't sync
  `BalanceAccount` state into Stripe Customer Balance. Adopters who
  need cross-system reconciliation can subscribe to the events and
  push to the PSP themselves.

## References

- ADR-017 — DDD Aggregate Root vs Entity (`BalanceAccount` is an
  AuditedAggregateRoot, `BalanceTransaction` is an Entity)
- ADR-032 — `Granit.Catalog.Product` (related: cross-module soft
  references, `IInvoicePrePaymentProcessor` strategy pattern)
- ORB Credits documentation (alignment target)
- Stripe Customer Balance (related but not synced — see Non-goals)
