import { useCallback, useEffect, useState } from 'react'
import { useCatalogue } from './catalogue.ts'
import { locationKey } from './forecasts.ts'
import { LocationCard } from './LocationCard.tsx'
import { LocationDetail } from './LocationDetail.tsx'
import { RenameUntracked } from './RenameLocation.tsx'
import { TrackLocation } from './TrackLocation.tsx'

/**
 * How often the clock the page renders against moves. It drives both "updated 12 minutes ago"
 * and which Forecast counts as the current one, so a page left open does not quietly go stale.
 */
const TICK_MILLISECONDS = 60_000

function useNow(): Date {
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    const tick = setInterval(() => setNow(new Date()), TICK_MILLISECONDS)

    return () => clearInterval(tick)
  }, [])

  return now
}

export default function App() {
  const catalogue = useCatalogue()
  /* Which Location is open, by coordinate. Deliberately not in the URL: no router, so no deep
     link and no browser back — the page swaps the grid for the detail in place. */
  const [openedKey, setOpenedKey] = useState<string | null>(null)
  const now = useNow()
  const close = useCallback(() => setOpenedKey(null), [])

  const { locations, failure, notice, busy, untracked, fieldProblems } = catalogue
  const opened = locations?.find((location) => locationKey(location) === openedKey) ?? null

  return (
    <div className="page">
      <header className="page__header">
        <h1>Weather Comparison</h1>
        <p className="page__lead">
          {opened !== null
            ? 'Every Forecast in this Location’s newest Snapshot. Pick a step to see what successive Snapshots said about that moment.'
            : `The newest Forecast Snapshot for every Location we track${
                locations === null ? '' : ` — ${locations.length} of them`
              }.`}
        </p>
      </header>

      <main>
        {failure !== null && <p className="page__failure">Could not load Locations: {failure}</p>}
        {failure === null && locations === null && <p className="page__loading">Loading Locations…</p>}
        {opened !== null && (
          <LocationDetail
            location={opened}
            locationId={catalogue.idOf(opened)}
            now={now}
            onClose={close}
          />
        )}
        {opened === null && (
          <TrackLocation
            busy={busy}
            fieldProblems={fieldProblems}
            onTrackKnown={catalogue.trackKnown}
            onTrackTyped={catalogue.trackTyped}
            untracked={untracked}
          />
        )}
        {/* A tracked Location is renamed from its card; these have no card to be renamed from. */}
        {opened === null && (
          <RenameUntracked busy={busy} onRename={catalogue.rename} untracked={untracked} />
        )}
        {notice !== null && opened === null && (
          <p
            className={`page__notice${notice.tone === 'problem' ? ' page__notice--problem' : ''}`}
            role="status"
          >
            {notice.text}
          </p>
        )}
        {locations !== null && opened === null && (
          <div className="grid">
            {locations.map((location) => {
              const id = catalogue.idOf(location)

              return (
                <LocationCard
                  busy={busy}
                  key={locationKey(location)}
                  location={location}
                  now={now}
                  onOpen={() => setOpenedKey(locationKey(location))}
                  onRename={id === null ? null : (name) => catalogue.rename(id, name)}
                  onUntrack={id === null ? null : () => catalogue.untrack(id)}
                />
              )
            })}
          </div>
        )}
      </main>

      <footer className="page__attribution">
        <p>
          Weather data from{' '}
          <a href="https://www.met.no/" rel="noreferrer noopener" target="_blank">
            MET Norway
          </a>{' '}
          (Locationforecast 2.0), licensed under{' '}
          <a href="https://creativecommons.org/licenses/by/4.0/" rel="noreferrer noopener" target="_blank">
            CC BY 4.0
          </a>{' '}
          and the{' '}
          <a href="https://data.norge.no/nlod/en/2.0" rel="noreferrer noopener" target="_blank">
            Norwegian Licence for Open Government Data (NLOD) 2.0
          </a>
          .
        </p>
        <p>
          Weather icons © 2015–2017{' '}
          <a href="https://github.com/metno/weathericons" rel="noreferrer noopener" target="_blank">
            Yr / MET Norway
          </a>
          , used under the MIT Licence.
        </p>
      </footer>
    </div>
  )
}
