import { renderToStaticMarkup } from 'react-dom/server'
import { ShapeOutline, drawOrder, edgePath, labelBelow, labelBox, pathMidpoint } from './geometry'
import type { Point } from './geometry'
import type { Diagram, Shape } from './types'

const MARGIN = 20
const FONT_FAMILY = 'system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif'
const STROKE = '#3a3f4b'
const TEXT = '#212529'

// The same look as the whiteboard (styles.css), without selection and grid; labels are SVG text, so any viewer shows them.
const STYLE = `
.outline, .outline-detail { fill: #fffdf5; stroke: ${STROKE}; stroke-width: 1.5; }
.outline-detail { fill: none; }
.start > .outline, .outline-dot { fill: ${STROKE}; }
.group > .outline { fill: #e2e8f4; fill-opacity: 0.45; }
.line { fill: none; stroke: ${STROKE}; stroke-width: 1.5; }
.arrow { fill: ${STROKE}; }
text { font-family: ${FONT_FAMILY}; fill: ${TEXT}; }
.edge-label { font-size: 12px; paint-order: stroke; stroke: white; stroke-width: 4px; stroke-linejoin: round; }
`

interface Font {
  size: number
  weight: number
}

const SHAPE_FONT: Font = { size: 14, weight: 500 }
const BELOW_FONT: Font = { size: 12, weight: 500 }
const GROUP_FONT: Font = { size: 14, weight: 600 }
const EDGE_FONT: Font = { size: 12, weight: 400 }

let context: CanvasRenderingContext2D | null = null

function textWidth(text: string, font: Font) {
  context ??= document.createElement('canvas').getContext('2d')
  if (!context) {
    return text.length * font.size * 0.55
  }
  context.font = `${font.weight} ${font.size}px ${FONT_FAMILY}`
  return context.measureText(text).width
}

/** Shortens the text with an ellipsis until it fits the width. */
function truncate(text: string, width: number, font: Font) {
  if (textWidth(text, font) <= width) {
    return text
  }
  let end = text.length
  while (end > 0 && textWidth(`${text.slice(0, end).trimEnd()}…`, font) > width) {
    end--
  }
  return `${text.slice(0, end).trimEnd()}…`
}

/** Breaks the text into lines fitting the width (long words are broken too), at most maxLines, the last one shortened if needed. */
function wrap(text: string, width: number, font: Font, maxLines: number): string[] {
  const lines: string[] = []
  for (const word of text.split(/\s+/).filter(Boolean)) {
    const last = lines.length > 0 ? `${lines[lines.length - 1]} ${word}` : null
    if (last !== null && textWidth(last, font) <= width) {
      lines[lines.length - 1] = last
      continue
    }
    let rest = word
    while (textWidth(rest, font) > width && rest.length > 1) {
      let cut = rest.length - 1
      while (cut > 1 && textWidth(rest.slice(0, cut), font) > width) {
        cut--
      }
      lines.push(rest.slice(0, cut))
      rest = rest.slice(cut)
    }
    lines.push(rest)
  }
  if (lines.length > maxLines) {
    const kept = lines.slice(0, Math.max(1, maxLines))
    kept[kept.length - 1] = truncate(`${kept[kept.length - 1]} ${lines[kept.length]}`, width, font)
    return kept
  }
  return lines
}

interface LaidOutLabel {
  lines: string[]
  font: Font
  lineHeight: number
  /** Where the first line's middle is and how the lines are anchored. */
  x: number
  y: number
  anchor: 'start' | 'middle'
  width: number
}

/** The shape label laid out as on the whiteboard: centered in its box, below start and end points, left in a group's title bar. */
function layOutLabel(shape: Shape): LaidOutLabel | null {
  if (!shape.label.trim()) {
    return null
  }
  const box = labelBox(shape)
  if (shape.kind === 'group') {
    const line = truncate(shape.label, Math.max(0, box.width - 20), GROUP_FONT)
    return { lines: [line], font: GROUP_FONT, lineHeight: 21, x: box.x + 10, y: box.y + box.height / 2, anchor: 'start', width: textWidth(line, GROUP_FONT) }
  }
  if (labelBelow(shape)) {
    const lineHeight = BELOW_FONT.size * 1.2
    const lines = wrap(shape.label, Math.max(1, box.width - 8), BELOW_FONT, Math.max(1, Math.floor(box.height / lineHeight)))
    const width = Math.max(...lines.map((l) => textWidth(l, BELOW_FONT)))
    return { lines, font: BELOW_FONT, lineHeight, x: box.x + box.width / 2, y: box.y + lineHeight / 2, anchor: 'middle', width }
  }
  const lineHeight = SHAPE_FONT.size * 1.5
  const lines = wrap(shape.label, Math.max(1, box.width - 16), SHAPE_FONT, Math.max(1, Math.floor(box.height / lineHeight)))
  const width = Math.max(...lines.map((l) => textWidth(l, SHAPE_FONT)))
  const top = box.y + box.height / 2 - ((lines.length - 1) * lineHeight) / 2
  return { lines, font: SHAPE_FONT, lineHeight, x: box.x + box.width / 2, y: top, anchor: 'middle', width }
}

