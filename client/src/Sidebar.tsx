import { SHAPE_MIME } from './Canvas'
import type { AlignDirection, Tool } from './types'

interface Props {
  hasDiagram: boolean
  tool: Tool
  onToolChange: (tool: Tool) => void
  selectedShapeCount: number
  onAlign: (direction: AlignDirection) => void
  onAutoLayout: () => void
}

export function Sidebar(props: Props) {
  const hasDiagram = props.hasDiagram

  return (
    <aside className="sidebar d-flex flex-column gap-4 p-3 bg-body-tertiary border-end">
      <section className="palette d-flex flex-column gap-2">
        <h2 className="small text-uppercase fw-semibold text-body-secondary mb-1">Palette</h2>
        <button
          type="button"
          className={`btn btn-sm palette-item ${props.tool === 'rectangle' ? 'btn-primary active' : 'btn-light'}`}
          disabled={!hasDiagram}
          draggable={hasDiagram}
          title="Click, then click on the canvas to place it, or drag onto the canvas"
          onClick={() => props.onToolChange(props.tool === 'rectangle' ? 'select' : 'rectangle')}
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
          type="button"
          className={`btn btn-sm palette-item ${props.tool === 'edge' ? 'btn-primary active' : 'btn-light'}`}
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

      <section className="palette d-flex flex-column gap-2">
        <h2 className="small text-uppercase fw-semibold text-body-secondary mb-1">Arrange</h2>
        <button
          type="button"
          className="btn btn-sm btn-light palette-item"
          disabled={props.selectedShapeCount < 2}
          title="Line up the selected rectangles in a row, centered on the first selected one"
          onClick={() => props.onAlign('horizontal')}
        >
          <svg width="28" height="18" aria-hidden="true">
            <rect x="1" y="5" width="7" height="8" />
            <rect x="11" y="1" width="7" height="16" />
            <rect x="21" y="4" width="6" height="10" />
            <line className="guide" x1="0" y1="9" x2="28" y2="9" />
          </svg>
          Align horizontally
        </button>
        <button
          type="button"
          className="btn btn-sm btn-light palette-item"
          disabled={props.selectedShapeCount < 2}
          title="Line up the selected rectangles in a column, centered on the first selected one"
          onClick={() => props.onAlign('vertical')}
        >
          <svg width="28" height="18" aria-hidden="true">
            <rect x="9" y="1" width="10" height="4" />
            <rect x="5" y="7" width="18" height="4" />
            <rect x="10" y="13" width="8" height="4" />
            <line className="guide" x1="14" y1="0" x2="14" y2="18" />
          </svg>
          Align vertically
        </button>
        <button
          type="button"
          className="btn btn-sm btn-light palette-item"
          disabled={!hasDiagram}
          title="Arrange all rectangles following the edge direction"
          onClick={props.onAutoLayout}
        >
          <svg width="28" height="18" aria-hidden="true">
            <rect x="10" y="1" width="8" height="5" />
            <rect x="2" y="12" width="8" height="5" />
            <rect x="18" y="12" width="8" height="5" />
            <line className="guide" x1="14" y1="6" x2="6" y2="12" />
            <line className="guide" x1="14" y1="6" x2="22" y2="12" />
          </svg>
          Auto-layout
        </button>
      </section>
    </aside>
  )
}
