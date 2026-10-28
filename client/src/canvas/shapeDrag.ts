import type { Point } from '../geometry/point'
import type { Edge, Shape } from '../types'

const MIN_SHAPE_SIZE = 20

/** Moving, copying or resizing shapes by dragging; edges that are carried along are kept for copying. */
export interface ShapeDrag {
  kind: 'move' | 'resize'
  copy: boolean
  start: Point
  /** The shape or edge that was grabbed. */
  target: string
  originals: Shape[]
  currents: Shape[]
  /** Selected edges, recreated between the copies when copying. */
  edges: Edge[]
  /** False once released, while the result is being saved. */
  active: boolean
}

const isUnchanged = (a: Shape, b: Shape) => a.x === b.x && a.y === b.y && a.width === b.width && a.height === b.height

export const hasChanged = (drag: ShapeDrag) => drag.currents.some((current, i) => !isUnchanged(current, drag.originals[i]))

/** The drag with its shapes following the pointer: resized from the bottom right corner, or moved. */
export function draggedTo(drag: ShapeDrag, pointer: Point): ShapeDrag {
  const dx = Math.round(pointer.x - drag.start.x)
  const dy = Math.round(pointer.y - drag.start.y)
  if (drag.kind === 'resize') {
    const original = drag.originals[0]
    const width = Math.max(MIN_SHAPE_SIZE, Math.round(original.width + pointer.x - drag.start.x))
    const height = Math.max(MIN_SHAPE_SIZE, Math.round(original.height + pointer.y - drag.start.y))
    return { ...drag, currents: [{ ...original, width, height }] }
  }
  // Clamp the shapes as a whole at the canvas edge, so their relative layout is preserved.
  const clampedDx = Math.max(dx, -Math.min(...drag.originals.map((s) => s.x)))
  const clampedDy = Math.max(dy, -Math.min(...drag.originals.map((s) => s.y)))
  return { ...drag, currents: drag.originals.map((s) => ({ ...s, x: s.x + clampedDx, y: s.y + clampedDy })) }
}
