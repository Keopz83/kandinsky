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
    <div className="tabs nav nav-tabs flex-nowrap px-2 pt-2 bg-body-tertiary" role="tablist" aria-label="Whiteboards">
      {diagrams.map((d) => (
        <div key={d.id} className={`tab nav-link d-flex align-items-center p-0 ${d.id === currentId ? 'active' : ''}`} title={`${d.name} — double-click to rename`}>
          {editingId === d.id ? (
            <LabelInput
              className="tab-name-input form-control form-control-sm"
              ariaLabel="Diagram name"
              value={d.name}
              autoFocus
              onCommit={(name) => onRename(d.id, name)}
              onDone={() => setEditingId(null)}
            />
          ) : (
            <button
              type="button"
              className={`tab-name btn btn-sm border-0 text-truncate ${d.id === currentId ? 'fw-semibold' : 'text-body-secondary'}`}
              role="tab"
              aria-selected={d.id === currentId}
              onClick={() => d.id !== currentId && onSelect(d.id)}
              onDoubleClick={() => setEditingId(d.id)}
            >
              {d.name}
            </button>
          )}
          <button type="button" className="tab-close btn-close me-2" aria-label={`Delete ${d.name}`} title="Delete whiteboard" onClick={() => onClose(d)} />
        </div>
      ))}
      <button type="button" className="tab-new nav-link px-3 py-1" aria-label="New whiteboard" title="New whiteboard" onClick={onNew}>
        +
      </button>
    </div>
  )
}
