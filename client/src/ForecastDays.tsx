import { Fragment } from 'react'
import type { Forecast } from './api.ts'
import {
  formatClock,
  formatDayName,
  formatMillimetres,
  formatMoment,
  formatPrecipitation,
  formatTemperature,
  formatWind,
  formatWindow,
} from './format.ts'
import { SymbolIcon } from './SymbolIcon.tsx'
import { symbolLabel } from './symbols.ts'
import type { DaySummary, ForecastDay } from './timeline.ts'
import { resolutionChanges, stepEnd, summariseDay } from './timeline.ts'

/**
 * The Snapshot read out step by step, broken at local midnight — ten days of steps in one
 * ribbon is unreadable, and a day is the unit a reader plans in.
 *
 * Every step names the window it describes, because the Provider's windows lengthen down the
 * range and `2.8 mm` means nothing without knowing whether it fell in an hour or in six. The
 * step that closes a Snapshot describes no window at all: it is an instant with a temperature
 * and a wind, and says so rather than showing a zero it was never given.
 *
 * A step is also the way into its own history. Picking one asks what every earlier Snapshot said
 * about that same moment — the entry point is here, in the timeline the reader is already
 * reading, rather than a screen of its own with a date picker on it.
 */

type Props = {
  days: ForecastDay[]
  now: Date
  /** The moment whose history is open, as the Forecast spelled it. */
  selected: string | null
  onSelect: (validAt: string) => void
}

export function ForecastDays({ days, now, selected, onSelect }: Props) {
  /* The steps where the Provider's window lengthens. Called out in the rows as well as in the
     chart, because that is the row where the millimetres above and below stop being comparable. */
  const rewindowed = new Map(
    resolutionChanges(days.flatMap((day) => day.steps)).map((change) => [
      change.at.getTime(),
      change,
    ]),
  )

  return (
    <ol className="days">
      {days.map((day) => (
        <li className="day" key={day.key}>
          <div className="day__header">
            <h3 className="day__name">{formatDayName(day.start)}</h3>
            <p className="day__summary" title="Over the windows that start on this day">
              {describeDay(summariseDay(day.steps))}
            </p>
          </div>

          <ul className="steps">
            {day.steps.map((step) => {
              const change = rewindowed.get(Date.parse(step.validAt))

              return (
                <Fragment key={step.validAt}>
                  {change !== undefined && (
                    <li className="steps__note">
                      Steps lengthen from {change.fromHours} h to {change.toHours} h here — each
                      figure below covers {change.toHours} hours, not {change.fromHours}.
                    </li>
                  )}
                  <Step
                    now={now}
                    onSelect={() => onSelect(step.validAt)}
                    selected={step.validAt === selected}
                    step={step}
                  />
                </Fragment>
              )
            })}
          </ul>
        </li>
      ))}
    </ol>
  )
}

type StepProps = {
  step: Forecast
  now: Date
  selected: boolean
  onSelect: () => void
}

function Step({ step, now, selected, onSelect }: StepProps) {
  const end = stepEnd(step)
  const past = end !== null ? end <= now : Date.parse(step.validAt) <= now.getTime()
  /* The direction is the arrow beside it, so the row prints the speed alone; the row's title
     still spells the bearing out in words for anyone who wants it named. */
  const wind = formatWind(step.windSpeedMetresPerSecond, null)

  return (
    <li
      className={`step${past ? ' step--past' : ''}${selected ? ' step--selected' : ''}`}
      title={describeStep(step)}
    >
      {/* The clock is the control, stretched over the whole row by its own ::after, so a step is
          opened from anywhere along it — one real button, which the keyboard gets for free. */}
      <button
        aria-label={`What successive Snapshots said about ${formatMoment(new Date(step.validAt))}`}
        aria-pressed={selected}
        className="step__open"
        onClick={onSelect}
        type="button"
      >
        <time className="step__clock" dateTime={step.validAt}>
          {formatClock(new Date(step.validAt))}
        </time>
      </button>
      <span className="step__window">{formatWindow(step.periodHours)}</span>
      <SymbolIcon symbol={step.symbol} size="small" />
      <span className="step__symbol">{symbolLabel(step.symbol) ?? 'No Symbol'}</span>
      <span className="step__temperature">{formatTemperature(step.temperatureCelsius)}</span>
      <span className="step__precipitation">{precipitation(step)}</span>
      <span className="step__wind">
        {step.windFromDirectionDegrees !== null && (
          <span
            className="step__arrow"
            aria-hidden="true"
            style={{ transform: `rotate(${step.windFromDirectionDegrees + 180}deg)` }}
          >
            ↑
          </span>
        )}
        <span className="step__speed">{wind === null ? '–' : wind}</span>
      </span>
    </li>
  )
}

/**
 * The millimetres alone: the window they fell in is its own column beside them, so it is never
 * read off without one. A step the Provider gave no precipitation block for shows nothing.
 */
function precipitation(step: Forecast): string {
  const millimetres = step.precipitationMillimetres

  if (millimetres === null) {
    return '–'
  }

  return millimetres <= 0 ? '' : `${formatMillimetres(millimetres)} mm`
}

/** A day in one line: how cold, how warm, how wet, and at what resolution it is measured. */
function describeDay(summary: DaySummary): string {
  const parts: string[] = []

  if (summary.lowestCelsius !== null && summary.highestCelsius !== null) {
    parts.push(
      `${formatTemperature(summary.lowestCelsius)} to ${formatTemperature(summary.highestCelsius)}`,
    )
  }

  if (summary.millimetres !== null) {
    parts.push(summary.millimetres <= 0 ? 'dry' : `${formatMillimetres(summary.millimetres)} mm`)
  }

  parts.push(describeResolution(summary.periodHours))

  return parts.join(' · ')
}

/** `hourly steps`, `6 h steps`, or `hourly, then 6 h steps` for the day the window changes on. */
function describeResolution(periodHours: readonly number[]): string {
  if (periodHours.length === 0) {
    return 'no summarised window'
  }

  return `${periodHours.map((hours) => (hours === 1 ? 'hourly' : `${hours} h`)).join(', then ')} steps`
}

/** The whole step spelled out, for a reader who hovers and for anyone reading the markup. */
function describeStep(step: Forecast): string {
  const end = stepEnd(step)
  const parts = [
    end === null
      ? `${formatMoment(new Date(step.validAt))} (instant, no summarised window)`
      : `${formatMoment(new Date(step.validAt))}–${formatClock(end)}`,
  ]
  const symbol = symbolLabel(step.symbol)
  const wind = formatWind(step.windSpeedMetresPerSecond, step.windFromDirectionDegrees)
  const rain = formatPrecipitation(step.precipitationMillimetres, step.periodHours)

  if (symbol !== null) {
    parts.push(symbol)
  }

  if (step.temperatureCelsius !== null) {
    parts.push(`${step.temperatureCelsius} °C`)
  }

  if (rain !== null) {
    parts.push(rain)
  }

  if (wind !== null) {
    parts.push(wind)
  }

  return parts.join(' · ')
}
