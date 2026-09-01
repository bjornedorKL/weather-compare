import type { Forecast } from './api.ts'

/**
 * Reading a whole Snapshot rather than the handful a card shows. Two properties of MET's
 * Forecasts drive everything here:
 *
 * - The step spacing coarsens down the range — one hour near-term, six hours further out — so
 *   `precipitationMillimetres` changes the window it is measured over partway through. Totals
 *   may be added up (the windows do not overlap), but the numbers may never be compared to
 *   each other without their window, and plotting them as one series would draw a cliff where
 *   only the unit changed. `precipitationIntensity` is what may be plotted; `periodHours` is
 *   what must be shown next to every raw millimetre.
 * - The final Forecast summarises no period at all: null `symbol`, null precipitation, null
 *   `periodHours`. It is an instant, not a window, and every stored payload has exactly one.
 */

/** A local calendar day's worth of steps, so the timeline can be broken up where a reader reads it. */
export type ForecastDay = {
  /** `2026-09-01` in the reader's own timezone — the grouping key, not something displayed. */
  key: string
  /** Local midnight that opens the day. */
  start: Date
  steps: Forecast[]
}

/** What a day's steps add up to. Nulls survive: a day we know nothing about must not read as zero. */
export type DaySummary = {
  lowestCelsius: number | null
  highestCelsius: number | null
  /** Total over the windows that *start* on this day; null when no step carried a figure. */
  millimetres: number | null
  /** Every window length the day is measured in, ascending. Empty when only the instant falls here. */
  periodHours: number[]
}

/** Where the Provider's step spacing coarsens, which is a change of units and must be visible. */
export type ResolutionChange = {
  at: Date
  fromHours: number
  toHours: number
}

function dayKey(moment: Date): string {
  const month = `${moment.getMonth() + 1}`.padStart(2, '0')
  const day = `${moment.getDate()}`.padStart(2, '0')

  return `${moment.getFullYear()}-${month}-${day}`
}

/** Local midnight opening the day `moment` falls in. */
function startOfDay(moment: Date): Date {
  return new Date(moment.getFullYear(), moment.getMonth(), moment.getDate())
}

/** The Forecasts split into local days, in order, each keeping its steps in order. */
export function groupByDay(forecasts: readonly Forecast[]): ForecastDay[] {
  const days: ForecastDay[] = []

  for (const forecast of forecasts) {
    const validAt = new Date(forecast.validAt)
    const key = dayKey(validAt)
    const last = days[days.length - 1]

    if (last !== undefined && last.key === key) {
      last.steps.push(forecast)
    } else {
      days.push({ key, start: startOfDay(validAt), steps: [forecast] })
    }
  }

  return days
}

/** The moment a step's window closes, or null for the instant that closes the Snapshot. */
export function stepEnd(forecast: Forecast): Date | null {
  if (forecast.periodHours === null) {
    return null
  }

  return new Date(Date.parse(forecast.validAt) + forecast.periodHours * 3_600_000)
}

/**
 * Millimetres per hour: the one form of a step's precipitation that means the same thing at
 * every point in the range, and so the only one that may be drawn as a single series.
 */
export function precipitationIntensity(forecast: Forecast): number | null {
  const { precipitationMillimetres: millimetres, periodHours } = forecast

  if (millimetres === null || periodHours === null || periodHours <= 0) {
    return null
  }

  return millimetres / periodHours
}

export function summariseDay(steps: readonly Forecast[]): DaySummary {
  let lowest: number | null = null
  let highest: number | null = null
  let millimetres: number | null = null
  const periods = new Set<number>()

  for (const step of steps) {
    if (step.temperatureCelsius !== null) {
      lowest = lowest === null ? step.temperatureCelsius : Math.min(lowest, step.temperatureCelsius)
      highest = highest === null ? step.temperatureCelsius : Math.max(highest, step.temperatureCelsius)
    }

    if (step.precipitationMillimetres !== null) {
      millimetres = (millimetres ?? 0) + step.precipitationMillimetres
    }

    if (step.periodHours !== null) {
      periods.add(step.periodHours)
    }
  }

  return {
    lowestCelsius: lowest,
    highestCelsius: highest,
    millimetres,
    periodHours: [...periods].sort((a, b) => a - b),
  }
}

/** Every point where the window length changes. The closing instant's null is not a change. */
export function resolutionChanges(forecasts: readonly Forecast[]): ResolutionChange[] {
  const changes: ResolutionChange[] = []
  let previous: number | null = null

  for (const forecast of forecasts) {
    if (forecast.periodHours === null) {
      continue
    }

    if (previous !== null && forecast.periodHours !== previous) {
      changes.push({
        at: new Date(forecast.validAt),
        fromHours: previous,
        toHours: forecast.periodHours,
      })
    }

    previous = forecast.periodHours
  }

  return changes
}
