import { Fragment, useEffect, useRef, useState } from 'react'
import type { Forecast } from './api.ts'
import {
  formatAge,
  formatClock,
  formatDegreeChange,
  formatDegrees,
  formatDuration,
  formatMoment,
  formatPrecipitation,
  formatWind,
} from './format.ts'
import type {
  ForecastHistory as History,
  ForecastHistoryGap,
  ForecastMove,
  ProviderForecastHistory,
} from './history.ts'
import { fetchForecastHistory, moveOf } from './history.ts'
import { HistoryChart } from './HistoryChart.tsx'
import { SymbolIcon } from './SymbolIcon.tsx'
import { symbolLabel } from './symbols.ts'

/**
 * One moment, and every answer a Provider has given about it. Reached by picking a step out of
 * the timeline above, so it opens on a moment the reader was already looking at rather than
 * standing on its own as a screen with a date picker in it.
 *
 * This is the whole reason the store keeps every Snapshot instead of overwriting one row. It
 * shows a prediction *moving*; it never shows whether the prediction was right. Being right
 * needs an Observation — what the weather actually was — which this system does not have and
 * `CONTEXT.md` deliberately does not define. Nothing here subtracts a Forecast from reality.
 */

type Props = {
  /** The id the API knows this Location by, or null when the two reads have not been matched. */
  locationId: number | null
  /** The moment picked out of the timeline, exactly as the Forecast spelled it. */
  validAt: string
  now: Date
  onClose: () => void
}

/** What one read produced, and which (Location, moment) it was a read of. */
type Read = {
  asked: string
  history: History | null
  failure: string | null
}

export function ForecastHistory({ locationId, validAt, now, onClose }: Props) {
  const heading = useRef<HTMLHeadingElement>(null)
  /* Held with the question it answers, so picking another step reads as loading rather than
     briefly showing the previous moment's Snapshots under the new moment's heading. */
  const [read, setRead] = useState<Read | null>(null)
  const asked = `${locationId}@${validAt}`
  const current = read !== null && read.asked === asked ? read : null

  useEffect(() => {
    heading.current?.focus()
  }, [validAt])

  useEffect(() => {
    if (locationId === null) {
      return
    }

    const attempt = new AbortController()

    fetchForecastHistory(locationId, validAt, attempt.signal)
      .then((history) => setRead({ asked, history, failure: null }))
      .catch((error: Error) => {
        if (error.name !== 'AbortError') {
          setRead({ asked, history: null, failure: error.message })
        }
      })

    return () => attempt.abort()
  }, [asked, locationId, validAt])

  const history = current?.history ?? null
  const failure = current?.failure ?? null

  return (
    <section className="history" aria-label={`Forecast history for ${formatMoment(new Date(validAt))}`}>
      <header className="history__header">
        <h3 className="history__title" ref={heading} tabIndex={-1}>
          What successive Snapshots said about {formatMoment(new Date(validAt))}
        </h3>
        <button className="history__close" onClick={onClose} type="button">
          Close
        </button>
      </header>

      {locationId === null && (
        <p className="history__empty">
          This Location has not been matched to an id we can ask history for.
        </p>
      )}
      {failure !== null && <p className="history__failure">Could not read the history: {failure}</p>}
      {locationId !== null && failure === null && history === null && (
        <p className="history__empty">Reading Snapshots…</p>
      )}

      {history !== null && (
        <>
          {history.providers.length === 0 && <NothingRecorded />}
          {history.providers.map((provider) => (
            <ProviderHistory key={provider.provider} history={provider} now={now} />
          ))}
          <p className="history__caveat">
            {history.snapshotsRead} Forecast Snapshot{history.snapshotsRead === 1 ? '' : 's'} read.
            This is how the prediction moved, not how right it was: nothing here records what the
            weather actually did.
          </p>
        </>
      )}
    </section>
  )
}

function NothingRecorded() {
  return (
    <p className="history__empty">
      No Forecast Snapshot was recorded before this moment, so there is nothing that predicted it.
    </p>
  )
}

/**
 * One Provider's answers, drawn from data already in hand — the panel above does the reading,
 * this does the drawing. Exported so it can be rendered against a fixed history without a
 * browser or a fetch: the movement, the breaks, and the single-Snapshot case are all in here.
 */
export function ProviderHistory({ history, now }: { history: ProviderForecastHistory; now: Date }) {
  const move = moveOf(history)

  return (
    <div className="history__provider">
      <p className="history__move">
        <span className="history__provider-name">{history.provider}</span>{' '}
        {move === null ? describeNoMove(history) : describeMove(history, move)}
      </p>

      <HistoryChart history={history} />

      <ol className="history__points">
        {history.points.map((point, index) => (
          <Fragment key={point.issuedAt}>
            <Point
              forecast={point.forecast}
              issuedAt={point.issuedAt}
              now={now}
              since={previousForecast(history, index)}
            />
            {gapAfter(history, point.issuedAt)}
          </Fragment>
        ))}
      </ol>
    </div>
  )
}

