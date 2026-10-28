import type { PointerEvent } from 'react'
import { LabelInput } from '../LabelInput'
import { labelBelow, labelBox } from '../geometry/shapeGeometry'
import { ShapeOutline } from '../ShapeOutline'
import type { Shape } from '../types'

const RESIZE_HANDLE_SIZE = 10

/** The label of a shape as read-only HTML text, wrapped like on the whiteboard. */
export function ShapeLabel({ shape }: { shape: Shape }) {
  return <div className={`label ${labelBelow(shape) ? 'below' : ''}`}>{shape.label}</div>
}

interface Props {
  shape: Shape
  /** Extra CSS classes for the selection state. */
  stateClasses: string
  isEditing: boolean
  showResizeHandle: boolean
  onMoveStart: (e: PointerEvent) => void
  onResizeStart: (e: PointerEvent) => void
  onEditStart: () => void
  onLabelCommit: (label: string) => void
  onEditDone: () => void
}

/** A shape on the whiteboard with its label (editable on double-click) and, when selected alone, a resize handle. */
export function ShapeView({ shape, stateClasses, isEditing, showResizeHandle, onMoveStart, onResizeStart, onEditStart, onLabelCommit, onEditDone }: Props) {
  const handleOffset = RESIZE_HANDLE_SIZE / 2
  return (
    <g className={`shape ${shape.kind} ${stateClasses}`}>
      <ShapeOutline shape={shape} onPointerDown={onMoveStart} onDoubleClick={onEditStart} />
      <foreignObject {...labelBox(shape)} pointerEvents={isEditing ? undefined : 'none'}>
        {isEditing ? (
          <LabelInput className="label-input" ariaLabel="Shape label" autoFocus value={shape.label} onCommit={onLabelCommit} onDone={onEditDone} />
        ) : (
          <ShapeLabel shape={shape} />
        )}
      </foreignObject>
      {showResizeHandle && (
        <rect
          className="handle"
          x={shape.x + shape.width - handleOffset}
          y={shape.y + shape.height - handleOffset}
          width={RESIZE_HANDLE_SIZE}
          height={RESIZE_HANDLE_SIZE}
          onPointerDown={onResizeStart}
        />
      )}
    </g>
  )
}
