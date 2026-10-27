interface Props {
  value: string
  onCommit: (value: string) => void
  onDone?: () => void
  autoFocus?: boolean
  className?: string
  ariaLabel: string
}

/** Text input that commits on Enter or blur and reverts on Escape. */
export function LabelInput({ value, onCommit, onDone, autoFocus, className, ariaLabel }: Props) {
  return (
    <input
      className={className}
      aria-label={ariaLabel}
      autoFocus={autoFocus}
      defaultValue={value}
      onFocus={(e) => e.currentTarget.select()}
      onPointerDown={(e) => e.stopPropagation()}
      onKeyDown={(e) => {
        if (e.key === 'Enter') {
          e.currentTarget.blur()
        }
        if (e.key === 'Escape') {
          e.currentTarget.value = value
          e.currentTarget.blur()
        }
      }}
      onBlur={(e) => {
        if (e.currentTarget.value !== value) {
          onCommit(e.currentTarget.value)
        }
        onDone?.()
      }}
    />
  )
}
