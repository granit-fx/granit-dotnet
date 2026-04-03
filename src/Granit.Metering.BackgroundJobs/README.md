# Granit.Metering.BackgroundJobs

Background jobs for Granit.Metering.

## Jobs

| Job | Cron | Description |
| --- | ---- | ----------- |
| `MeteringAggregationJob` | `0 */1 * * *` | Hourly rollup via watermark cursor |
| `QuotaThresholdCheckJob` | `*/15 * * * *` | Quota alerts at 80% and 100% |
