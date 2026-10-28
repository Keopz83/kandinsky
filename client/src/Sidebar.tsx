import { SHAPE_MIME } from './Canvas'
import type { ReactNode } from 'react'
import type { AlignDirection, ShapeKind, Tool } from './types'

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
    <aside className="sidebar p-2 border-end">
      <div className="palette-panel d-flex flex-column gap-4">
        <section className="palette d-flex flex-column gap-1">
          <h2 className="small text-uppercase fw-semibold text-body-secondary mb-1">Shapes</h2>
          <ShapeTool kind="rectangle" label="Rectangle" {...props}>
            <rect x="1" y="1" width="26" height="16" rx="2" />
          </ShapeTool>
          <ShapeTool kind="rounded" label="Rounded" {...props}>
            <rect x="1" y="1" width="26" height="16" rx="6" />
          </ShapeTool>
          <ShapeTool kind="database" label="Database" {...props}>
            <path className="cylinder" d="M5,4 A9,3 0 0 1 23,4 V14 A9,3 0 0 1 5,14 Z" />
            <path className="cylinder" d="M5,4 A9,3 0 0 0 23,4" />
          </ShapeTool>
          <ShapeTool kind="group" label="Group" {...props}>
            <rect className="group-icon" x="1" y="1" width="26" height="16" rx="2" />
            <line className="group-icon" x1="1" y1="6" x2="27" y2="6" />
          </ShapeTool>
        </section>

        <section className="palette d-flex flex-column gap-1">
          <h2 className="small text-uppercase fw-semibold text-body-secondary mb-1">Flowchart</h2>
          <ShapeTool kind="start" label="Start" {...props}>
            <circle className="filled" cx="14" cy="9" r="6" />
          </ShapeTool>
          <ShapeTool kind="end" label="End" {...props}>
            <circle cx="14" cy="9" r="7.5" />
            <circle className="filled" cx="14" cy="9" r="4" />
          </ShapeTool>
          <ShapeTool kind="terminal" label="Terminal" {...props}>
            <rect x="1" y="3" width="26" height="12" rx="6" />
          </ShapeTool>
          <ShapeTool kind="decision" label="Decision" {...props}>
            <polygon points="14,1 27,9 14,17 1,9" />
          </ShapeTool>
          <ShapeTool kind="io" label="Input / output" {...props}>
            <polygon points="7,2 27,2 21,16 1,16" />
          </ShapeTool>
          <ShapeTool kind="subroutine" label="Subroutine" {...props}>
            <rect x="1" y="2" width="26" height="14" />
            <line x1="5" y1="2" x2="5" y2="16" />
            <line x1="23" y1="2" x2="23" y2="16" />
          </ShapeTool>
          <ShapeTool kind="document" label="Document" {...props}>
            <path className="shape-icon" d="M1,1 H27 V13 C20,9 8,19 1,13 Z" />
          </ShapeTool>
          <ShapeTool kind="preparation" label="Preparation" {...props}>
            <polygon points="6,2 22,2 27,9 22,16 6,16 1,9" />
          </ShapeTool>
          <ShapeTool kind="manual" label="Manual operation" {...props}>
            <polygon points="1,2 27,2 22,16 6,16" />
          </ShapeTool>
          <ShapeTool kind="connector" label="Connector" {...props}>
            <circle cx="14" cy="9" r="8" />
          </ShapeTool>
        </section>

        <section className="palette d-flex flex-column gap-1">
          <h2 className="small text-uppercase fw-semibold text-body-secondary mb-1">Edges</h2>
          <EdgeTool edgeTool="edge" label="Directed edge" {...props}>
            <line x1="1" y1="9" x2="21" y2="9" />
            <path d="M20,4 L27,9 L20,14 z" />
          </EdgeTool>
          <EdgeTool edgeTool="angled-edge" label="Angled edge" {...props}>
            <polyline className="line-icon" points="1,3 12,3 12,14 21,14" />
            <path d="M20,10 L27,14 L20,18 z" />
          </EdgeTool>
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
      </div>
    </aside>
  )
}

interface ShapeToolProps extends Props {
  kind: ShapeKind
  label: string
  children: ReactNode
}

/** A palette button for a shape: click, then click on the canvas to place it, or drag it onto the canvas. */
function ShapeTool({ kind, label, children, hasDiagram, tool, onToolChange }: ShapeToolProps) {
  return (
    <button
      type="button"
      className={`btn btn-sm palette-item ${tool === kind ? 'btn-primary active' : 'btn-light'}`}
      disabled={!hasDiagram}
      draggable={hasDiagram}
      title="Click, then click on the canvas to place it, or drag onto the canvas"
      onClick={() => onToolChange(tool === kind ? 'select' : kind)}
      onDragStart={(e) => {
        e.dataTransfer.setData(SHAPE_MIME, kind)
        e.dataTransfer.effectAllowed = 'copy'
      }}
    >
      <svg width="28" height="18" aria-hidden="true">
        {children}
      </svg>
      {label}
    </button>
  )
}

interface EdgeToolProps extends Props {
  edgeTool: 'edge' | 'angled-edge'
  label: string
  children: ReactNode
}

/** A palette button for drawing edges: straight or angled. Right-click an edge to switch it later. */
function EdgeTool({ edgeTool, label, children, ...props }: EdgeToolProps) {
  const active = props.tool === edgeTool
  return (
    <button
      type="button"
      className={`btn btn-sm palette-item ${active ? 'btn-primary active' : 'btn-light'}`}
      disabled={!props.hasDiagram}
      title="Press on the source shape and drag onto the target. Right-click an edge to switch between straight and angled."
      onClick={() => props.onToolChange(active ? 'select' : edgeTool)}
    >
      <svg width="28" height="18" aria-hidden="true">
        {children}
      </svg>
      {label}
    </button>
  )
}
