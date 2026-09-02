import { useState } from 'react'
import type { LocationForecasts } from './api.ts'
import { currentForecast, newestSnapshot, upcomingForecasts } from './forecasts.ts'
import {
  formatAge,
  formatCoordinate,
  formatMoment,
  formatPrecipitation,
  formatTemperature,
  formatWind,
} from './format.ts'
import { ForecastStrip } from './ForecastStrip.tsx'
import { RenameLocation } from './RenameLocation.tsx'
import { SymbolIcon } from './SymbolIcon.tsx'
import { symbolLabel } from './symbols.ts'

/**
 * How far ahead a card looks. Enough to plan an afternoon, not a holiday — and few enough
 * that the strip fits across a narrow card without scrolling sideways.
 */
const STRIP_LENGTH = 6

type Props = {
  location: LocationForecasts
  now: Date
  /** Opens the Location's full timeline. */
  onOpen: () => void
  /**
   * Takes this Location out of the Catalogue. Null when the page cannot name it to the API —
   * the two reads disagreed — in which case the card simply offers no such control.
   */
  onUntrack: (() => void) | null
  /**
   * Gives this Location a different label. Null on the same terms as `onUntrack`: without an
   * id the page cannot name it to the API, so the card offers no such control.
   */
  onRename: ((name: string) => void) | null
  /** A write is already in flight, so the controls stand down until it lands. */
  busy: boolean
}

export function LocationCard({ location, now, onOpen, onUntrack, onRename, busy }: Props) {
  const snapshot = newestSnapshot(location)
  const current = snapshot === null ? null : currentForecast(snapshot, now)
  /* A Location no Provider has been asked about has no timeline to open, so it does not offer
     one; the name stays plain text rather than a control that leads nowhere. */
  const openable = snapshot !== null && current !== null

  return (
    <article className={`card${openable ? ' card--openable' : ''}`}>
      <CardHeader
        busy={busy}
        location={location}
        onOpen={onOpen}
        onRename={onRename}
        onUntrack={onUntrack}
        openable={openable}
      />

      {snapshot === null || current === null ? (
        <p className="card__empty">
          No Forecast Snapshot yet — no Provider has been asked about this Location.
        </p>
      ) : (
        <>
          <div className="now">
            <SymbolIcon symbol={current.symbol} size="large" />
            <div className="now__reading">
              <p className="now__temperature">{formatTemperature(current.temperatureCelsius)}</p>
              <p className="now__symbol">{symbolLabel(current.symbol) ?? 'No symbol'}</p>
            </div>
          </div>

          <dl className="measures">
            <div className="measures__row">
              <dt>Wind</dt>
              <dd>{formatWind(current.windSpeedMetresPerSecond, current.windFromDirectionDegrees) ?? '–'}</dd>
            </div>
            <div className="measures__row">
              <dt>Precipitation</dt>
              <dd>
                {formatPrecipitation(current.precipitationMillimetres, current.periodHours) ?? '–'}
              </dd>
            </div>
          </dl>

          <ForecastStrip forecasts={upcomingForecasts(snapshot, now, STRIP_LENGTH)} />

          <footer className="card__issued">
            {snapshot.provider} · updated{' '}
            <time dateTime={snapshot.issuedAt} title={formatMoment(new Date(snapshot.issuedAt))}>
              {formatAge(new Date(snapshot.issuedAt), now)}
            </time>
          </footer>
        </>
      )}
    </article>
  )
}

/**
 * The name, the coordinate, and the two controls that act on the Location. While a rename is
 * open it is the whole header: the name is the thing being edited, so it is not also a control
 * that opens the timeline, and with `.card__open` gone the card's stretched hit area goes with it.
 */
function CardHeader({
  location,
  openable,
  onOpen,
  onUntrack,
  onRename,
  busy,
}: Pick<Props, 'location' | 'onOpen' | 'onUntrack' | 'onRename' | 'busy'> & { openable: boolean }) {
  const [renaming, setRenaming] = useState(false)

  if (renaming && onRename !== null) {
    return (
      <header className="card__header card__header--renaming">
        <RenameLocation
          busy={busy}
          name={location.name}
          onCancel={() => setRenaming(false)}
          onRename={(name) => {
            setRenaming(false)
            onRename(name)
          }}
        />
      </header>
    )
  }

  return (
    <header className="card__header">
      <div className="card__heading">
        <h2 className="card__name">
          {openable ? (
            <button className="card__open" onClick={onOpen} type="button">
              {location.name}
            </button>
          ) : (
            location.name
          )}
        </h2>
        <p className="card__coordinate">
          {formatCoordinate(location.latitude, location.longitude)} · {location.altitude} m
        </p>
      </div>

      {/* Sits above the stretched hit area that opens the Location, so the two do not fight:
          the card is one big click target and this is the hole punched in it. */}
      <div className="card__controls">
        {onRename !== null && (
          <button
            aria-label={`Rename ${location.name}`}
            className="card__rename"
            disabled={busy}
            onClick={() => setRenaming(true)}
            title="Changes the label only. The Location, its coordinate and its history are untouched."
            type="button"
          >
            Rename
          </button>
        )}

        {onUntrack !== null && (
          <button
            aria-label={`Stop tracking ${location.name}`}
            className="card__untrack"
            disabled={busy}
            onClick={onUntrack}
            title="Stops new Forecast Snapshots. Every Snapshot already recorded is kept."
            type="button"
          >
            Stop tracking
          </button>
        )}
      </div>
    </header>
  )
}
