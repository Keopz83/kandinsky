import { renderToStaticMarkup } from 'react-dom/server'
import { edgePath } from '../geometry/edgeRouting'
import type { Point } from '../geometry/point'
import { pathMidpoint, toPoints } from '../geometry/polyline'
import { isGroup, labelBelow } from '../geometry/shapeGeometry'
import { drawOrder } from '../geometry/shapeHierarchy'
import { ShapeOutline } from '../ShapeOutline'
import type { Diagram, Edge, Shape } from '../types'
import { ShapeLabelText, layOutLabel, type LaidOutLabel } from './ShapeLabelText'
import { FONT_FAMILY, textWidth, type Font } from './textLayout'

const MARGIN = 20
const STROKE = '#3a3f4b'
const TEXT = '#212529'
const EDGE_FONT: Font = { size: 12, weight: 400 }
/** Distance of an edge label's baseline above the edge's midpoint (as on the whiteboard). */
const EDGE_LABEL_OFFSET = 6
/** Room around an edge label for its white halo. */
const EDGE_LABEL_HALO = 2
const DEFAULT_PNG_SCALE = 2

// The same look as the whiteboard (styles.css), without selection and grid; labels are SVG text, so any viewer shows them.
const STYLE = `
.outline, .outline-detail { fill: #fffdf5; stroke: ${STROKE}; stroke-width: 1.5; }
.outline-detail { fill: none; }
.start > .outline, .outline-dot { fill: ${STROKE}; }
.group > .outline { fill: #e2e8f4; fill-opacity: 0.45; }
.line { fill: none; stroke: ${STROKE}; stroke-width: 1.5; }
.arrow { fill: ${STROKE}; }
text { font-family: ${FONT_FAMILY}; fill: ${TEXT}; }
.edge-label { font-size: ${EDGE_FONT.size}px; paint-order: stroke; stroke: white; stroke-width: 4px; stroke-linejoin: round; }
`

export interface DiagramImage {
  svg: string
  width: number
  height: number
}

interface RoutedEdge {
  edge: Edge
  path: Point[]
  middle: Point
}

function routeEdges(diagram: Diagram): RoutedEdge[] {
  const byId = new Map(diagram.shapes.map((s) => [s.id, s]))
  return diagram.edges.flatMap((edge) => {
    const from = byId.get(edge.sourceId)
    const to = byId.get(edge.targetId)
    if (!from || !to) {
      return []
    }
    const path = edgePath(from, to, edge)
    return [{ edge, path, middle: pathMidpoint(path) }]
  })
}

/** Points that must lie inside the image: shapes, labels hanging below shapes, edges and their labels. */
function contentPoints(shapes: Shape[], labels: Map<string, LaidOutLabel | null>, edges: RoutedEdge[]): Point[] {
  const points: Point[] = []
  for (const s of shapes) {
    points.push({ x: s.x, y: s.y }, { x: s.x + s.width, y: s.y + s.height })
    const label = labels.get(s.id)
    if (label && labelBelow(s)) {
      points.push(
        { x: label.x - label.width / 2, y: label.y - label.lineHeight / 2 },
        { x: label.x + label.width / 2, y: label.y + (label.lines.length - 0.5) * label.lineHeight },
      )
    }
  }
  for (const { edge, path, middle } of edges) {
    points.push(...path)
    if (edge.label) {
      const halfWidth = textWidth(edge.label, EDGE_FONT) / 2 + EDGE_LABEL_HALO
      const top = middle.y - EDGE_LABEL_OFFSET - EDGE_FONT.size - EDGE_LABEL_HALO
      points.push({ x: middle.x - halfWidth, y: top }, { x: middle.x + halfWidth, y: middle.y })
    }
  }
  return points
}

/** The whole-pixel bounds of the points plus a margin. */
function boundsWithMargin(points: Point[]) {
  const x = Math.floor(Math.min(...points.map((p) => p.x))) - MARGIN
  const y = Math.floor(Math.min(...points.map((p) => p.y))) - MARGIN
  const width = Math.ceil(Math.max(...points.map((p) => p.x))) + MARGIN - x
  const height = Math.ceil(Math.max(...points.map((p) => p.y))) + MARGIN - y
  return { x, y, width, height }
}

/** The whiteboard as a standalone SVG document, cropped to its content with a margin and on a white background. */
export function diagramSvg(diagram: Diagram): DiagramImage {
  if (diagram.shapes.length === 0) {
    throw new Error('The whiteboard is empty; there is nothing to export.')
  }
  const labels = new Map(diagram.shapes.map((s) => [s.id, layOutLabel(s)]))
  const edges = routeEdges(diagram)
  const bounds = boundsWithMargin(contentPoints(diagram.shapes, labels, edges))
  const ordered = drawOrder(diagram.shapes)

  const renderShape = (shape: Shape) => {
    const label = labels.get(shape.id)
    return (
      <g key={shape.id} className={`shape ${shape.kind}`}>
        <ShapeOutline shape={shape} />
        {label && <ShapeLabelText label={label} />}
      </g>
    )
  }
  const markup = renderToStaticMarkup(
    <svg xmlns="http://www.w3.org/2000/svg" width={bounds.width} height={bounds.height} viewBox={`${bounds.x} ${bounds.y} ${bounds.width} ${bounds.height}`}>
      <title>{diagram.name}</title>
      <style>{STYLE}</style>
      <defs>
        <marker id="arrow" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" className="arrow" />
        </marker>
      </defs>
      <rect {...bounds} fill="white" />
      {ordered.filter(isGroup).map(renderShape)}
      {edges.map(({ edge, path, middle }) => (
        <g key={edge.id} className="edge">
          <polyline className="line" points={toPoints(path)} markerEnd="url(#arrow)" />
          {edge.label && (
            <text className="edge-label" x={middle.x} y={middle.y - EDGE_LABEL_OFFSET} textAnchor="middle">
              {edge.label}
            </text>
          )}
        </g>
      ))}
      {ordered.filter((s) => !isGroup(s)).map(renderShape)}
    </svg>,
  )
  return { svg: `<?xml version="1.0" encoding="UTF-8"?>\n${markup}\n`, width: bounds.width, height: bounds.height }
}

/** The SVG rendered to a PNG, scaled up (2× by default) so it stays sharp on high-resolution screens. */
export async function diagramPng(image: DiagramImage, scale = DEFAULT_PNG_SCALE): Promise<Blob> {
  const url = URL.createObjectURL(new Blob([image.svg], { type: 'image/svg+xml' }))
  try {
    const img = new Image()
    img.src = url
    await img.decode()
    const canvas = document.createElement('canvas')
    canvas.width = Math.round(image.width * scale)
    canvas.height = Math.round(image.height * scale)
    const context = canvas.getContext('2d')
    if (!context) {
      throw new Error('The browser cannot draw the PNG image.')
    }
    context.drawImage(img, 0, 0, canvas.width, canvas.height)
    return await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('The browser cannot create the PNG image.'))), 'image/png'),
    )
  } finally {
    URL.revokeObjectURL(url)
  }
}
