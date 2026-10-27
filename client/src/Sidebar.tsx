import { useRef } from 'react'
import { SHAPE_MIME } from './Canvas'
import type { DiagramSummary, Format, Tool } from './types'

interface Props {
  diagrams: DiagramSummary[]
  currentId: string | undefined
  tool: Tool
  onNew: () => void
  onOpen: (id: string) => void
  onSave: () => void
  onImport: (file: File) => void
  onExport: (format: Format) => void
  onAddRectangle: () => void
  onToolChange: (tool: Tool) => void
}

export function Sidebar(props: Props) {
  const fileInput = useRef<HTMLInputElement>(null)
  const hasDiagram = props.currentId !== undefined

  return (
    <aside className="sidebar">
      <h1>Kandinsky</h1>

      <nav className="menu">
        <h2>Diagram</h2>
        <button onClick={props.onNew}>New</button>
        <select
          aria-label="Open diagram"
          value={props.currentId ?? ''}
          disabled={props.diagrams.length === 0}
          onChange={(e) => props.onOpen(e.target.value)}
        >
          <option value="" disabled>
            Open…
          </option>
          {props.diagrams.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </select>
        <button onClick={props.onSave} disabled={!hasDiagram}>
          Save
        </button>
        <button onClick={() => fileInput.current?.click()}>Import…</button>
        <input
          ref={fileInput}
          type="file"
          hidden
          accept=".puml,.plantuml,.pu,.iuml,.mmd,.mermaid,.txt"
          onChange={(e) => {
            const file = e.target.files?.[0]
            e.target.value = ''
            if (file) {
              props.onImport(file)
            }
          }}
        />
        <div className="export">
          <span>Export</span>
          <button onClick={() => props.onExport('plantuml')} disabled={!hasDiagram}>
            PlantUML
          </button>
          <button onClick={() => props.onExport('mermaid')} disabled={!hasDiagram}>
            Mermaid
          </button>
        </div>
      </nav>

      <section className="palette">
        <h2>Palette</h2>
        <button
          className="palette-item"
          disabled={!hasDiagram}
          draggable={hasDiagram}
          title="Click or drag onto the canvas"
          onClick={props.onAddRectangle}
          onDragStart={(e) => {
            e.dataTransfer.setData(SHAPE_MIME, 'rectangle')
            e.dataTransfer.effectAllowed = 'copy'
          }}
        >
          <svg width="28" height="18" aria-hidden="true">
            <rect x="1" y="1" width="26" height="16" rx="2" />
          </svg>
          Rectangle
        </button>
        <button
          className={`palette-item ${props.tool === 'edge' ? 'active' : ''}`}
          disabled={!hasDiagram}
          title="Click the source rectangle, then the target"
          onClick={() => props.onToolChange(props.tool === 'edge' ? 'select' : 'edge')}
        >
          <svg width="28" height="18" aria-hidden="true">
            <line x1="1" y1="9" x2="21" y2="9" />
            <path d="M20,4 L27,9 L20,14 z" />
          </svg>
          Directed edge
        </button>
      </section>
    </aside>
  )
}
