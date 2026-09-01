/**
 * What `GET /api/locations` returns, mirroring the API's own records. Every measurement is
 * nullable because a Provider may say nothing about it: MET's last timestep, for instance,
 * carries an instant and no precipitation block at all.
 */
export type Forecast = {
  /** The future moment this Forecast describes, ISO 8601 with an offset. */
  validAt: string
  temperatureCelsius: number | null
  windSpeedMetresPerSecond: number | null
  /** The direction the wind blows *from*, degrees clockwise from north. */
  windFromDirectionDegrees: number | null
  /** Precipitation over `periodHours`, not over an hour unless `periodHours` says so. */
  precipitationMillimetres: number | null
  /** MET Norway's symbol vocabulary (ADR-0002). Null when the Provider summarised no period. */
  symbol: string | null
  /** The length of the window `symbol` and `precipitationMillimetres` describe. */
  periodHours: number | null
}

/** One Provider's newest Forecast Snapshot for a Location. */
export type ForecastSnapshot = {
  provider: string
  /** When we asked the Provider — not a moment any Forecast inside describes. */
  issuedAt: string
  forecasts: Forecast[]
}

/** A Location, with the newest Snapshot from each Provider. Empty for one never asked about. */
export type LocationForecasts = {
  name: string
  latitude: number
  longitude: number
  altitude: number
  snapshots: ForecastSnapshot[]
}

export async function fetchLocations(signal?: AbortSignal): Promise<LocationForecasts[]> {
  const response = await fetch('/api/locations', { signal })

  if (!response.ok) {
    throw new Error(`GET /api/locations answered ${response.status}`)
  }

  return (await response.json()) as LocationForecasts[]
}
