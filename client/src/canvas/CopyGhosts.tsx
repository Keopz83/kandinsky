import { edgePath } from '../geometry/edgeRouting'
import { toPoints } from '../geometry/polyline'
import { labelBox } from '../geometry/shapeGeometry'
import { drawOrder } from '../geometry/shapeHierarchy'
import { ShapeOutline } from '../ShapeOutline'
import type { ShapeDrag } from './shapeDrag'
import { ShapeLabel } from './ShapeView'

/** Semi-transparent previews of the copies while Shift+dragging. */
export function CopyGhosts({ drag }: { drag: ShapeDrag }) {
  const byId = new Map(drag.currents.map((c) => [c.id, c]))
  return (
    <g pointerEvents="none">
      {drag.edges.map((edge) => (
        <g key={edge.id} className="edge ghost">
          <polyline className="line" points={toPoints(edgePath(byId.get(edge.sourceId)!, byId.get(edge.targetId)!, edge))} markerEnd="url(#arrow)" />
        </g>
      ))}
      {drawOrder(drag.currents).map((shape) => (
        <g key={shape.id} className={`shape ${shape.kind} ghost`}>
          <ShapeOutline shape={shape} />
          <foreignObject {...labelBox(shape)}>
            <ShapeLabel shape={shape} />
          </foreignObject>
        </g>
      ))}
    </g>
  )
}
