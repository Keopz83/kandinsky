import { SHAPE_MIME } from './Canvas'
import type { AlignDirection, Tool } from './types'

interface Props {
  hasDiagram: boolean
  tool: Tool
  onAddRectangle: () => void
  onToolChange: (tool: Tool) => void
  selectedShapeCount: number
  onAlign: (direction: AlignDirection) => void
  onAutoLayout: () => void
}

export function Sidebar(props: Props) {
  const hasDiagram = props.hasDiagram

  return (
    <aside className="sidebar">
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

      <section className="palette">
        <h2>Arrange</h2>
        <button
          className="palette-item"
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
          className="palette-item"
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
          className="palette-item"
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
