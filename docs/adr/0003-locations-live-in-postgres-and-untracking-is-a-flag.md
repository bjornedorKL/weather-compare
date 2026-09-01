# Locations live in Postgres, and untracking is a flag

The **Catalogue** becomes a `locations` table in Postgres. `locations.json` is demoted to seed data applied on first run; after that the table is the truth. Untracking a Location sets a flag — the row, its name and its coordinate survive.

## Why

The Catalogue was an immutable singleton loaded once at startup, so adding or removing a Location was impossible without editing a file and restarting. Making it mutable required somewhere to put it.

Rewriting `locations.json` was rejected on a concrete detail: it is loaded from `AppContext.BaseDirectory`, so the file the application reads is the copy in `bin/`, which the next `dotnet build` overwrites. Writing there loses data silently. Writing to the source tree instead means the running application needs to know where its own source lives, which is wrong the moment it is published anywhere.

Deleting rows on untrack was rejected because the Snapshots for that Location survive regardless — the store cannot delete — so a hard delete leaves Snapshots referencing a coordinate nothing describes. A flag also makes re-tracking one click instead of a coordinate you have to find again, which matters because untrack-then-retrack is the common case and adding a genuinely new place is the rare one.

## Consequences

- The Catalogue stops being a singleton. Anything holding one must resolve it per scope — the poller already creates a scope per unit of work, so this is contained.
- The read API's anti-join between an in-memory catalogue and the snapshot table collapses into a plain join, which is simpler than what it replaces.
- Every query over the Catalogue must filter on the flag. Forgetting to means the poller silently resumes asking about Locations you removed.
- **Two boundaries, two precision rules.** The seed loader *rejects* coordinates with more than 4 decimals, because silently moving a hand-written entry would change which Location it is. A coordinate typed or pasted into the page is *truncated* to 4, because rejecting a phone's 7-decimal reading would be hostile. This asymmetry is deliberate; it is not an inconsistency to be tidied away.
