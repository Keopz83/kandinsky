import { api } from '../api'
import { withContent } from '../geometry/shapeHierarchy'
import type { Diagram, Edge, Shape } from '../types'

/** Deletes the shapes and edges with the given ids. */
export async function deleteObjects(diagramId: string, diagram: Diagram, ids: string[]) {
  // Edges first: deleting a shape also removes its edges on the server.
  for (const edge of diagram.edges.filter((edge) => ids.includes(edge.id))) {
    await api.deleteEdge(diagramId, edge.id)
  }
  // A group is deleted with its content, so shapes inside a deleted group are already gone.
  const deletedGroups = diagram.shapes.filter((shape) => shape.kind === 'group' && ids.includes(shape.id)).map((shape) => shape.id)
  const deletedWithGroups = withContent(diagram.shapes, deletedGroups)
  const isInDeletedGroup = (shape: Shape) => !!shape.parentId && deletedWithGroups.has(shape.parentId)
  for (const shape of diagram.shapes.filter((shape) => ids.includes(shape.id) && !isInDeletedGroup(shape))) {
    await api.deleteShape(diagramId, shape.id)
  }
}

/** Adds copies of the shapes and of the edges between them; returns the copies' ids by original id and the new edge ids. */
export async function copyShapes(diagramId: string, shapes: Shape[], edges: Edge[]) {
  const copyIds = new Map<string, string>()
  for (const { id, kind, label, x, y, width, height } of shapes) {
    const copy = await api.addShape(diagramId, { kind, label, x, y, width, height })
    copyIds.set(id, copy.id)
  }
  const edgeIds: string[] = []
  for (const edge of edges) {
    const corners = { sourceCorner: edge.sourceCorner, targetCorner: edge.targetCorner }
    const copy = await api.addEdge(diagramId, copyIds.get(edge.sourceId)!, copyIds.get(edge.targetId)!, edge.routing, edge.label, corners)
    edgeIds.push(copy.id)
  }
  return { copyIds, edgeIds }
}

/** Saves the shapes' new positions and sizes. */
export async function updateGeometry(diagramId: string, shapes: Shape[]) {
  for (const { id, x, y, width, height } of shapes) {
    await api.updateShape(diagramId, id, { x, y, width, height })
  }
}
