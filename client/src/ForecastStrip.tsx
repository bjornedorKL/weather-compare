import type { Forecast } from './api.ts'
import {
  formatClock,
  formatMillimetres,
  formatMoment,
  formatPrecipitation,
  formatTemperature,
} from './format.ts'
import { SymbolIcon } from './SymbolIcon.tsx'
import { symbolLabel } from './symbols.ts'

type Props = {
  forecasts: Forecast[]
}

/** Keeps a column's rows aligned with its neighbours when it has nothing to say. */
const BLANK = '\u00a0'

/**
 * The hours ahead. Each column names the moment it starts at; where the Provider summarised a
 * window longer than an hour the column says so, because the millimetres beneath it belong to
 * that window and not to the hour.
 */
export function ForecastStrip({ forecasts }: Props) {
  if (forecasts.length === 0) {
    return null
  }

  return (
    <ol className="strip">
      {forecasts.map((forecast) => (
        <li className="strip__step" key={forecast.validAt} title={describe(forecast)}>
          <time className="strip__clock" dateTime={forecast.validAt}>
            {formatClock(new Date(forecast.validAt))}
          </time>
          <span className="strip__period">{periodBadge(forecast)}</span>
          <SymbolIcon symbol={forecast.symbol} size="small" />
          <span className="strip__temperature">
            {formatTemperature(forecast.temperatureCelsius)}
          </span>
          <span className="strip__precipitation">{precipitationBadge(forecast)}</span>
        </li>
      ))}
    </ol>
  )
}

/** Only shown when the window is not the hourly one a reader would assume. */
function periodBadge(forecast: Forecast): string {
  if (forecast.periodHours === null || forecast.periodHours === 1) {
    return BLANK
  }

  return `${forecast.periodHours} h`
}

/** Dry hours stay quiet; a wet one shows the millimetres its column's window collected. */
function precipitationBadge(forecast: Forecast): string {
  const millimetres = forecast.precipitationMillimetres

  if (millimetres === null || millimetres <= 0) {
    return BLANK
  }

  return `${formatMillimetres(millimetres)} mm`
}

/** The whole column spelled out, for a reader who hovers and for anyone reading the markup. */
function describe(forecast: Forecast): string {
  const parts = [formatMoment(new Date(forecast.validAt))]
  const symbol = symbolLabel(forecast.symbol)
  const precipitation = formatPrecipitation(
    forecast.precipitationMillimetres,
    forecast.periodHours,
  )

  if (symbol !== null) {
    parts.push(symbol)
  }

  if (forecast.temperatureCelsius !== null) {
    parts.push(`${forecast.temperatureCelsius} °C`)
  }

  if (precipitation !== null) {
    parts.push(precipitation)
  }

  return parts.join(' · ')
}
