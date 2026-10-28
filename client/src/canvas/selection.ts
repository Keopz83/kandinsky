import { edgePath } from '../geometry/edgeRouting'
import { pathTouches, shapeTouches } from '../geometry/hitTesting'
import type { Box } from '../geometry/point'
import type { Edge, Shape } from '../types'

/** Ids of the shapes and edges a selection box selects. */
export function objectsInBox(shapes: Shape[], edges: Edge[], box: Box): string[] {
  const byId = new Map(shapes.map((s) => [s.id, s]))
  const edgeTouches = (edge: Edge) => {
    const from = byId.get(edge.sourceId)
    const to = byId.get(edge.targetId)
    return !!from && !!to && pathTouches(edgePath(from, to, edge), box)
  }
  return [...shapes.filter((s) => shapeTouches(s, box)).map((s) => s.id), ...edges.filter(edgeTouches).map((edge) => edge.id)]
}

export const toggled = (ids: string[], id: string) => (ids.includes(id) ? ids.filter((other) => other !== id) : [...ids, id])

export const withAdded = (ids: string[], more: string[]) => [...ids, ...more.filter((id) => !ids.includes(id))]
