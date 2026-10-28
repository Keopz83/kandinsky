import type { Corner, Edge } from '../types'

export type EdgeEnd = 'source' | 'target'

/** Dragging an end of an edge to another corner of the diamond it is attached to. */
export interface CornerDrag {
  edgeId: string
  end: EdgeEnd
  /** The corner nearest to the pointer, where the end goes when released. */
  corner: Corner
  moved: boolean
}

export const cornerKey = (end: EdgeEnd) => (end === 'source' ? 'sourceCorner' : 'targetCorner')

export const endShapeId = (edge: Edge, end: EdgeEnd) => (end === 'source' ? edge.sourceId : edge.targetId)

/** The edge as drawn while one of its ends is dragged to another corner. */
export const withCornerDrag = (edge: Edge, drag: CornerDrag | null): Edge =>
  drag?.edgeId === edge.id ? { ...edge, [cornerKey(drag.end)]: drag.corner } : edge

export const hasFixedCorner = (edge: Edge) => edge.sourceCorner !== 'auto' || edge.targetCorner !== 'auto'
