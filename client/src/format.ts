const COMPASS = [
  'N', 'NNE', 'NE', 'ENE', 'E', 'ESE', 'SE', 'SSE',
  'S', 'SSW', 'SW', 'WSW', 'W', 'WNW', 'NW', 'NNW',
]

const hourAndMinute = new Intl.DateTimeFormat(undefined, {
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

const fullMoment = new Intl.DateTimeFormat(undefined, {
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

const dayName = new Intl.DateTimeFormat(undefined, {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
})

const dayShort = new Intl.DateTimeFormat(undefined, {
  weekday: 'short',
  day: 'numeric',
})

const relative = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' })

/** Whole degrees. A card is a summary; the tenth of a degree is noise at this size. */
export function formatTemperature(celsius: number | null): string {
  return celsius === null ? '–' : `${Math.round(celsius)}°`
}

/** Millimetres without a trailing `.0`, so 0 reads as `0` and not `0.0`. */
export function formatMillimetres(millimetres: number): string {
  return millimetres.toFixed(1).replace(/\.0$/, '')
}

/**
 * Precipitation always carries the window it fell in: 0.5 mm over an hour and 0.5 mm over
 * twelve are different weather. With no window we cannot say what the number means, so we
 * do not print it.
 */
export function formatPrecipitation(
  millimetres: number | null,
  periodHours: number | null,
): string | null {
  if (millimetres === null || periodHours === null) {
    return null
  }

  return `${formatMillimetres(millimetres)} mm over ${periodHours} h`
}

export function formatWind(
  metresPerSecond: number | null,
  fromDegrees: number | null,
): string | null {
  if (metresPerSecond === null) {
    return null
  }

  const speed = `${metresPerSecond.toFixed(1)} m/s`

  return fromDegrees === null ? speed : `${speed} from ${compassPoint(fromDegrees)}`
}

/** The 16-point compass name for a bearing in degrees clockwise from north. */
export function compassPoint(degrees: number): string {
  const normalised = ((degrees % 360) + 360) % 360

  return COMPASS[Math.round(normalised / 22.5) % COMPASS.length]
}

/** `14:00`, in the reader's own timezone. */
export function formatClock(moment: Date): string {
  return hourAndMinute.format(moment)
}

/** `Tue 1 Sep, 14:00`, for the tooltip that spells out which day a strip column belongs to. */
export function formatMoment(moment: Date): string {
  return fullMoment.format(moment)
}

/** `Tuesday 1 September` — the heading that delineates one day of the timeline from the next. */
export function formatDayName(moment: Date): string {
  return dayName.format(moment)
}

/** `Tue 1`, for the day marks along a timeline where there is no room for the long form. */
export function formatDayShort(moment: Date): string {
  return dayShort.format(moment)
}

/**
 * The window a step's precipitation and Symbol describe: `1 h`, `6 h`, or — for the Forecast
 * that closes a Snapshot — no window at all, which is a fact about the data and not a gap.
 */
export function formatWindow(periodHours: number | null): string {
  return periodHours === null ? 'instant' : `${periodHours} h`
}

/**
 * Precipitation as a rate. Only rates may be compared across a range whose window length
 * changes, so this is what a chart plots and what its axis is labelled in.
 */
export function formatIntensity(millimetresPerHour: number): string {
  return `${millimetresPerHour.toFixed(1).replace(/\.0$/, '')} mm/h`
}

/**
 * How old a Snapshot's Issued At is, in words: `12 minutes ago`. A page that cannot say how
 * stale its data is, is lying by omission — and a bare timestamp makes the reader do the sum.
 */
export function formatAge(issuedAt: Date, now: Date): string {
  const seconds = Math.round((issuedAt.getTime() - now.getTime()) / 1000)

  if (Math.abs(seconds) < 60) {
    return 'just now'
  }

  const minutes = Math.round(seconds / 60)

  if (Math.abs(minutes) < 60) {
    return relative.format(minutes, 'minute')
  }

  const hours = Math.round(minutes / 60)

  if (Math.abs(hours) < 24) {
    return relative.format(hours, 'hour')
  }

  return relative.format(Math.round(hours / 24), 'day')
}

/** `59.9139°N, 10.7522°E` — the coordinate that is the Location's real identity. */
export function formatCoordinate(latitude: number, longitude: number): string {
  const northing = `${Math.abs(latitude).toFixed(4)}°${latitude < 0 ? 'S' : 'N'}`
  const easting = `${Math.abs(longitude).toFixed(4)}°${longitude < 0 ? 'W' : 'E'}`

  return `${northing}, ${easting}`
}
