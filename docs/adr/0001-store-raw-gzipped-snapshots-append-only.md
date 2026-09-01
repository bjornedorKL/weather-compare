# Store raw Provider payloads, gzipped and append-only

A **Forecast Snapshot** stores the Provider's response verbatim (MET Norway's `compact` JSON), gzipped, in an append-only table keyed by (Provider, Location, Issued At). Nothing is ever updated or deleted. Normalisation into a common cross-Provider shape happens on read, in code.

## Why

Normalisation is where the bugs live, especially when mapping a second Provider into a shape derived from the first. Storing normalised rows bakes those bugs into history that cannot be re-derived; storing raw means a mapping bug is fixed by changing code and reprocessing. The reversibility is the point.

Storing raw was rejected on size grounds until measured: `compact` is 36 KB raw but **2.6 KB gzipped**, giving roughly 680 MB/year at 30 Locations refreshed hourly — and far less in practice, because `If-Modified-Since` returns `304` with no body and no write. Filtering fields before storing would have saved storage we do not need at the cost of irreversibility.

`compact` over `complete` is a deliberate, honest narrowing at the Provider's own API level: it carries temperature, wind, precipitation and symbol code — everything the page renders — at ~2.5x less than `complete`. Switching later is a one-line change; only Snapshots written before the switch will lack the extra fields.

## Consequences

- Queries over forecast values cannot be plain SQL — reads decompress and parse. Acceptable at a few dozen Locations; revisit if read latency ever actually hurts.
- Adding a Provider means writing a mapping into the common shape, not a migration.
- Storage grows monotonically. If it ever matters, thin old Snapshots (keep one per day beyond N days) rather than dropping fields.
