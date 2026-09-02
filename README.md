# Weather Comparison

A page showing what weather **Providers** predict for a handful of **Locations**, backed by an
append-only store of every response those Providers have ever given us.

Built as a learning project for two things: driving GitHub through its MCP server, and .NET / C#.

## Status

Running locally. The backend polls, stores and serves; the page renders.

- A card per tracked Location, click through to the full forecast timeline.
- Locations are tracked and untracked from the page; the Catalogue lives in Postgres.
- A Location can be found by name: searching offers **Matches**, and picking one fills the
  track form. Typing a coordinate by hand still works, and is the route that needs no network.
- Snapshot history shows how the forecast for a given hour moved between Snapshots.
- 105 tests. CI builds and tests the API and the client on every push and pull request.

MET Norway is still the only Provider, so nothing is compared against anything yet.

- [CONTEXT.md](./CONTEXT.md) — the domain language. Read this first.
- [docs/adr/](./docs/adr/) — decisions and why they were made.

## Shape

- **Backend** — ASP.NET Core (.NET 9), single project, EF Core against Postgres in Docker.
  A background poller walks the Location catalogue on a schedule, sends `If-Modified-Since`,
  and appends a **Forecast Snapshot** only when the Provider returns `200`.
- **Frontend** — React (Vite) in `client/`. A card per Location: symbol, temperature,
  wind, precipitation, and the next few hours.
- **Provider** — MET Norway [Locationforecast 2.0](https://docs.api.met.no/doc/locationforecast/HowTO.html)
  is the only implementation. Adding a second is a design requirement, not yet a feature.
- **Hosting** — local only. The poller runs when this machine runs, so the stored history
  has gaps and cannot be backfilled.

## Attribution

Weather data from [MET Norway](https://api.met.no/), licensed
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) / [NLOD](https://data.norge.no/nlod/en/2.0).

Location search by [Open-Meteo](https://open-meteo.com/en/docs/geocoding-api), over the
[GeoNames](https://www.geonames.org/) gazetteer — both licensed
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
