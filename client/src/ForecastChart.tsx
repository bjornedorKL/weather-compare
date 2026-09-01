import type { Forecast } from './api.ts'
import { formatDayShort, formatIntensity, formatTemperature } from './format.ts'
import type { ForecastDay, ResolutionChange } from './timeline.ts'
import { precipitationIntensity, stepEnd } from './timeline.ts'

/**
 * The shape of a whole Snapshot at a glance: temperature as a line, precipitation as bars,
 * one vertical rule per day.
 *
 * The x axis is linear in *time*, not in steps, so the coarsening of the range shows up as
 * wider bars and sparser vertices rather than as a distortion of the curve. Each bar spans
 * exactly the window the Provider summarised and is drawn at that window's intensity in mm/h,
 * so its area is the millimetres and a six-hour window cannot masquerade as an hour of heavier
 * rain. Where the window length changes, the chart says so.
 *
 * The drawing carries no text: the SVG is stretched to the reader's width, which would squash
 * any glyph inside it, so every label is HTML positioned over the plot instead. The chart is
 * decoration on top of the day-by-day steps below it, which carry the same numbers, so it is
 * hidden from assistive technology rather than duplicating them badly.
 */

/** The plot's own coordinates. Bands keep the curve and the bars from ever overlapping. */
const WIDTH = 1000
const TEMPERATURE_TOP = 8
const TEMPERATURE_BOTTOM = 62
const PRECIPITATION_TOP = 70
const PRECIPITATION_BOTTOM = 100

/** Nothing is plotted below this rate, so a trace of drizzle cannot fill the band. */
const MINIMUM_INTENSITY_SCALE = 0.5

type Props = {
  forecasts: Forecast[]
  days: ForecastDay[]
  changes: ResolutionChange[]
  now: Date
}

export function ForecastChart({ forecasts, days, changes, now }: Props) {
  const plot = plotOf(forecasts)

  if (plot === null) {
    return null
  }

  const { x, temperature, intensityScale, curve, bars } = plot

  return (
    <figure className="chart">
      <div className="chart__plot">
        <svg
          className="chart__svg"
          viewBox={`0 0 ${WIDTH} ${PRECIPITATION_BOTTOM}`}
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          {days.slice(1).map((day) => (
            <line className="chart__midnight" key={day.key} x1={x(day.start)} x2={x(day.start)} y1="0" y2={PRECIPITATION_BOTTOM} />
          ))}
          {temperature.crossesFreezing && (
            <line className="chart__freezing" x1="0" x2={WIDTH} y1={temperature.y(0)} y2={temperature.y(0)} />
          )}
          <line className="chart__baseline" x1="0" x2={WIDTH} y1={PRECIPITATION_BOTTOM} y2={PRECIPITATION_BOTTOM} />
          {bars.map((bar) => (
            <rect className="chart__rain" key={bar.key} x={bar.x} y={bar.y} width={bar.width} height={bar.height} />
          ))}
          <polyline className="chart__temperature" points={curve} />
          {changes.map((change) => (
            <line className="chart__change" key={change.at.toISOString()} x1={x(change.at)} x2={x(change.at)} y1="0" y2={PRECIPITATION_BOTTOM} />
          ))}
          {plot.contains(now) && <line className="chart__now" x1={x(now)} x2={x(now)} y1="0" y2={PRECIPITATION_BOTTOM} />}
        </svg>

        <div className="chart__marks">
          {/* Ten day marks will not sit side by side on a phone, so they alternate between two
              rows. The first stays on the upper row, clear of the `now` mark beneath it. */}
          {days.slice(1).map((day, index) => (
            <span
              className={`chart__mark${index % 2 === 1 ? ' chart__mark--lower' : ''}`}
              key={day.key}
              style={{ left: plot.percent(day.start) }}
            >
              {formatDayShort(day.start)}
            </span>
          ))}
          {plot.contains(now) && (
            <span
              className="chart__mark chart__mark--lower chart__mark--now"
              style={{ left: plot.percent(now) }}
            >
              now
            </span>
          )}
          {changes.map((change) => (
            <span
              className="chart__mark chart__mark--change"
              key={change.at.toISOString()}
              style={{ left: plot.percent(change.at) }}
            >
              {change.toHours} h steps
            </span>
          ))}
        </div>

        <span className="chart__axis chart__axis--warm">{formatTemperature(temperature.highest)}</span>
        <span className="chart__axis chart__axis--cold">{formatTemperature(temperature.lowest)}</span>
        <span className="chart__axis chart__axis--wet">{formatIntensity(intensityScale)}</span>
      </div>

      <figcaption className="chart__caption">
        Temperature (line) and precipitation (bars) over the whole Snapshot. Precipitation is
        drawn as a rate, because the Provider measures it over a window that lengthens down the
        range: every bar spans its own window at its own mm/h, so a wide bar is a long window
        and not a heavier shower. Each rule is a local midnight; the day-by-day steps below
        carry the same figures with their windows spelled out.
      </figcaption>
    </figure>
  )
}

