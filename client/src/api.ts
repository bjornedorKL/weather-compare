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

/**
 * A Location we know, tracked or not — what `GET /api/locations/known` returns. `id` is how the
 * page names a Location when tracking or untracking it, so it never has to spell the coordinate
 * out; identity is still the coordinate, and this is only a handle onto it.
 */
export type KnownLocation = {
  id: number
  name: string
  latitude: number
  longitude: number
  altitude: number
  /** Whether it is in the Catalogue right now. Untracked ones are still known, and still here. */
  tracked: boolean
}

/** A Location as someone types it: nulls where a field was left empty, for the API to reject. */
export type TypedLocation = {
  name: string
  latitude: number | null
  longitude: number | null
  altitude: number | null
}

/**
 * The Location the Catalogue now holds, and whether tracking it added a row. `created` is false
 * when that coordinate was already known — under the typed name or a different one.
 */
export type TrackedCoordinate = {
  location: KnownLocation
  created: boolean
}

type ProblemDetails = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

/**
 * A write the API turned down, in the API's own words. A 400 names the offending fields and a
 * 404 explains the id; either way the page repeats what it was told rather than guessing at a
 * wording of its own, which would drift from what the API actually enforces.
 */
export class WriteRefused extends Error {
  /** Field name as the API spelled it (`Latitude`) to its first message, for use beside inputs. */
  readonly fields: Readonly<Record<string, string>>

  constructor(message: string, fields: Record<string, string>) {
    super(message)
    this.name = 'WriteRefused'
    this.fields = fields
  }
}

async function refusal(response: Response): Promise<WriteRefused> {
  let problem: ProblemDetails = {}

  try {
    problem = (await response.json()) as ProblemDetails
  } catch {
    /* Not problem+json. The status line below is then all we can honestly report. */
  }

  const fields: Record<string, string> = {}

  for (const [field, messages] of Object.entries(problem.errors ?? {})) {
    if (messages.length > 0) {
      fields[field] = messages[0]
    }
  }

  const spelled = Object.values(fields)
  const said = spelled.length > 0 ? [problem.title, ...spelled] : [problem.title, problem.detail]
  const message = said.filter((part) => part !== undefined).join(' ')

  return new WriteRefused(message === '' ? `The API answered ${response.status}.` : message, fields)
}

export async function fetchKnownLocations(signal?: AbortSignal): Promise<KnownLocation[]> {
  const response = await fetch('/api/locations/known', { signal })

  if (!response.ok) {
    throw new Error(`GET /api/locations/known answered ${response.status}`)
  }

  return (await response.json()) as KnownLocation[]
}

/**
 * Puts a Location we already know into the Catalogue, or takes it out. Untracking stops future
 * Forecast Snapshots; it deletes nothing, and the Location stays known so this can be undone.
 */
export async function setTracked(id: number, tracked: boolean): Promise<KnownLocation> {
  const response = await fetch(`/api/locations/${id}/${tracked ? 'track' : 'untrack'}`, {
    method: 'POST',
  })

  if (!response.ok) {
    throw await refusal(response)
  }

  return (await response.json()) as KnownLocation
}

/** Tracks a typed coordinate. 201 means the coordinate was new, 200 that we already knew it. */
export async function trackCoordinate(typed: TypedLocation): Promise<TrackedCoordinate> {
  const response = await fetch('/api/locations', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(typed),
  })

  if (!response.ok) {
    throw await refusal(response)
  }

  return {
    location: (await response.json()) as KnownLocation,
    created: response.status === 201,
  }
}

/**
 * A candidate coordinate a name search offered — what `GET /api/locations/search?q=` returns.
 * A Match is not a Location: nothing is stored when one is offered, and picking one only fills
 * the track form, which is what turns it into a Location if it is then tracked (CONTEXT.md).
 * `admin1` is the gazetteer's word for the first-level region; the page shows it as one.
 */
export type Match = {
  name: string
  admin1: string | null
  country: string | null
  /** Metres above sea level, from the gazetteer rather than from a person (ADR-0004). */
  elevation: number
  latitude: number
  longitude: number
}

/**
 * Matches for a name. An empty array is a search that ran and found nothing; a throw is a search
 * that could not run, which the page reports as such because typing a coordinate still works.
 * The error is a plain one, not a `WriteRefused` — nothing was written to refuse — but it is
 * worded from the same problem details, so the page still repeats what the API said.
 */
export async function searchMatches(query: string, signal?: AbortSignal): Promise<Match[]> {
  const response = await fetch(`/api/locations/search?q=${encodeURIComponent(query)}`, { signal })

  if (!response.ok) {
    throw new Error((await refusal(response)).message)
  }

  return (await response.json()) as Match[]
}
