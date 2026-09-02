# Locations are found by name, and always have an altitude

A Location can be found by typing its name, and by asking the browser where you are. Both routes fill in the same form that exists today — a **Match** is not a Location until it is tracked. Altitude stays required, and comes from the lookup rather than from a person.

This reverses an earlier decision. `TrackLocation.tsx` and `TrackLocationRequest.cs` both stated that a coordinate is "given, not found" and that a geocoder had been decided against. That decision was recorded only in those two doc comments, with no reasoning attached, so what it traded away is no longer knowable. It is reversed here deliberately rather than by accident.

## Why

Typing four decimals of latitude is not something anyone can do from memory. It makes adding a Location a research task — open a map, find the point, copy two numbers and a height — for what is conceptually "watch the weather in Bergen". The Catalogue became mutable in ADR-0003 so that adding a Location was possible; this is what makes it *usable*.

**Altitude stays required, even though MET says it is optional.** MET's documentation is explicit about what happens without it: "the internal topography model is used for temperature correction, which is rather coarse and may be incorrect in hilly terrain." The seed Catalogue is Finse at 1222 m, Geilo at 794 m, Røros at 628 m — hilly terrain is not an edge case here, it is the interesting part of the data. A nullable altitude would also add a "we did not bother to look" state to every read path (`LocationForecastReader`, `ForecastHistoryReader`, `ForecastPollingCycle`, both views) in exchange for nothing, because the lookup supplies an altitude for free.

**Open-Meteo supplies both halves.** Its geocoding API returns `elevation` alongside the coordinate on every result, so search satisfies the altitude rule in the same response. Its elevation API (Copernicus DEM GLO-90, 90 m) covers the other route, where the browser gives a coordinate and nothing else. Neither needs a key for non-commercial use. One vendor for both is a convenience, not a principle: each is a separate call behind a separate endpoint of ours, and either can be replaced alone.

**The browser's own altitude is never used.** `GeolocationCoordinates.altitude` is height above the WGS84 ellipsoid, not above sea level — in Norway a systematic error of roughly 40 m, in the one field we just decided is load-bearing for temperature. It is also null on any device positioning by wi-fi. Looking the elevation up from a coordinate is both more accurate and more reliable than the reading the device offers.

## Considered and rejected

**Calling the gazetteer from the browser.** Simpler, and defensible given that search is UI scaffolding rather than domain. Rejected because `vite.config.ts` names one-origin as a deliberate property, because every other external service here is reached server-side with a `User-Agent` we control — MET rejects requests without a distinctive one — and because going through our API keeps the gazetteer's twenty-field response shape out of the client, so swapping gazetteers later stays a backend change.

**Reverse geocoding the coordinate into a name.** Open-Meteo's geocoding is forward-only, so this would mean a third service under a stricter usage policy (Nominatim, 1 request/second). What it returns for "where I am standing" is an administrative label — a district, a road — and the name wanted for a Location you are standing at is "Home" or "Cabin". Since renaming is now possible, the guess would usually be corrected rather than kept. Not worth a third dependency; revisit if naming turns out to be the friction.

**Letting a track under a new name rename the Location.** Tracking a coordinate we already know keeps the name on file, as it does today (`LocationTracking.cs:79`). Renaming is now a separate, explicit act, which is what makes keeping that rule safe rather than a trap.

## Consequences

- Two external services beyond MET. Both are best-effort: if either is unreachable, the Location cannot be found by name, but the app is otherwise unaffected. **The hand-typed coordinate form stays** — it is the fallback, and the only route that works offline.
- GeoNames data is CC-BY. The README's Attribution section gains Open-Meteo and GeoNames alongside MET.
- Searching is a read that creates nothing. `GET /api/locations/search` and the elevation lookup add routes to an API that is otherwise about the Catalogue; they are honest about returning Matches, not Locations.
- Renaming becomes possible for the first time. It is safe by construction: `ForecastSnapshot` stores a coordinate and no name, and the seeder only ever inserts coordinates it does not know, so a rename survives restarts and touches no history.
