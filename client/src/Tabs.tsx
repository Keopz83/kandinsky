import { useState } from 'react'
import { LabelInput } from './LabelInput'
import type { DiagramSummary } from './types'

interface Props {
  diagrams: DiagramSummary[]
  currentId: string | undefined
  onSelect: (id: string) => void
  onRename: (id: string, name: string) => void
  onClose: (diagram: DiagramSummary) => void
  onNew: () => void
}

export function Tabs({ diagrams, currentId, onSelect, onRename, onClose, onNew }: Props) {
  const [editingId, setEditingId] = useState<string | null>(null)

  return (
    <div className="tabs" role="tablist" aria-label="Whiteboards">
      {diagrams.map((d) => (
        <div key={d.id} className={`tab ${d.id === currentId ? 'active' : ''}`} title={`${d.name} — double-click to rename`}>
          {editingId === d.id ? (
            <LabelInput
              className="tab-name-input"
              ariaLabel="Diagram name"
              value={d.name}
              autoFocus
              onCommit={(name) => onRename(d.id, name)}
              onDone={() => setEditingId(null)}
            />
          ) : (
            <button
              className="tab-name"
              role="tab"
              aria-selected={d.id === currentId}
              onClick={() => d.id !== currentId && onSelect(d.id)}
              onDoubleClick={() => setEditingId(d.id)}
            >
              {d.name}
            </button>
          )}
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
