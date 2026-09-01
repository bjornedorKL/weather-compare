import { useEffect, useState } from 'react'
import type { LocationForecasts } from './api.ts'
import { fetchLocations } from './api.ts'
import { LocationCard } from './LocationCard.tsx'

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
  const [locations, setLocations] = useState<LocationForecasts[] | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  const now = useNow()

  useEffect(() => {
    const attempt = new AbortController()

    fetchLocations(attempt.signal)
      .then(setLocations)
      .catch((error: Error) => {
        if (error.name !== 'AbortError') {
          setFailure(error.message)
        }
      })

    return () => attempt.abort()
  }, [])

  return (
    <div className="page">
      <header className="page__header">
        <h1>Weather Comparison</h1>
        <p className="page__lead">
          The newest Forecast Snapshot for every Location we track
          {locations === null ? '' : ` — ${locations.length} of them`}.
        </p>
      </header>

      <main>
        {failure !== null && <p className="page__failure">Could not load Locations: {failure}</p>}
        {failure === null && locations === null && <p className="page__loading">Loading Locations…</p>}
        {locations !== null && (
          <div className="grid">
            {locations.map((location) => (
              <LocationCard key={`${location.latitude},${location.longitude}`} location={location} now={now} />
            ))}
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
