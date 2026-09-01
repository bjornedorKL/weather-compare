/**
 * Turning a Symbol into something a reader can see. The Symbol vocabulary is MET Norway's
 * (ADR-0002) and Yr's icon set is keyed by exactly those names, so no mapping table is needed
 * between the two — only a lookup, and a graceful nothing when the Symbol is absent or unknown.
 */

const iconUrls = import.meta.glob('./weathericons/svg/*.svg', {
  eager: true,
  query: '?url',
  import: 'default',
}) as Record<string, string>

const iconBySymbol: Record<string, string> = Object.fromEntries(
  Object.entries(iconUrls).map(([path, url]) => [
    path.slice(path.lastIndexOf('/') + 1, -'.svg'.length),
    url,
  ]),
)

/**
 * English names, taken from MET's own `legend.csv`. Keyed by the Symbol with its time-of-day
 * variant removed, since `clearsky_day` and `clearsky_night` are one condition seen twice.
 * The double `s` in `lightssleetshowersandthunder` is MET's spelling, not a typo here.
 */
const LABELS: Record<string, string> = {
  clearsky: 'Clear sky',
  cloudy: 'Cloudy',
  fair: 'Fair',
  fog: 'Fog',
  heavyrain: 'Heavy rain',
  heavyrainandthunder: 'Heavy rain and thunder',
  heavyrainshowers: 'Heavy rain showers',
  heavyrainshowersandthunder: 'Heavy rain showers and thunder',
  heavysleet: 'Heavy sleet',
  heavysleetandthunder: 'Heavy sleet and thunder',
  heavysleetshowers: 'Heavy sleet showers',
  heavysleetshowersandthunder: 'Heavy sleet showers and thunder',
  heavysnow: 'Heavy snow',
  heavysnowandthunder: 'Heavy snow and thunder',
  heavysnowshowers: 'Heavy snow showers',
  heavysnowshowersandthunder: 'Heavy snow showers and thunder',
  lightrain: 'Light rain',
  lightrainandthunder: 'Light rain and thunder',
  lightrainshowers: 'Light rain showers',
  lightrainshowersandthunder: 'Light rain showers and thunder',
  lightsleet: 'Light sleet',
  lightsleetandthunder: 'Light sleet and thunder',
  lightsleetshowers: 'Light sleet showers',
  lightsnow: 'Light snow',
  lightsnowandthunder: 'Light snow and thunder',
  lightsnowshowers: 'Light snow showers',
  lightssleetshowersandthunder: 'Light sleet showers and thunder',
  lightssnowshowersandthunder: 'Light snow showers and thunder',
  partlycloudy: 'Partly cloudy',
  rain: 'Rain',
  rainandthunder: 'Rain and thunder',
  rainshowers: 'Rain showers',
  rainshowersandthunder: 'Rain showers and thunder',
  sleet: 'Sleet',
  sleetandthunder: 'Sleet and thunder',
  sleetshowers: 'Sleet showers',
  sleetshowersandthunder: 'Sleet showers and thunder',
  snow: 'Snow',
  snowandthunder: 'Snow and thunder',
  snowshowers: 'Snow showers',
  snowshowersandthunder: 'Snow showers and thunder',
}

const VARIANTS: Record<string, string> = {
  day: 'day',
  night: 'night',
  polartwilight: 'polar twilight',
}

/** The vendored Yr icon for a Symbol, or null when there is no Symbol or no icon for it. */
export function symbolIcon(symbol: string | null): string | null {
  if (symbol === null) {
    return null
  }

  return iconBySymbol[symbol] ?? null
}

/**
 * A Symbol in words. Falls back to the raw Symbol for a name we do not know, which is more
 * use to a reader — and to whoever has to debug it — than nothing at all.
 */
export function symbolLabel(symbol: string | null): string | null {
  if (symbol === null) {
    return null
  }

  const separator = symbol.lastIndexOf('_')
  const variant = separator === -1 ? null : VARIANTS[symbol.slice(separator + 1)] ?? null
  const base = variant === null ? symbol : symbol.slice(0, separator)
  const label = LABELS[base]

  if (label === undefined) {
    return symbol
  }

  return variant === null ? label : `${label} (${variant})`
}
