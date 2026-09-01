import type { Forecast, ForecastSnapshot, LocationForecasts } from './api.ts'

/**
 * A Location is identified by its coordinate (CONTEXT.md) — the name is only a label — so the
 * coordinate is what the page keys a Location by and what it remembers when one is opened.
 */
export function locationKey(location: LocationForecasts): string {
  return `${location.latitude},${location.longitude}`
}

/**
 * The Snapshot a card shows. `snapshots` holds one entry per Provider; with a single Provider
 * there is at most one. Comparing Providers is out of scope, so when there are several the card
 * shows the most recently Issued one and names its Provider rather than trying to reconcile them.
 */
export function newestSnapshot(location: LocationForecasts): ForecastSnapshot | null {
  let newest: ForecastSnapshot | null = null

  for (const snapshot of location.snapshots) {
    if (newest === null || Date.parse(snapshot.issuedAt) > Date.parse(newest.issuedAt)) {
      newest = snapshot
    }
  }

  return newest
}

/**
 * Which Forecast describes the weather right now: the last one that has already begun. A Snapshot
 * is minutes to hours old by the time it is rendered, so its first Forecasts may be in the past —
 * this is deliberately not `forecasts[0]`. Falls back to the earliest Forecast when the whole
 * Snapshot still lies ahead of now, and to -1 when it holds no Forecasts at all.
 */
function currentIndex(forecasts: readonly Forecast[], now: Date): number {
  const moment = now.getTime()
  let index = -1

  for (let i = 0; i < forecasts.length; i++) {
    if (Date.parse(forecasts[i].validAt) <= moment) {
      index = i
    }
  }

  if (index === -1 && forecasts.length > 0) {
    return 0
  }

  return index
}

/** The Forecast for right now, or null when the Snapshot holds none. */
export function currentForecast(snapshot: ForecastSnapshot, now: Date): Forecast | null {
  const index = currentIndex(snapshot.forecasts, now)

  return index === -1 ? null : snapshot.forecasts[index]
}

/** The next `count` Forecasts after the current one. */
export function upcomingForecasts(
  snapshot: ForecastSnapshot,
  now: Date,
  count: number,
): Forecast[] {
  const index = currentIndex(snapshot.forecasts, now)

  return index === -1 ? [] : snapshot.forecasts.slice(index + 1, index + 1 + count)
}
