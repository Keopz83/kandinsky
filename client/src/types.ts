export interface Shape {
  id: string
  label: string
  x: number
  y: number
  width: number
  height: number
}

export interface Edge {
  id: string
  sourceId: string
  targetId: string
  label: string | null
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

export type Format = 'plantuml' | 'mermaid'

export type AlignDirection = 'horizontal' | 'vertical'

export type Tool = 'select' | 'rectangle' | 'edge'
