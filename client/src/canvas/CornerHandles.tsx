import type { PointerEvent } from 'react'
import { edgePath } from '../geometry/edgeRouting'
import { corners } from '../geometry/shapeGeometry'
import type { Edge, Shape } from '../types'
import type { CornerDrag, EdgeEnd } from './cornerDrag'

const HANDLE_RADIUS = 5

interface Props {
  /** The edge as currently drawn, with a dragged corner applied. */
  edge: Edge
  from: Shape
  to: Shape
  cornerDrag: CornerDrag | null
  onDragStart: (e: PointerEvent, end: EdgeEnd) => void
}

/** Handles on the ends of an edge attached to a diamond; while one is dragged, the diamond's corners it can go to. */
export function CornerHandles({ edge, from, to, cornerDrag, onDragStart }: Props) {
  const path = edgePath(from, to, edge)
  const ends = [
    { end: 'source' as const, shape: from, point: path[0] },
    { end: 'target' as const, shape: to, point: path[path.length - 1] },
  ].filter(({ shape }) => shape.kind === 'decision')
  return (
    <g>
      {ends.map(({ end, shape, point }) =>
        cornerDrag?.end === end ? (
          corners(shape).map(({ corner, point }) => (
            <circle
              key={`${end}-${corner}`}
              className={`corner-target ${corner === cornerDrag.corner ? 'active' : ''}`}
              cx={point.x}
              cy={point.y}
              r={HANDLE_RADIUS}
            />
          ))
        ) : (
          <circle
            key={end}
            className="corner-handle"
            cx={point.x}
            cy={point.y}
            r={HANDLE_RADIUS}
            aria-label={`Move the ${end} end to another corner`}
            onPointerDown={(e) => onDragStart(e, end)}
          />
        ),
      )}
    </g>
  )
}
