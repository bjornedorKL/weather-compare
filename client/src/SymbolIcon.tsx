import { symbolIcon, symbolLabel } from './symbols.ts'

type Props = {
  symbol: string | null
  size: 'large' | 'small'
}

/**
 * A Symbol drawn as Yr's icon. A Forecast can carry no Symbol at all — MET's last timestep
 * has no summarised period — and a Provider could one day send a name we have no icon for.
 * Neither is an error, so both fall back to a placeholder rather than a broken image.
 */
export function SymbolIcon({ symbol, size }: Props) {
  const icon = symbolIcon(symbol)
  const label = symbolLabel(symbol)

  if (icon === null) {
    return (
      <span
        className={`symbol symbol--${size} symbol--absent`}
        role="img"
        aria-label={label ?? 'No symbol'}
        title={label ?? 'This Forecast carries no Symbol'}
      >
        ?
      </span>
    )
  }

  return <img className={`symbol symbol--${size}`} src={icon} alt={label ?? ''} title={label ?? ''} />
}
