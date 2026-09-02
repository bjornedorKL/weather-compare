import { useCallback, useEffect, useState } from 'react'
import type { KnownLocation, LocationForecasts, TypedLocation } from './api.ts'
import {
  WriteRefused,
  fetchKnownLocations,
  fetchLocations,
  renameLocation,
  setTracked,
  trackCoordinate,
} from './api.ts'
import { locationKey } from './forecasts.ts'

/** Something the page has to tell the reader after a write, and whether it went well. */
export type Notice = {
  tone: 'ok' | 'problem'
  text: string
}

/**
 * The Catalogue as the page holds it: the two reads it needs, and the four writes it offers.
 *
 * The reads are separate endpoints answering separate questions — `/api/locations` is the
 * Catalogue with its Forecasts, which is what the grid draws; `/api/locations/known` is every
 * Location we know with its id and tracked flag, which is what a write has to name. They are
 * fetched side by side and matched on the coordinate that identifies a Location.
 *
 * Every write reloads both rather than patching state in place. A write changes what the API
 * would answer — a Location that leaves the Catalogue leaves the grid, one that joins it arrives
 * with no Forecast Snapshot yet — and re-asking is both shorter and less likely to be a lie.
 */
export type Catalogue = {
  /** Tracked Locations with their newest Snapshots. Null until the first read lands. */
  locations: LocationForecasts[] | null
  /** Known Locations that are not in the Catalogue: the ones that can be tracked again. */
  untracked: KnownLocation[]
  /** Why the last read failed, if it did. */
  failure: string | null
  /** What the last write did, in words for the reader. */
  notice: Notice | null
  /** Field name as the API spells it (`Latitude`) to what is wrong with it, for use by inputs. */
  fieldProblems: Readonly<Record<string, string>>
  /** A write is in flight; controls stand down until it lands. */
  busy: boolean
  /** The id the API knows a tracked Location by, or null if the two reads disagreed. */
  idOf: (location: LocationForecasts) => number | null
  untrack: (id: number) => void
  trackKnown: (id: number) => void
  trackTyped: (typed: TypedLocation) => void
  /** Gives a Location we know a different label. Changes nothing else about it. */
  rename: (id: number, name: string) => void
}

/**
 * What the page says once a Location has left the Catalogue. Deliberately not "deleted", because
 * nothing is: the store cannot delete a Forecast Snapshot and does not try. The gap is worth
 * naming out loud because it can never be filled in — a Provider cannot be asked, later, what it
 * said last week (CONTEXT.md).
 */
function stoppedTracking(name: string): string {
  return (
    `Stopped tracking ${name}. Every Forecast Snapshot already recorded is kept — only new ones ` +
    'stop. Its history gains a gap for as long as it stays untracked, and a gap can never be ' +
    'filled in afterwards.'
  )
}

function trackingAgain(name: string): string {
  return (
    `Tracking ${name} again. New Forecast Snapshots resume at the next poll; the stretch it spent ` +
    'untracked stays a gap in its history.'
  )
}

/**
 * A rename is the one write that changes nothing about which Location it is, which is worth
 * saying: the coordinate is what identifies a Location, so the Forecast Snapshots recorded under
 * the old name are the same Location's history, unchanged and still there (CONTEXT.md).
 */
function renamed(name: string): string {
  return (
    `Now shown as ${name}. It is the same Location — same coordinate, same Forecast Snapshots. ` +
    'The name is only a label.'
  )
}

/**
 * A 200 from the write means the coordinate was already known, and the Location that came back
 * may well carry a different name from the one typed — a Location is its coordinate, and the name
 * on file wins. Saying which Location was actually got is the whole point of the message.
 */
function alreadyKnown(name: string, wasTracked: boolean): string {
  return wasTracked
    ? `That coordinate is already tracked as ${name}. A Location is its coordinate, so nothing was added.`
    : `That coordinate is one we already know as ${name}, so it was tracked again rather than added a second time.`
}

/** Both reads at once. They answer different questions, and the page needs both to be current. */
async function readBoth(signal?: AbortSignal) {
  const [catalogue, everything] = await Promise.all([
    fetchLocations(signal),
    fetchKnownLocations(signal),
  ])

  return { catalogue, everything }
}

export function useCatalogue(): Catalogue {
  const [locations, setLocations] = useState<LocationForecasts[] | null>(null)
  const [known, setKnown] = useState<KnownLocation[]>([])
  const [failure, setFailure] = useState<string | null>(null)
  const [notice, setNotice] = useState<Notice | null>(null)
  const [fieldProblems, setFieldProblems] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)

  const receive = useCallback((read: Awaited<ReturnType<typeof readBoth>>) => {
    setLocations(read.catalogue)
    setKnown(read.everything)
    setFailure(null)
  }, [])

  useEffect(() => {
    const attempt = new AbortController()

    readBoth(attempt.signal)
      .then(receive)
      .catch((error: Error) => {
        if (error.name !== 'AbortError') {
          setFailure(error.message)
        }
      })

    return () => attempt.abort()
  }, [receive])

  /**
   * Runs one write, then re-reads, then says what happened. The re-read comes before the notice
   * so the reader never sees "now tracking Ålesund" over a grid that does not have it yet.
   */
  const write = useCallback(
    (attempt: () => Promise<string>) => {
      setBusy(true)
      setFieldProblems({})

      attempt()
        .then(async (said) => {
          receive(await readBoth())
          setNotice({ tone: 'ok', text: said })
        })
        .catch((error: Error) => {
          setFieldProblems(error instanceof WriteRefused ? error.fields : {})
          setNotice({ tone: 'problem', text: error.message })
        })
        .finally(() => setBusy(false))
    },
    [receive],
  )

  const untrack = useCallback(
    (id: number) => write(async () => stoppedTracking((await setTracked(id, false)).name)),
    [write],
  )

  const trackKnown = useCallback(
    (id: number) => write(async () => trackingAgain((await setTracked(id, true)).name)),
    [write],
  )

  const trackTyped = useCallback(
    (typed: TypedLocation) =>
      write(async () => {
        const { location, created } = await trackCoordinate(typed)
        /* Asked of the Location that came back, not of the coordinate typed: the API truncates a
           coordinate to four decimals, so what was sent need not key to what was matched. */
        const wasTracked = known.some(
          (candidate) => candidate.id === location.id && candidate.tracked,
        )

        return created
          ? `Now tracking ${location.name}. No Provider has been asked about it yet, so it has no Forecast Snapshot to show.`
          : alreadyKnown(location.name, wasTracked)
      }),
    [known, write],
  )

  const rename = useCallback(
    (id: number, name: string) => write(async () => renamed((await renameLocation(id, name)).name)),
    [write],
  )

  const idOf = useCallback(
    (location: LocationForecasts) => {
      const key = locationKey(location)

      return known.find((candidate) => locationKey(candidate) === key)?.id ?? null
    },
    [known],
  )

  return {
    locations,
    untracked: known.filter((location) => !location.tracked),
    failure,
    notice,
    fieldProblems,
    busy,
    idOf,
    untrack,
    trackKnown,
    trackTyped,
    rename,
  }
}
