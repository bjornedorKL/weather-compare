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
}

export function LocationCard({ location, now, onOpen }: Props) {
  const snapshot = newestSnapshot(location)
  const current = snapshot === null ? null : currentForecast(snapshot, now)
  /* A Location no Provider has been asked about has no timeline to open, so it does not offer
     one; the name stays plain text rather than a control that leads nowhere. */
  const openable = snapshot !== null && current !== null

  return (
    <article className={`card${openable ? ' card--openable' : ''}`}>
      <header className="card__header">
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
      </header>

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
