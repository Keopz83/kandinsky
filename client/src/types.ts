export type ShapeKind =
  | 'rectangle'
  | 'database'
  | 'decision'
  | 'group'
  | 'start'
  | 'end'
  | 'terminal'
  | 'rounded'
  | 'io'
  | 'subroutine'
  | 'document'
  | 'connector'
  | 'preparation'
  | 'manual'

export const SHAPE_KINDS: readonly ShapeKind[] = [
  'rectangle', 'database', 'decision', 'group', 'start', 'end', 'terminal',
  'rounded', 'io', 'subroutine', 'document', 'connector', 'preparation', 'manual',
]

export type EdgeRouting = 'straight' | 'angled'

export interface Shape {
  id: string
  kind: ShapeKind
  label: string
  x: number
  y: number
  width: number
  height: number
  /** The group the shape lies in, derived by the server from the positions. */
  parentId?: string | null
}

export interface Edge {
  id: string
  sourceId: string
  targetId: string
  label: string | null
  routing: EdgeRouting
}

export interface Diagram {
  id: string
  name: string
  shapes: Shape[]
  edges: Edge[]
}

export interface DiagramSummary {
  id: string
  name: string
}

export interface PasteResult {
  diagram: Diagram
  shapeIds: string[]
  edgeIds: string[]
}

export type Format = 'plantuml' | 'mermaid'

export type AlignDirection = 'horizontal' | 'vertical'

/** The selection, a shape to place, or an edge to draw: straight ('edge') or angled. */
export type Tool = 'select' | ShapeKind | 'edge' | 'angled-edge'
