import type { FormEvent, ReactNode } from 'react'
import { useRef, useState } from 'react'
import type { KnownLocation, Match, TypedLocation } from './api.ts'
import { lookUpElevation, searchMatches } from './api.ts'
import { formatCoordinate } from './format.ts'

/**
 * The two ways a Location joins the Catalogue, in one panel above the grid.
 *
 * They are deliberately in that order. Tracking again something we already know is the common
 * act — untracking is reversible and meant to be reversed — while describing a Location we have
 * never seen is the rare one.
 *
 * That second way now has three routes into the same four fields: search a name and pick a Match,
 * ask the browser where you are, or type the coordinate out. Searching is the one anyone can do
 * from memory and locating is for the points no gazetteer has a name for (ADR-0004); typing stays
 * because it is the fallback when neither can be reached, and the only route that works with no
 * network at all.
 *
 * Collapsed by default: with thirty cards on the page, a form permanently in front of them is
 * noise. The summary carries the count of untracked Locations so the way back is visible without
 * opening anything.
 */

type Props = {
  untracked: KnownLocation[]
  /** Field name as the API spells it → what is wrong with it. */
  fieldProblems: Readonly<Record<string, string>>
  busy: boolean
  onTrackKnown: (id: number) => void
  onTrackTyped: (typed: TypedLocation) => void
}

