import type { FormEvent, ReactNode } from 'react'
import { useState } from 'react'
import type { KnownLocation, TypedLocation } from './api.ts'
import { formatCoordinate } from './format.ts'

/**
 * The two ways a Location joins the Catalogue, in one panel above the grid.
 *
 * They are deliberately in that order. Tracking again something we already know is the common
 * act — untracking is reversible and meant to be reversed — while describing a coordinate we
 * have never seen is the rare one, and needs a coordinate typed out because there is no
 * place-name search and no map (decided against a geocoder).
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

  return (
    <section className="track__section">
      <h2 className="track__heading">A coordinate we have never seen</h2>
      <p className="track__note">
        Type the coordinate — there is no place-name search. Four decimals is the precision
        Providers answer at; anything finer is truncated to it. The name is only a label.
      </p>

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
