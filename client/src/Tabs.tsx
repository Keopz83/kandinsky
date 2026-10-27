import type { DiagramSummary } from './types'

interface Props {
  diagrams: DiagramSummary[]
  currentId: string | undefined
  onSelect: (id: string) => void
  onClose: (diagram: DiagramSummary) => void
  onNew: () => void
}

export function Tabs({ diagrams, currentId, onSelect, onClose, onNew }: Props) {
  return (
    <div className="tabs" role="tablist" aria-label="Whiteboards">
      {diagrams.map((d) => (
        <div key={d.id} className={`tab ${d.id === currentId ? 'active' : ''}`} title={d.name}>
          <button
            className="tab-name"
            role="tab"
            aria-selected={d.id === currentId}
            onClick={() => d.id !== currentId && onSelect(d.id)}
          >
            {d.name}
          </button>
          <button className="tab-close" aria-label={`Delete ${d.name}`} title="Delete whiteboard" onClick={() => onClose(d)}>
            ×
          </button>
        </div>
      ))}
      <button className="tab-new" aria-label="New whiteboard" title="New whiteboard" onClick={onNew}>
        +
      </button>
    </div>
  )
}
