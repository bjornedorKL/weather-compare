# Weather Comparison

A page that shows what weather **Providers** predict for a handful of **Locations**, backed by a store of everything those Providers have ever told us — so we can show forecasts without re-asking the Providers on every page load, and later compare Providers against each other.

## Language

**Provider**:
An organisation that publishes weather predictions, identified by name (e.g. MET Norway). Not the endpoint it exposes.
_Avoid_: API, service, source, backend

**Location**:
A point on earth we track weather for, identified by its coordinate at the precision the Provider accepts. Its name is a human label we attach for display — two Locations with the same coordinate are the same Location regardless of what they are called, and renaming one changes nothing about which Location it is.
_Avoid_: place, city, station, position

**Catalogue**:
The set of **Locations** we currently track. A Location enters it when tracked and leaves when untracked. Untracking stops Providers being asked about that Location and freezes its history; it never removes **Forecast Snapshots** already recorded, and the Location remains known, so it can be tracked again without being described afresh.
_Avoid_: list, places, watchlist, subscriptions

**Forecast**:
A statement about what the weather will be at a **Location** at some future moment. A Provider issues many Forecasts at once, covering a range of future moments.
_Avoid_: prediction, weather data

**Forecast Snapshot**:
An immutable record of everything one **Provider** said about one **Location** at one moment in time. Snapshots are never updated or deleted — a refresh writes a new Snapshot alongside the old ones. The newest Snapshot for a (Provider, Location) pair is what the page shows.
_Avoid_: cache entry, reading, sample, record

**Symbol**:
The canonical name for what the weather looks like at a given moment (`clearsky_day`, `sleet`, `heavyrainandthunder`). The vocabulary is MET Norway's symbol set; every Provider maps into it.
_Avoid_: icon, condition, weather type, code

**Issued At**:
The moment a **Forecast Snapshot** was captured from its Provider. Distinct from the future moments the Forecasts inside it describe.
_Avoid_: timestamp, created, date

## Flagged ambiguities

**"Compare data"** — resolved as *comparing Providers to each other*, not comparing a Provider to reality. Scoring which Provider was closest to what actually happened is explicitly out of scope for now.

**Observation** — what the weather *actually was*, as opposed to what was forecast. Deliberately not a term in this context yet, because nothing produces one. If forecast-accuracy scoring ever comes in scope, this is the term to introduce, and it must not be conflated with **Forecast**.

**Nearest station** — not a concept here. Locationforecast is grid-based: it answers for *any* coordinate, so there is no set of available points to be closest to. Nearest-station matching belongs to observation services (MET's Frost), which are out of scope. Do not let "find the closest location" re-enter the design.

**Match** — what a name search offers: a candidate coordinate from a gazetteer, carrying a name and an elevation. A Match is not a **Location** and does not become one by being shown; it becomes one only if it is tracked, and most Matches are discarded unlooked-at. Do not call a Match a place, and do not model it as a Location that happens not to be tracked yet — an untracked Location is a different thing entirely, one we have history for.

**Canonical vocabulary is one Provider's** — **Symbol** uses MET Norway's names for every Provider. A deliberate shortcut, not a principle (see ADR-0002). If a Provider's conditions will not map into MET's set, that is the trigger to introduce a neutral vocabulary.

**History has gaps** — the poller runs locally, so Snapshots exist only for periods when the machine was awake. Gaps cannot be backfilled: a Provider can never be asked what it said last Tuesday. Any comparison across Providers is a comparison over an incomplete record, and must not be read as continuous.

**Removing is untracking, not deleting** — taking a Location out of the **Catalogue** stops future Snapshots being recorded for it. Every Snapshot already recorded survives, permanently and by construction: the store rejects deletes outright. A Location tracked again later resumes with an unbackfillable gap in the middle of its history.

**Card** — not a domain term. A card is how the page draws a **Location**; there is no such thing as adding, removing or clicking a Card in the domain. Say Location.

## Example dialogue

> **Dev**: The page is slow, can we just update the forecast in place instead of writing a new row every time?
>
> **Domain expert**: Then it isn't a Snapshot any more. The whole point is that a Snapshot is what a Provider told us at a moment — if you overwrite it, you've destroyed the record that they ever said it.
>
> **Dev**: But we only ever show the latest one.
>
> **Domain expert**: Today, yes. The page reads the newest Snapshot for that Provider and Location. But the older ones are why we can ever ask "did MET and the other one disagree last Tuesday?" Overwriting throws that away and we can't get it back.
>
> **Dev**: So a refresh is an append, never an update.
>
> **Domain expert**: Always. And note the Snapshot's Issued At is when *we asked* — it's not the time the weather happens. Every Snapshot contains Forecasts for lots of future moments.
