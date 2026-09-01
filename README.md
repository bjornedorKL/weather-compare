# Weather Comparison

A page showing what weather **Providers** predict for a handful of **Locations**, backed by an
append-only store of every response those Providers have ever given us.

Built as a learning project for two things: driving GitHub through its MCP server, and .NET / C#.

## Status

Design agreed, no code yet.

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