function Label({ label }: { label: LaidOutLabel }) {
  return (
    <text x={label.x} textAnchor={label.anchor} fontSize={label.font.size} fontWeight={label.font.weight}>
      {label.lines.map((line, i) => (
        <tspan key={i} x={label.x} y={label.y + i * label.lineHeight} dominantBaseline="central">
          {line}
        </tspan>
      ))}
    </text>
  )
}

export interface DiagramImage {
  svg: string
  width: number
  height: number
}

/** The whiteboard as a standalone SVG document, cropped to its content with a margin and on a white background. */
export function diagramSvg(diagram: Diagram): DiagramImage {
  if (diagram.shapes.length === 0) {
    throw new Error('The whiteboard is empty; there is nothing to export.')
  }
  const byId = new Map(diagram.shapes.map((s) => [s.id, s]))
  const labels = new Map(diagram.shapes.map((s) => [s.id, layOutLabel(s)]))
  const edges = diagram.edges.flatMap((edge) => {
    const from = byId.get(edge.sourceId)
    const to = byId.get(edge.targetId)
    if (!from || !to) {
      return []
    }
    const path = edgePath(from, to, edge)
    return [{ edge, path, middle: pathMidpoint(path) }]
  })

  const points: Point[] = []
  for (const s of diagram.shapes) {
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
      const half = textWidth(edge.label, EDGE_FONT) / 2 + 2
      points.push({ x: middle.x - half, y: middle.y - 6 - EDGE_FONT.size - 2 }, { x: middle.x + half, y: middle.y })
    }
  }
  const minX = Math.floor(Math.min(...points.map((p) => p.x))) - MARGIN
  const minY = Math.floor(Math.min(...points.map((p) => p.y))) - MARGIN
  const width = Math.ceil(Math.max(...points.map((p) => p.x))) + MARGIN - minX
  const height = Math.ceil(Math.max(...points.map((p) => p.y))) + MARGIN - minY

  const renderShape = (shape: Shape) => {
    const label = labels.get(shape.id)
    return (
      <g key={shape.id} className={`shape ${shape.kind}`}>
        <ShapeOutline shape={shape} />
        {label && <Label label={label} />}
      </g>
    )
  }
  const ordered = drawOrder(diagram.shapes)
  const markup = renderToStaticMarkup(
    <svg xmlns="http://www.w3.org/2000/svg" width={width} height={height} viewBox={`${minX} ${minY} ${width} ${height}`}>
      <title>{diagram.name}</title>
      <style>{STYLE}</style>
      <defs>
        <marker id="arrow" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" className="arrow" />
        </marker>
      </defs>
      <rect x={minX} y={minY} width={width} height={height} fill="white" />
      {ordered.filter((s) => s.kind === 'group').map(renderShape)}
      {edges.map(({ edge, path, middle }) => (
        <g key={edge.id} className="edge">
          <polyline className="line" points={path.map((p) => `${p.x},${p.y}`).join(' ')} markerEnd="url(#arrow)" />
          {edge.label && (
            <text className="edge-label" x={middle.x} y={middle.y - 6} textAnchor="middle">
              {edge.label}
            </text>
          )}
        </g>
      ))}
      {ordered.filter((s) => s.kind !== 'group').map(renderShape)}
    </svg>,
  )
  return { svg: `<?xml version="1.0" encoding="UTF-8"?>\n${markup}\n`, width, height }
}

/** The SVG rendered to a PNG, scaled up (2× by default) so it stays sharp on high-resolution screens. */
export async function diagramPng(image: DiagramImage, scale = 2): Promise<Blob> {
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
