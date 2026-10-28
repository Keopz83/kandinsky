import type { MouseEvent, PointerEvent } from 'react'
import { LabelInput } from '../LabelInput'
import type { Point } from '../geometry/point'
import { pathMidpoint, toPoints } from '../geometry/polyline'
import type { Edge } from '../types'

const LABEL_INPUT_WIDTH = 160
const LABEL_INPUT_HEIGHT = 28
/** Distance of the label's baseline above the edge's midpoint. */
const LABEL_OFFSET = 6

interface Props {
  edge: Edge
  path: Point[]
  isSelected: boolean
  isEditing: boolean
  onPointerDown: (e: PointerEvent) => void
  onEditStart: () => void
  onContextMenu: (e: MouseEvent) => void
  onLabelCommit: (label: string) => void
  onEditDone: () => void
}

/** An edge on the whiteboard: an arrow along the path with a wide invisible hit area and its label (editable on double-click). */
export function EdgeView({ edge, path, isSelected, isEditing, onPointerDown, onEditStart, onContextMenu, onLabelCommit, onEditDone }: Props) {
  const middle = pathMidpoint(path)
  return (
    <g className={`edge ${isSelected ? 'selected' : ''}`} onPointerDown={onPointerDown} onDoubleClick={onEditStart} onContextMenu={onContextMenu}>
      <polyline className="hit" points={toPoints(path)} />
      <polyline className="line" points={toPoints(path)} markerEnd={`url(#${isSelected ? 'arrow-selected' : 'arrow'})`} />
      {isEditing ? (
        <foreignObject x={middle.x - LABEL_INPUT_WIDTH / 2} y={middle.y - LABEL_INPUT_HEIGHT / 2} width={LABEL_INPUT_WIDTH} height={LABEL_INPUT_HEIGHT}>
          <LabelInput className="label-input" ariaLabel="Edge label" autoFocus value={edge.label ?? ''} onCommit={onLabelCommit} onDone={onEditDone} />
        </foreignObject>
      ) : (
        edge.label && (
          <text x={middle.x} y={middle.y - LABEL_OFFSET} textAnchor="middle">
            {edge.label}
          </text>
        )
      )}
    </g>
  )
}

/** Arrow heads for edges, normal and selected. */
export function ArrowMarkers() {
  return (
    <defs>
      {['arrow', 'arrow-selected'].map((id) => (
        <marker key={id} id={id} viewBox="0 0 10 10" refX="10" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" className={id} />
        </marker>
      ))}
    </defs>
  )
}