/** The last Forecast before this one that actually said something, for the step-by-step move. */
function previousForecast(history: ProviderForecastHistory, index: number): Forecast | null {
  for (let i = index - 1; i >= 0; i--) {
    if (history.points[i].forecast !== null) {
      return history.points[i].forecast
    }
  }

  return null
}

function gapAfter(history: ProviderForecastHistory, issuedAt: string) {
  const gap = history.gaps.find((candidate) => candidate.fromIssuedAt === issuedAt)

  return gap === undefined ? null : <Gap gap={gap} key={`gap-${issuedAt}`} />
}

/**
 * The stretch nothing was recorded over, given its own row rather than being closed up. It is
 * unbackfillable by construction: a Provider cannot be asked later what it would have said.
 */
function Gap({ gap }: { gap: ForecastHistoryGap }) {
  const from = new Date(gap.dueAt)
  const to = new Date(gap.toIssuedAt)

  return (
    <li className="history__gap-row">
      No Forecast Snapshot recorded between {formatClock(from)} and {formatClock(to)} —{' '}
      {formatDuration(to.getTime() - from.getTime())} with the Provider due to be asked and nothing
      recorded. What it said in there cannot be recovered.
    </li>
  )
}

type PointProps = {
  issuedAt: string
  forecast: Forecast | null
  since: Forecast | null
  now: Date
}

function Point({ issuedAt, forecast, since, now }: PointProps) {
  const asked = new Date(issuedAt)

  if (forecast === null) {
    return (
      <li className="history__point history__point--silent">
        <time className="history__asked" dateTime={issuedAt}>
          {formatClock(asked)}
        </time>
        <span className="history__silent-note">
          Snapshot recorded, but it described no Forecast for this moment
        </span>
      </li>
    )
  }

  const drift =
    since?.temperatureCelsius !== null &&
    since?.temperatureCelsius !== undefined &&
    forecast.temperatureCelsius !== null
      ? forecast.temperatureCelsius - since.temperatureCelsius
      : null

  return (
    <li className="history__point" title={`Snapshot issued ${formatMoment(asked)}`}>
      <time className="history__asked" dateTime={issuedAt}>
        {formatClock(asked)}
      </time>
      <span className="history__age">{formatAge(asked, now)}</span>
      <SymbolIcon symbol={forecast.symbol} size="small" />
      <span className="history__symbol">{symbolLabel(forecast.symbol) ?? 'No Symbol'}</span>
      <span className="history__temperature">{formatDegrees(forecast.temperatureCelsius)}</span>
      <span className="history__drift">{drift === null ? '' : formatDegreeChange(drift)}</span>
      <span className="history__rest">
        {formatPrecipitation(forecast.precipitationMillimetres, forecast.periodHours) ?? '–'} ·{' '}
        {formatWind(forecast.windSpeedMetresPerSecond, forecast.windFromDirectionDegrees) ?? '–'}
      </span>
    </li>
  )
}

/** The headline: where the Provider started, where it is now, and how far that is. */
function describeMove(history: ProviderForecastHistory, move: ForecastMove): string {
  const spoke = history.points.filter((point) => point.forecast !== null).length
  const symbols = move.symbols.map((symbol) => symbolLabel(symbol) ?? symbol)
  const said =
    `first said ${formatDegrees(move.from.celsius)}, now says ${formatDegrees(move.to.celsius)} — ` +
    `${formatDegreeChange(move.degrees)} across ${spoke} Snapshots spanning ` +
    `${formatDuration(move.spanMilliseconds)}.`

  return symbols.length > 1 ? `${said} Symbol moved ${symbols.join(' → ')}.` : said
}

/** What to say when there is no movement to describe, which is not the same as nothing to say. */
function describeNoMove(history: ProviderForecastHistory): string {
  const spoke = history.points.filter((point) => point.forecast !== null).length

  if (spoke === 0) {
    return (
      `recorded ${history.points.length} Snapshot${history.points.length === 1 ? '' : 's'} before ` +
      'this moment, and none of them described a Forecast for it — its steps had already ' +
      'lengthened past it.'
    )
  }

  return (
    'has been asked only once about this moment so far, so there is no history to compare. One ' +
    'Snapshot is a single answer, not a Forecast moving; the next poll appends another.'
  )
}
