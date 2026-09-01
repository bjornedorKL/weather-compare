import type { Forecast } from './api.ts'

/**
 * What successive Forecast Snapshots said about one future moment — the read the append-only
 * store exists for, and the only one that looks past the newest row.
 *
 * Two absences are kept apart here, because drawing them the same way would be a lie:
 *
 * - A **silent** Snapshot was recorded and said nothing about this moment. MET's steps lengthen
 *   to six hours down the range, so an older Snapshot may never have spoken about 14:00.
 * - A **gap** is a stretch where a Snapshot was due and none was recorded. The poller runs
 *   locally, so the record only covers the periods the machine was awake, and a gap can never
 *   be backfilled — a Provider cannot be asked, later, what it said (CONTEXT.md).
 *
 * Neither may be drawn through. A line joining the points either side of one would claim the
 * Provider held that value all the way across, which nothing in the store says.
 */

/** What one Snapshot said about the moment. `forecast` is null when it was silent. */
export type ForecastHistoryPoint = {
  /** When we asked — not the moment being asked about. */
  issuedAt: string
  forecast: Forecast | null
}

/** A stretch where a Snapshot was due and none was recorded. */
export type ForecastHistoryGap = {
  fromIssuedAt: string
  toIssuedAt: string
  /** When the Provider before the gap said to ask again: where the record fell silent. */
  dueAt: string
}

export type ProviderForecastHistory = {
  provider: string
  /** Oldest Issued first — the order a Forecast moves in. */
  points: ForecastHistoryPoint[]
  gaps: ForecastHistoryGap[]
}

export type ForecastHistory = {
  name: string
  latitude: number
  longitude: number
  altitude: number
  /** The moment every Forecast describes. Matched exactly, never nearest. */
  validAt: string
  /** How many Snapshot payloads the API decompressed and read to answer. */
  snapshotsRead: number
  providers: ProviderForecastHistory[]
}

export async function fetchForecastHistory(
  locationId: number,
  validAt: string,
  signal?: AbortSignal,
): Promise<ForecastHistory> {
  const moment = encodeURIComponent(validAt)
  const response = await fetch(`/api/locations/${locationId}/history?validAt=${moment}`, { signal })

  if (!response.ok) {
    throw new Error(`GET /api/locations/${locationId}/history answered ${response.status}`)
  }

  return (await response.json()) as ForecastHistory
}

/** A point that actually carries a Forecast, with its Issued At already parsed. */
export type PlottedPoint = {
  issuedAt: Date
  forecast: Forecast
  celsius: number
}

/**
 * A run of points that may be joined by a line: consecutive, each carrying a temperature, and
 * with no gap between them. Everything else is a break, and a break stays a break.
 */
export type HistorySegment = PlottedPoint[]

function precedesGap(point: ForecastHistoryPoint, gaps: readonly ForecastHistoryGap[]): boolean {
  return gaps.some((gap) => gap.fromIssuedAt === point.issuedAt)
}

/** The joinable runs, in order. A silent Snapshot or a gap ends the run it interrupts. */
export function segmentsOf(history: ProviderForecastHistory): HistorySegment[] {
  const segments: HistorySegment[] = []
  let current: HistorySegment = []

  for (const point of history.points) {
    const celsius = point.forecast?.temperatureCelsius ?? null

    if (point.forecast === null || celsius === null) {
      current = []
      continue
    }

    if (current.length === 0) {
      segments.push(current)
    }

    current.push({ issuedAt: new Date(point.issuedAt), forecast: point.forecast, celsius })

    if (precedesGap(point, history.gaps)) {
      current = []
    }
  }

  return segments.filter((segment) => segment.length > 0)
}

/** How the Provider's answer about this moment moved between its first Snapshot and its last. */
export type ForecastMove = {
  from: PlottedPoint
  to: PlottedPoint
  /** Degrees Celsius. Positive is warmer than it first said; this is a move, never an error. */
  degrees: number
  /** How long apart the two Snapshots were Issued, in milliseconds. */
  spanMilliseconds: number
  /** Every distinct Symbol the Provider has used for this moment, in the order it used them. */
  symbols: string[]
}

/** Null when fewer than two Snapshots carry a temperature: one point is not a movement. */
export function moveOf(history: ProviderForecastHistory): ForecastMove | null {
  const plotted = segmentsOf(history).flat()
  const from = plotted[0]
  const to = plotted[plotted.length - 1]

  if (from === undefined || to === undefined || plotted.length < 2) {
    return null
  }

  const symbols: string[] = []

  for (const point of plotted) {
    const symbol = point.forecast.symbol

    if (symbol !== null && symbols[symbols.length - 1] !== symbol) {
      symbols.push(symbol)
    }
  }

  return {
    from,
    to,
    degrees: to.celsius - from.celsius,
    spanMilliseconds: to.issuedAt.getTime() - from.issuedAt.getTime(),
    symbols,
  }
}