export function TrackLocation({ untracked, fieldProblems, busy, onTrackKnown, onTrackTyped }: Props) {
  return (
    <details className="track">
      <summary className="track__summary">
        Track a Location
        {untracked.length > 0 && (
          <span className="track__count">{untracked.length} known, not tracked</span>
        )}
      </summary>

      {untracked.length > 0 && (
        <section className="track__section">
          <h2 className="track__heading">Known, but not tracked</h2>
          <p className="track__note">
            Their Forecast Snapshots are kept. Tracking one again resumes recording — the stretch
            it spent untracked stays a gap, and a gap can never be filled in afterwards.
          </p>
          <ul className="track__known">
            {untracked.map((location) => (
              <li key={location.id}>
                <button
                  className="track__again"
                  disabled={busy}
                  onClick={() => onTrackKnown(location.id)}
                  type="button"
                >
                  <span className="track__again-name">{location.name}</span>
                  <span className="track__again-coordinate">
                    {formatCoordinate(location.latitude, location.longitude)} · {location.altitude} m
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      <NewCoordinate busy={busy} fieldProblems={fieldProblems} onTrackTyped={onTrackTyped} />
    </details>
  )
}

/** An empty field is sent as null so the API is the one that says it was needed. */
function reading(typed: string): number | null {
  const trimmed = typed.trim()

  if (trimmed === '') {
    return null
  }

  const value = Number(trimmed)

  return Number.isNaN(value) ? null : value
}

function Field({ children, label, problem }: { children: ReactNode; label: string; problem?: string }) {
  return (
    <label className="field">
      <span className="field__label">{label}</span>
      {children}
      {problem !== undefined && (
        <span className="field__problem" role="alert">
          {problem}
        </span>
      )}
    </label>
  )
}

/** Where a name search has got to. `found` with nothing in it is a search that matched nothing. */
type Search =
  | { state: 'unasked' }
  | { state: 'searching' }
  | { state: 'found'; matches: Match[] }
  | { state: 'failed'; failure: string }

/** "Vestland, Norway · 12 m" — what separates the four Bergens from each other. */
function describe(match: Match): string {
  const where = [match.admin1, match.country].filter((part) => part !== null && part !== '').join(', ')
  const height = `${match.elevation} m`

  return where === '' ? height : `${where} · ${height}`
}

/**
 * Searching by name, and what it offered. A Match picked here only fills the form below — it is
 * not tracked, and nothing is written until the form is submitted, so a wrong pick costs a
 * second pick.
 *
 * Searching happens on submit rather than on every keystroke: the gazetteer is someone else's
 * service, and one request per name typed is what we would want asked of ours.
 *
 * A Match has no id of its own — nothing stores one, so there is nothing to give it an id — so
 * its name and coordinate are what tell two of them apart in the list.
 */
function FindByName({ busy, onPick }: { busy: boolean; onPick: (match: Match) => void }) {
  const [query, setQuery] = useState('')
  const [search, setSearch] = useState<Search>({ state: 'unasked' })

  async function run(event: FormEvent) {
    event.preventDefault()

    const name = query.trim()

    if (name === '') {
      return
    }

    setSearch({ state: 'searching' })

    try {
      setSearch({ state: 'found', matches: await searchMatches(name) })
    } catch (error) {
      setSearch({
        state: 'failed',
        failure: error instanceof Error ? error.message : 'The name search could not be reached.',
      })
    }
  }

  return (
    <>
      <form className="track__search" onSubmit={run}>
        <Field label="Name to search for">
          <input
            className="field__input"
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Bergen"
            type="search"
            value={query}
          />
        </Field>

        <button className="track__submit" disabled={search.state === 'searching'} type="submit">
          {search.state === 'searching' ? 'Searching…' : 'Search'}
        </button>
      </form>

      {search.state === 'failed' && (
        <p className="track__note track__note--problem" role="alert">
          {search.failure} Type the coordinate below instead — that route needs nothing but this page.
        </p>
      )}

      {search.state === 'found' && search.matches.length === 0 && (
        <p className="track__note" role="status">
          Nothing by that name. Try a different spelling, or type the coordinate below.
        </p>
      )}

      {search.state === 'found' && search.matches.length > 0 && (
        <ul className="track__matches">
          {search.matches.map((match) => (
            <li key={`${match.name},${match.latitude},${match.longitude}`}>
              <button
                className="track__match"
                disabled={busy}
                onClick={() => onPick(match)}
                type="button"
              >
                <span className="track__match-name">{match.name}</span>
                <span className="track__match-where">{describe(match)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}

/**
 * Whether the browser can be asked where it is at all. Geolocation only exists in a secure
 * context, so outside one the button is left out rather than offered and broken — there is
 * nothing a reader could do about it, and the two routes beside it work regardless.
 */
const CAN_BE_LOCATED =
  typeof navigator !== 'undefined' && 'geolocation' in navigator && window.isSecureContext

/**
 * The worst fix we will put in the form. Wi-fi and GPS land within tens of metres; a fix off by
 * more than a kilometre came from an IP address, and is a guess at a city rather than a position.
 * Since a Location *is* its coordinate, filling the form from one would create a Location that
 * is not where you are — in a different valley, at a different altitude, and the row survives
 * untracking. Refusing it and saying so leaves the two honest routes: search the name, or type
 * the coordinate.
 */
const COARSEST_FIX_METRES = 1000

/** Where asking the browser has got to. `placed` may still be waiting for the altitude. */
type Fix =
  | { state: 'unasked' }
  | { state: 'locating' }
  | { state: 'failed'; failure: string }
  | { state: 'placed'; accuracy: number; altitude: number | null; failure?: string }

/** `35 m`, `2.4 km` — how far out a fix may be, in a unit that does not need counting zeroes. */
function formatDistance(metres: number): string {
  return metres < 1000 ? `${Math.round(metres)} m` : `${(metres / 1000).toFixed(1)} km`
}

/** Four decimals, truncated, which is the precision the coordinate will be tracked at. */
function fourDecimals(degrees: number): string {
  return (Math.trunc(degrees * 10_000) / 10_000).toFixed(4)
}

function askTheBrowser(): Promise<GeolocationPosition> {
  return new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, {
      enableHighAccuracy: true,
      timeout: 15_000,
      // One reading, taken now. A cached fix from an hour ago is a different place.
      maximumAge: 0,
    })
  })
}

/**
 * `GeolocationPositionError`'s codes, which the specification fixes. Read as numbers rather than
 * off the error object or through `instanceof`, because the error's class is not a global in
 * every browser and a missing one would throw inside the handler for the failure.
 */
const PERMISSION_DENIED = 1
const POSITION_UNAVAILABLE = 2
const TIMEOUT = 3

/**
 * What went wrong, in the browser's three flavours of wrong. Each is a different act to take
 * next — grant the permission, go outside, or try again — so each gets its own sentence.
 */
function whyNot(error: unknown): string {
  switch ((error as GeolocationPositionError | undefined)?.code) {
    case PERMISSION_DENIED:
      return (
        'This page is not allowed to know where you are. Your browser asks per site, so the' +
        ' permission can be given there and this pressed again.'
      )
    case POSITION_UNAVAILABLE:
      return 'Your browser could not work out where you are — no satellite fix, and no network it recognises.'
    case TIMEOUT:
      return (
        'Your browser did not settle on a position within fifteen seconds. Pressing again' +
        ' usually works; indoors it may not.'
      )
    default:
      return 'Your browser would not say where you are, and did not say why.'
  }
}

/**
 * "Use my location", for the Locations no gazetteer knows the name of — a house, a cabin, the
 * field behind it. It fills the coordinate and the altitude and stops there: the name is typed,
 * because the name is a label we attach for display (CONTEXT.md) and the label wanted here is
 * "Home", not the administrative district a reverse geocoder would offer (ADR-0004).
 *
 * The altitude comes from the elevation model through our API, never from
 * `GeolocationCoordinates.altitude` — that is height above the WGS84 ellipsoid, some 40 m out in
 * Norway and null on any device positioning by wi-fi, in the one field the temperature forecast
 * leans on. The lookup failing costs the altitude and nothing else; it is a form field, so it
 * can be typed.
 *
 * Nothing is tracked here. This fills the form, and *Track this Location* is pressed after.
 */
function UseMyLocation({
  busy,
  onPlace,
  onMeasure,
}: {
  busy: boolean
  onPlace: (latitude: number, longitude: number) => void
  onMeasure: (metres: number) => void
}) {
  const [fix, setFix] = useState<Fix>({ state: 'unasked' })

  async function locate() {
    setFix({ state: 'locating' })

    let position: GeolocationPosition

    try {
      position = await askTheBrowser()
    } catch (error) {
      setFix({ state: 'failed', failure: whyNot(error) })

      return
    }

    const { accuracy, latitude, longitude } = position.coords

    if (accuracy > COARSEST_FIX_METRES) {
      setFix({
        state: 'failed',
        failure:
          `Your position is only known to within ${formatDistance(accuracy)}, which is a guess` +
          ' at a town rather than a point. Search for a name instead, or type the coordinate.',
      })

      return
    }

    const north = Number(fourDecimals(latitude))
    const east = Number(fourDecimals(longitude))

    onPlace(north, east)

    try {
      const metres = await lookUpElevation(north, east)

      onMeasure(metres)
      setFix({ state: 'placed', accuracy, altitude: metres })
    } catch (error) {
      setFix({
        state: 'placed',
        accuracy,
        altitude: null,
        failure: error instanceof Error ? error.message : 'The elevation lookup could not be reached.',
      })
    }
  }

  if (!CAN_BE_LOCATED) {
    return null
  }

  return (
    <div className="track__locate">
      <button
        className="track__submit"
        disabled={busy || fix.state === 'locating'}
        onClick={locate}
        type="button"
      >
        {fix.state === 'locating' ? 'Locating…' : 'Use my location'}
      </button>

      {fix.state === 'failed' && (
        <p className="track__note track__note--problem" role="alert">
          {fix.failure}
        </p>
      )}

      {fix.state === 'placed' && (
        <p
          className={`track__note${fix.altitude === null ? ' track__note--problem' : ''}`}
          role="status"
        >
          Where you are, to within {formatDistance(fix.accuracy)}.{' '}
          {fix.altitude === null
            ? `${fix.failure} Type the altitude in metres above sea level — it changes the` +
              ' temperature forecast.'
            : `The elevation model puts that point at ${fix.altitude} m above sea level;` +
              ' correct it below if you know better. Then name it and track it.'}
        </p>
      )}
    </div>
  )
}

function NewCoordinate({ busy, fieldProblems, onTrackTyped }: Omit<Props, 'untracked' | 'onTrackKnown'>) {
  const [name, setName] = useState('')
  const [latitude, setLatitude] = useState('')
  const [longitude, setLongitude] = useState('')
  const [altitude, setAltitude] = useState('')
  /* The one rule the page enforces itself. Altitude is a whole number of metres to the API, so a
     fraction never reaches its validation — it fails to bind, and the answer names no field. */
  const [ownProblems, setOwnProblems] = useState<Record<string, string>>({})
  const problems = { ...fieldProblems, ...ownProblems }
  /* Where the cursor goes after the browser fills the coordinate: the name is the one field only
     a person can fill, and it is the only thing left to do before tracking. */
  const nameInput = useRef<HTMLInputElement>(null)

  function submit(event: FormEvent) {
    event.preventDefault()

    const metres = reading(altitude)

    if (metres !== null && !Number.isInteger(metres)) {
      setOwnProblems({ Altitude: 'Give the altitude in whole metres above sea level.' })

      return
    }

    setOwnProblems({})
    onTrackTyped({
      name,
      latitude: reading(latitude),
      longitude: reading(longitude),
      altitude: metres,
    })
  }

  /**
   * A picked Match fills the four fields and stops there. They stay ordinary inputs afterwards —
   * a gazetteer's name is a starting point, and the name is only a label, so correcting "Bergen"
   * to "Grandma's" before tracking has to be possible.
   */
  function fill(match: Match) {
    setName(match.name)
    setLatitude(String(match.latitude))
    setLongitude(String(match.longitude))
    setAltitude(String(match.elevation))
    setOwnProblems({})
  }

  /**
   * Where the browser says you are: the coordinate, at the four decimals it will be tracked at,
   * and an empty name to type. The altitude is cleared with it and arrives separately, because it
   * is a second request that may not answer — a stale height beside a new coordinate would be a
   * quiet lie about the one field the temperature forecast leans on.
   */
  function place(latitude: number, longitude: number) {
    setName('')
    setLatitude(String(latitude))
    setLongitude(String(longitude))
    setAltitude('')
    setOwnProblems({})
    nameInput.current?.focus()
  }

  return (
    <section className="track__section">
      <h2 className="track__heading">A Location we have never seen</h2>
      <p className="track__note">
        Search for a name and pick one, which fills the form below and leaves it editable; ask the
        browser where you are, which fills the coordinate and the altitude and leaves the name to
        you — or type the coordinate straight in, which is what still works when neither lookup can
        be reached. Four decimals is the precision Providers answer at; anything finer is truncated
        to it. Altitude changes the temperature forecast, so both lookups supply one.
      </p>

      <FindByName busy={busy} onPick={fill} />

      <UseMyLocation busy={busy} onMeasure={(metres) => setAltitude(String(metres))} onPlace={place} />

      <form className="track__form" onSubmit={submit}>
        <Field label="Name" problem={problems.Name}>
          <input
            className="field__input"
            onChange={(event) => setName(event.target.value)}
            ref={nameInput}
            type="text"
            value={name}
          />
        </Field>

        <Field label="Latitude °N" problem={problems.Latitude}>
          <input
            className="field__input"
            inputMode="decimal"
            onChange={(event) => setLatitude(event.target.value)}
            step="any"
            type="number"
            value={latitude}
          />
        </Field>

        <Field label="Longitude °E" problem={problems.Longitude}>
          <input
            className="field__input"
            inputMode="decimal"
            onChange={(event) => setLongitude(event.target.value)}
            step="any"
            type="number"
            value={longitude}
          />
        </Field>

        <Field label="Altitude, whole metres" problem={problems.Altitude}>
          <input
            className="field__input"
            inputMode="numeric"
            onChange={(event) => setAltitude(event.target.value)}
            step="1"
            type="number"
            value={altitude}
          />
        </Field>

        <button className="track__submit" disabled={busy} type="submit">
          Track this Location
        </button>
      </form>
    </section>
  )
}
