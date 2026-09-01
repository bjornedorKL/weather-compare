import { formatClock, formatDegrees, formatDuration } from './format.ts'
import type { ForecastHistoryGap, HistorySegment, ProviderForecastHistory } from './history.ts'
import { segmentsOf } from './history.ts'

/**
 * One Provider's answers about a single future moment, plotted against when it gave them. The
 * x axis is Issued At — when we asked — and the y axis is what it said the temperature would be.
 * A falling line is a Provider that cooled its mind, not a Provider getting it wrong.
 *
 * The line is drawn in pieces. It breaks wherever the record does: at a gap, where a Snapshot
 * was due and none was recorded, and at a Snapshot that was recorded but said nothing about
 * this moment. Joining across either would draw a value nothing in the store ever held —
 * `CONTEXT.md` is explicit that an incomplete record must not be read as continuous.
 *
 * Like the Snapshot chart it sits under, the drawing is stretched to the reader's width, so it
 * carries no text of its own: every label is HTML positioned over the plot, and the rows beneath
 * it carry the same figures. It is hidden from assistive technology rather than read out badly.
 */

const WIDTH = 1000
const TOP = 10
const BOTTOM = 80
/** The plot's full height, including the strip under the axis where silent Snapshots are ticked. */
const HEIGHT = 100

/** Degrees of headroom either side, and a floor on the span so a steady Forecast is not a zigzag. */
const HEADROOM = 0.2
const MINIMUM_SPAN = 1

type Props = {
  history: ProviderForecastHistory
}

export function HistoryChart({ history }: Props) {
  const plot = plotOf(history)

  if (plot === null) {
    return null
  }

  const { x, y, percent, lowest, highest, segments, gaps, silent } = plot

  return (
    <figure className="history__chart">
      <div className="history__plot">
        <svg
          className="history__svg"
          viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          {gaps.map((gap) => (
            <g key={gap.from.toISOString()}>
              <rect
                className="history__gap"
                x={x(gap.due)}
                y="0"
                width={Math.max(x(gap.to) - x(gap.due), 0)}
                height={BOTTOM}
              />
              <line className="history__gap-edge" x1={x(gap.due)} x2={x(gap.due)} y1="0" y2={BOTTOM} />
              <line className="history__gap-edge" x1={x(gap.to)} x2={x(gap.to)} y1="0" y2={BOTTOM} />
            </g>
          ))}
          <line className="history__axis-line" x1="0" x2={WIDTH} y1={BOTTOM} y2={BOTTOM} />
          {segments.map((segment) => (
            <polyline
              className="history__line"
              key={segment[0].issuedAt.toISOString()}
              points={segment
                .map((point) => `${x(point.issuedAt)},${y(point.celsius)}`)
                .join(' ')}
            />
          ))}
          {silent.map((moment) => (
            <line
              className="history__silent"
              key={moment.toISOString()}
              x1={x(moment)}
              x2={x(moment)}
              y1={BOTTOM}
              y2={HEIGHT - 6}
            />
          ))}
        </svg>

        <div className="history__marks">
          {segments.flat().map((point) => (
            <span
              className="history__dot"
              key={point.issuedAt.toISOString()}
              style={{ left: percent(point.issuedAt), top: `${(y(point.celsius) / HEIGHT) * 100}%` }}
              title={`${formatClock(point.issuedAt)} · said ${formatDegrees(point.celsius)}`}
            />
          ))}
          {gaps.map((gap) => (
            <span
              className="history__gap-label"
              key={gap.from.toISOString()}
              style={{ left: percent(gap.due), width: percent(gap.to, gap.due) }}
            >
              no Snapshot · {formatDuration(gap.to.getTime() - gap.due.getTime())}
            </span>
          ))}
        </div>

        <span className="history__axis history__axis--high">{formatDegrees(highest)}</span>
        <span className="history__axis history__axis--low">{formatDegrees(lowest)}</span>
      </div>

      <figcaption className="history__caption">
        What {history.provider} said about this one moment, plotted against when it said it —
        older asks to the left. The line breaks wherever the record does: a shaded band is a stretch where a
        Snapshot was due and none was recorded, and a tick under the axis is a Snapshot that was
        recorded but described no Forecast for this moment. Neither is drawn through, because a
        gap can never be filled in afterwards.
      </figcaption>
    </figure>
  )
}

type PlottedGap = { from: Date; to: Date; due: Date }

type Plot = {
  x: (moment: Date) => number
  y: (celsius: number) => number
  percent: (moment: Date, from?: Date) => string
  lowest: number
  highest: number
  segments: HistorySegment[]
  gaps: PlottedGap[]
  silent: Date[]
}

/**
 * Everything the drawing needs, or null when there is nothing to draw: fewer than two Snapshots
 * carrying a temperature, or a run of Snapshots with no time between them. A single point is
 * emphatically not a chart — one dot with an axis around it invites a trend that is not there.
 */
function plotOf(history: ProviderForecastHistory): Plot | null {
  const segments = segmentsOf(history)
  const plotted = segments.flat()

  if (plotted.length < 2) {
    return null
  }

  const from = new Date(history.points[0].issuedAt).getTime()
  const to = new Date(history.points[history.points.length - 1].issuedAt).getTime()
  const span = to - from

  if (span <= 0) {
    return null
  }

  const readings = plotted.map((point) => point.celsius)
  const lowest = Math.min(...readings)
  const highest = Math.max(...readings)
  const floor = lowest - HEADROOM
  const degrees = Math.max(highest + HEADROOM - floor, MINIMUM_SPAN)
  const x = (moment: Date) => ((moment.getTime() - from) / span) * WIDTH

  return {
    x,
    y: (celsius: number) => BOTTOM - ((celsius - floor) / degrees) * (BOTTOM - TOP),
    percent: (moment, since) =>
      `${((x(moment) - (since === undefined ? 0 : x(since))) / WIDTH) * 100}%`,
    lowest,
    highest,
    segments,
    gaps: plottedGaps(history.gaps),
    silent: history.points
      .filter((point) => point.forecast === null || point.forecast.temperatureCelsius === null)
      .map((point) => new Date(point.issuedAt)),
  }
}

function plottedGaps(gaps: readonly ForecastHistoryGap[]): PlottedGap[] {
  return gaps.map((gap) => ({
    from: new Date(gap.fromIssuedAt),
    to: new Date(gap.toIssuedAt),
    due: new Date(gap.dueAt),
  }))
}
