# Vendored weather icons

The SVGs in `svg/` are MET Norway's weather icon set, copied verbatim from
<https://github.com/metno/weathericons> (`weather/svg/`, `main` branch, fetched 2026-09-01).

They are keyed by exactly the **Symbol** names the API hands us — that is the payoff of
ADR-0002, and the reason there is no mapping table between a Symbol and its picture.

## Licence

The icons are copyright (c) 2015–2017 Yr and licensed under the MIT Licence. The upstream
licence text is kept beside them in `LICENSE`, unaltered.

The whole set (83 files) is vendored rather than a subset, because which Symbols arrive
depends on the weather, not on us — a snow Symbol in January must not arrive to find its
icon was never copied. They are vendored, not hotlinked, so the page does not depend on
GitHub being up.

Attribution is rendered on the page itself, in the footer, alongside MET Norway's own
CC BY 4.0 / NLOD attribution for the forecast data.
