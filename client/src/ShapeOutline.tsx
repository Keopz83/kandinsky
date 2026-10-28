import type { PointerEvent } from 'react'
import { toPoints } from './geometry/polyline'
import { GROUP_TITLE_HEIGHT, SUBROUTINE_INSET, capHeight, outlinePolygon, wave } from './geometry/shapeGeometry'
import type { Shape } from './types'

const GROUP_CORNER_RADIUS = 6
const RECTANGLE_CORNER_RADIUS = 4
const MAX_ROUNDED_CORNER_RADIUS = 16
/** Radius of the filled dot of an end point, relative to its size. */
const END_DOT_SCALE = 0.3

interface Props {
  shape: Shape
  onPointerDown?: (e: PointerEvent<SVGElement>) => void
  onDoubleClick?: () => void
}

/** The shape's outline; details such as the front edge of a cylinder's top are drawn separately and ignore the pointer. */
export function ShapeOutline({ shape, ...events }: Props) {
  const { x, y, width: w, height: h } = shape
  const ellipse = { cx: x + w / 2, cy: y + h / 2, rx: w / 2, ry: h / 2 }
  switch (shape.kind) {
    case 'decision':
    case 'io':
    case 'manual':
    case 'preparation':
      return <polygon className="outline" points={toPoints(outlinePolygon(shape))} {...events} />
    case 'start':
    case 'connector':
      return <ellipse className="outline" {...ellipse} {...events} />
    case 'end':
      return (
        <>
          <ellipse className="outline" {...ellipse} {...events} />
          <ellipse className="outline-dot" cx={ellipse.cx} cy={ellipse.cy} rx={w * END_DOT_SCALE} ry={h * END_DOT_SCALE} />
        </>
      )
    case 'terminal':
      return <rect className="outline" x={x} y={y} width={w} height={h} rx={Math.min(h, w) / 2} {...events} />
    case 'rounded':
      return <rect className="outline" x={x} y={y} width={w} height={h} rx={Math.min(MAX_ROUNDED_CORNER_RADIUS, h / 3, w / 3)} {...events} />
    case 'subroutine':
      return (
        <>
          <rect className="outline" x={x} y={y} width={w} height={h} {...events} />
          <path className="outline-detail" d={`M${x + SUBROUTINE_INSET},${y} V${y + h} M${x + w - SUBROUTINE_INSET},${y} V${y + h}`} />
        </>
      )
    case 'document':
      return <path className="outline" d={documentPath(shape)} {...events} />
    case 'group': {
      const titleBottom = y + Math.min(GROUP_TITLE_HEIGHT, h)
      return (
        <>
          <rect className="outline" x={x} y={y} width={w} height={h} rx={GROUP_CORNER_RADIUS} {...events} />
          <line className="outline-detail" x1={x} y1={titleBottom} x2={x + w} y2={titleBottom} />
        </>
      )
    }
    case 'database': {
      const rx = w / 2
      const ry = capHeight(shape) / 2
      return (
        <>
          <path
            className="outline"
            d={`M${x},${y + ry} A${rx},${ry} 0 0 1 ${x + w},${y + ry} V${y + h - ry} A${rx},${ry} 0 0 1 ${x},${y + h - ry} Z`}
            {...events}
          />
          <path className="outline-detail" d={`M${x},${y + ry} A${rx},${ry} 0 0 0 ${x + w},${y + ry}`} />
        </>
      )
    }
    default:
      return <rect className="outline" x={x} y={y} width={w} height={h} rx={RECTANGLE_CORNER_RADIUS} {...events} />
  }
}

/** A rectangle whose bottom side is a wave. */
function documentPath(shape: Shape) {
  const { x, y, width: w, height: h } = shape
  const depth = wave(shape)
  return `M${x},${y} H${x + w} V${y + h - depth} C${x + w * 0.75},${y + h - 3 * depth} ${x + w * 0.25},${y + h + depth} ${x},${y + h - depth} Z`
}
