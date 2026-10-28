import { labelBelow, labelBox } from '../geometry/shapeGeometry'
import type { Shape } from '../types'
import { textWidth, truncate, wrap, type Font } from './textLayout'

const SHAPE_FONT: Font = { size: 14, weight: 500 }
const BELOW_FONT: Font = { size: 12, weight: 500 }
const GROUP_FONT: Font = { size: 14, weight: 600 }
const SHAPE_LINE_SPACING = 1.5
const BELOW_LINE_SPACING = 1.2
const GROUP_LINE_HEIGHT = 21
const GROUP_TITLE_PADDING = 10
const SHAPE_LABEL_PADDING = 8
const BELOW_LABEL_PADDING = 4

/** A shape label broken into lines and positioned, as SVG text needs it. */
export interface LaidOutLabel {
  lines: string[]
  font: Font
  lineHeight: number
  /** Where the first line's middle is and how the lines are anchored. */
  x: number
  y: number
  anchor: 'start' | 'middle'
  width: number
}

const widestLine = (lines: string[], font: Font) => Math.max(...lines.map((line) => textWidth(line, font)))

const maxLinesIn = (height: number, lineHeight: number) => Math.max(1, Math.floor(height / lineHeight))

/** The shape label laid out as on the whiteboard: centered in its box, below start and end points, left in a group's title bar. */
export function layOutLabel(shape: Shape): LaidOutLabel | null {
  if (!shape.label.trim()) {
    return null
  }
  const box = labelBox(shape)
  if (shape.kind === 'group') {
    const line = truncate(shape.label, Math.max(0, box.width - 2 * GROUP_TITLE_PADDING), GROUP_FONT)
    const y = box.y + box.height / 2
    return { lines: [line], font: GROUP_FONT, lineHeight: GROUP_LINE_HEIGHT, x: box.x + GROUP_TITLE_PADDING, y, anchor: 'start', width: textWidth(line, GROUP_FONT) }
  }
  if (labelBelow(shape)) {
    const lineHeight = BELOW_FONT.size * BELOW_LINE_SPACING
    const lines = wrap(shape.label, Math.max(1, box.width - 2 * BELOW_LABEL_PADDING), BELOW_FONT, maxLinesIn(box.height, lineHeight))
    const x = box.x + box.width / 2
    return { lines, font: BELOW_FONT, lineHeight, x, y: box.y + lineHeight / 2, anchor: 'middle', width: widestLine(lines, BELOW_FONT) }
  }
  const lineHeight = SHAPE_FONT.size * SHAPE_LINE_SPACING
  const lines = wrap(shape.label, Math.max(1, box.width - 2 * SHAPE_LABEL_PADDING), SHAPE_FONT, maxLinesIn(box.height, lineHeight))
  const top = box.y + box.height / 2 - ((lines.length - 1) * lineHeight) / 2
  return { lines, font: SHAPE_FONT, lineHeight, x: box.x + box.width / 2, y: top, anchor: 'middle', width: widestLine(lines, SHAPE_FONT) }
}

export function ShapeLabelText({ label }: { label: LaidOutLabel }) {
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
