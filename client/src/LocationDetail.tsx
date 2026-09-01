import { useEffect, useRef } from 'react'
import type { LocationForecasts } from './api.ts'
import { currentForecast, newestSnapshot } from './forecasts.ts'
import {
  formatAge,
  formatCoordinate,
  formatMoment,
  formatPrecipitation,
  formatTemperature,
  formatWind,
} from './format.ts'
import { ForecastChart } from './ForecastChart.tsx'
import { ForecastDays } from './ForecastDays.tsx'
import { SymbolIcon } from './SymbolIcon.tsx'
import { symbolLabel } from './symbols.ts'
import { groupByDay, resolutionChanges } from './timeline.ts'

/**
 * One Location's newest Forecast Snapshot in full — every Forecast it holds, which is what the
 * card on the grid was summarising. There is no route behind this: the grid swaps itself for
 * the detail in place, so Escape and the back control are the only ways out, and both are here.
 *
 * The Snapshot's Issued At is repeated here rather than left behind on the card. A view of ten
 * days of Forecasts is exactly where a reader is most likely to forget that all of it was one
 * Provider's answer at one past moment.
 */

type Props = {
  location: LocationForecasts
  now: Date
  onClose: () => void
}

export function LocationDetail({ location, now, onClose }: Props) {
  const back = useRef<HTMLButtonElement>(null)
  const snapshot = newestSnapshot(location)
  const current = snapshot === null ? null : currentForecast(snapshot, now)

  useEffect(() => {
    back.current?.focus()
  }, [])

  useEffect(() => {
    function escape(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    window.addEventListener('keydown', escape)

    return () => window.removeEventListener('keydown', escape)
  }, [onClose])

  const days = snapshot === null ? [] : groupByDay(snapshot.forecasts)

  return (
    <section className="detail" aria-label={`Forecasts for ${location.name}`}>
      <button className="detail__back" onClick={onClose} ref={back} type="button">
        ← All Locations
      </button>

      <header className="detail__header">
        <h2 className="detail__name">{location.name}</h2>
        <p className="detail__coordinate">
          {formatCoordinate(location.latitude, location.longitude)} · {location.altitude} m
        </p>
        {snapshot !== null && (
          <p className="detail__issued">
            {snapshot.provider} · Snapshot issued{' '}
            <time dateTime={snapshot.issuedAt}>{formatAge(new Date(snapshot.issuedAt), now)}</time>,
            at {formatMoment(new Date(snapshot.issuedAt))} · {snapshot.forecasts.length} Forecasts
          </p>
        )}
      </header>

      {snapshot === null || current === null ? (
        <p className="detail__empty">
          No Forecast Snapshot yet — no Provider has been asked about this Location, so there is
          no timeline to show.
        </p>
      ) : (
        <>
          <div className="detail__now">
            <SymbolIcon symbol={current.symbol} size="large" />
            <div>
              <p className="now__temperature">{formatTemperature(current.temperatureCelsius)}</p>
              <p className="now__symbol">{symbolLabel(current.symbol) ?? 'No symbol'}</p>
            </div>
            <dl className="measures">
              <div className="measures__row">
                <dt>Wind</dt>
                <dd>
                  {formatWind(current.windSpeedMetresPerSecond, current.windFromDirectionDegrees) ?? '–'}
                </dd>
              </div>
              <div className="measures__row">
                <dt>Precipitation</dt>
                <dd>
                  {formatPrecipitation(current.precipitationMillimetres, current.periodHours) ?? '–'}
                </dd>
              </div>
            </dl>
          </div>

          <ForecastChart
            changes={resolutionChanges(snapshot.forecasts)}
            days={days}
            forecasts={snapshot.forecasts}
            now={now}
          />

          <ForecastDays days={days} now={now} />
        </>
      )}
    </section>
  )
}
