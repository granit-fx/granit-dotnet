# Granit.Payments.SepaDirectDebit

SEPA Direct Debit abstractions for Granit.Payments.

## Providers

| Package | Strategy | Coverage |
| ------- | -------- | -------- |
| `.Internal` | Self-hosted PAIN.008 | Any SEPA bank |
| `.GoCardless` | GoCardless API | Pan-European, UK |
| `.Twikey` | Twikey API | BE, NL, FR, DE |

## Mandate lifecycle

Pending → Active → Cancelled / Suspended / Expired
