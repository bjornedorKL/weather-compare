import type { FormEvent, ReactNode } from 'react'
import { useState } from 'react'
import type { KnownLocation, Match, TypedLocation } from './api.ts'
import { searchMatches } from './api.ts'
import { formatCoordinate } from './format.ts'

/**
 * The two ways a Location joins the Catalogue, in one panel above the grid.
 *
 * They are deliberately in that order. Tracking again something we already know is the common
 * act — untracking is reversible and meant to be reversed — while describing a Location we have
 * never seen is the rare one.
 *
 * That second way now has two routes into the same four fields: search a name and pick a Match,
 * or type the coordinate out. Searching is the one anyone can do from memory (ADR-0004); typing
 * stays because it is the fallback when the gazetteer is unreachable, and the only route that
 * works with no network at all.
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

function NewCoordinate({ busy, fieldProblems, onTrackTyped }: Omit<Props, 'untracked' | 'onTrackKnown'>) {
  const [name, setName] = useState('')
  const [latitude, setLatitude] = useState('')
  const [longitude, setLongitude] = useState('')
  const [altitude, setAltitude] = useState('')
  /* The one rule the page enforces itself. Altitude is a whole number of metres to the API, so a
     fraction never reaches its validation — it fails to bind, and the answer names no field. */
  const [ownProblems, setOwnProblems] = useState<Record<string, string>>({})
  const problems = { ...fieldProblems, ...ownProblems }

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

  return (
    <section className="track__section">
      <h2 className="track__heading">A Location we have never seen</h2>
      <p className="track__note">
        Search for a name and pick one, which fills the form below and leaves it editable — or
        type the coordinate straight in, which is what still works when the search cannot be
        reached. Four decimals is the precision Providers answer at; anything finer is truncated
        to it. Altitude changes the temperature forecast, so a search supplies one.
      </p>

      <FindByName busy={busy} onPick={fill} />

      <form className="track__form" onSubmit={submit}>
        <Field label="Name" problem={problems.Name}>
          <input
            className="field__input"
            onChange={(event) => setName(event.target.value)}
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