type Bar = { key: string; x: number; y: number; width: number; height: number }

type Plot = {
  x: (moment: Date) => number
  percent: (moment: Date) => string
  contains: (moment: Date) => boolean
  temperature: { lowest: number; highest: number; crossesFreezing: boolean; y: (celsius: number) => number }
  intensityScale: number
  curve: string
  bars: Bar[]
}

/**
 * Everything the drawing needs, or null when there is nothing to draw — no Forecasts, or a
 * range with no duration, which a single-step Snapshot would give us.
 */
function plotOf(forecasts: readonly Forecast[]): Plot | null {
  const first = forecasts[0]
  const last = forecasts[forecasts.length - 1]

  if (first === undefined) {
    return null
  }

  const from = Date.parse(first.validAt)
  const to = Math.max(stepEnd(last)?.getTime() ?? 0, Date.parse(last.validAt))
  const span = to - from

  if (span <= 0) {
    return null
  }

  const x = (moment: Date) => ((moment.getTime() - from) / span) * WIDTH
  const temperature = temperatureScale(forecasts)
  const intensityScale = Math.max(
    MINIMUM_INTENSITY_SCALE,
    ...forecasts.map((forecast) => precipitationIntensity(forecast) ?? 0),
  )

  return {
    x,
    percent: (moment) => `${(x(moment) / WIDTH) * 100}%`,
    contains: (moment) => moment.getTime() >= from && moment.getTime() <= to,
    temperature,
    intensityScale,
    curve: curveOf(forecasts, x, temperature.y),
    bars: barsOf(forecasts, x, intensityScale),
  }
}

/** A degree of headroom either side, and a floor on the span so a flat day is not a wild zigzag. */
function temperatureScale(forecasts: readonly Forecast[]) {
  const readings = forecasts
    .map((forecast) => forecast.temperatureCelsius)
    .filter((celsius): celsius is number => celsius !== null)

  const lowest = readings.length === 0 ? 0 : Math.min(...readings)
  const highest = readings.length === 0 ? 0 : Math.max(...readings)
  const floor = lowest - 1
  const span = Math.max(highest + 1 - floor, 4)
  const height = TEMPERATURE_BOTTOM - TEMPERATURE_TOP

  return {
    lowest,
    highest,
    crossesFreezing: lowest < 0 && highest > 0,
    y: (celsius: number) => TEMPERATURE_BOTTOM - ((celsius - floor) / span) * height,
  }
}

function curveOf(
  forecasts: readonly Forecast[],
  x: (moment: Date) => number,
  y: (celsius: number) => number,
): string {
  return forecasts
    .filter((forecast) => forecast.temperatureCelsius !== null)
    .map((forecast) => `${x(new Date(forecast.validAt))},${y(forecast.temperatureCelsius!)}`)
    .join(' ')
}

function barsOf(
  forecasts: readonly Forecast[],
  x: (moment: Date) => number,
  scale: number,
): Bar[] {
  const height = PRECIPITATION_BOTTOM - PRECIPITATION_TOP
  const bars: Bar[] = []

  for (const forecast of forecasts) {
    const intensity = precipitationIntensity(forecast)
    const end = stepEnd(forecast)

    if (intensity === null || intensity <= 0 || end === null) {
      continue
    }

    const start = x(new Date(forecast.validAt))
    const bar = Math.min(intensity / scale, 1) * height

    bars.push({
      key: forecast.validAt,
      x: start,
      y: PRECIPITATION_BOTTOM - bar,
      width: Math.max(x(end) - start, 0.75),
      height: bar,
    })
  }

  return bars
}
