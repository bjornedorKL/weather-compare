import type { FormEvent } from 'react'
import { useState } from 'react'
import type { KnownLocation } from './api.ts'
import { formatCoordinate } from './format.ts'

/**
 * Renaming: the one field of a Location a person may change.
 *
 * A name is a label, so this form is deliberately the whole of it — the coordinate identifies the
 * Location and the altitude changes what the forecast says, and neither is editable here
 * (ADR-0004). What is typed is sent as typed: the API owns the rule about what a name may be, and
 * a refusal is reported in the API's own words by the page's notice, as every other write is.
 */

/**
 * What the `name` column holds, mirroring `TrackLocationRequest.LongestName`. Stopping the field
 * at the length the API accepts is a courtesy, not the rule — the API still refuses an overlong
 * name, and still would if this were wrong.
 */
const LONGEST_NAME = 100

type Props = {
  /** The name on file, which the field starts from — a rename usually edits rather than replaces. */
  name: string
  busy: boolean
  onRename: (name: string) => void
  onCancel: () => void
}

export function RenameLocation({ name, busy, onRename, onCancel }: Props) {
  const [typed, setTyped] = useState(name)

  function submit(event: FormEvent) {
    event.preventDefault()
    onRename(typed)
  }

  return (
    <form className="rename__form" onSubmit={submit}>
      <label className="rename__field">
        <span className="rename__label">Name</span>
        <input
          className="rename__input"
          maxLength={LONGEST_NAME}
          onChange={(event) => setTyped(event.target.value)}
          onKeyDown={(event) => event.key === 'Escape' && onCancel()}
          type="text"
          value={typed}
        />
      </label>

      <button className="rename__save" disabled={busy} type="submit">
        Save
      </button>
      <button className="rename__cancel" onClick={onCancel} type="button">
        Cancel
      </button>
    </form>
  )
}

type PanelProps = {
  /** Locations we know but do not track. The tracked ones are renamed from their card. */
  untracked: KnownLocation[]
  busy: boolean
  onRename: (id: number, name: string) => void
}

/**
 * Renaming a Location that is not in the Catalogue. It has no card to be renamed from — the grid
 * draws the Catalogue — and it is still a Location we know, still shown on the page, so its name
 * still has to be correctable. Collapsed, and absent entirely when everything we know is tracked.
 */
export function RenameUntracked({ untracked, busy, onRename }: PanelProps) {
  const [renaming, setRenaming] = useState<number | null>(null)

  if (untracked.length === 0) {
    return null
  }

  return (
    <details className="rename">
      <summary className="rename__summary">Rename one we know but do not track</summary>

      <section className="rename__section">
        <p className="rename__note">
          A name is only a label: renaming changes nothing about which Location it is, keeps every
          Forecast Snapshot recorded at its coordinate, and does not start tracking it again. Two
          Locations are allowed to share a name.
        </p>

        <ul className="rename__list">
          {untracked.map((location) => (
            <li className="rename__item" key={location.id}>
              {renaming === location.id ? (
                <RenameLocation
                  busy={busy}
                  name={location.name}
                  onCancel={() => setRenaming(null)}
                  onRename={(name) => {
                    setRenaming(null)
                    onRename(location.id, name)
                  }}
                />
              ) : (
                <button
                  className="rename__pick"
                  disabled={busy}
                  onClick={() => setRenaming(location.id)}
                  type="button"
                >
                  <span className="rename__pick-name">{location.name}</span>
                  <span className="rename__pick-coordinate">
                    {formatCoordinate(location.latitude, location.longitude)} · {location.altitude} m
                  </span>
                </button>
              )}
            </li>
          ))}
        </ul>
      </section>
    </details>
  )
}
