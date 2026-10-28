import type { PointerEvent } from 'react'
import type { Corner, Edge, EdgeCorner, Shape, ShapeKind } from './types'

export interface Point {
  x: number
  y: number
}

/** Sizes of newly placed shapes, so they are centered on the click; the same defaults as on the server. */
export const DEFAULT_SIZE: Record<ShapeKind, { width: number; height: number }> = {
  rectangle: { width: 140, height: 60 },
  database: { width: 140, height: 60 },
  decision: { width: 140, height: 80 },
  group: { width: 320, height: 200 },
  start: { width: 40, height: 40 },
  end: { width: 40, height: 40 },
  terminal: { width: 140, height: 60 },
  rounded: { width: 140, height: 60 },
  io: { width: 140, height: 60 },
  subroutine: { width: 140, height: 60 },
  document: { width: 140, height: 70 },
  connector: { width: 60, height: 60 },
  preparation: { width: 140, height: 60 },
  manual: { width: 140, height: 60 },
}

const GROUP_TITLE_HEIGHT = 28
const SUBROUTINE_INSET = 10

const isEllipse = (s: Shape) => s.kind === 'start' || s.kind === 'end' || s.kind === 'connector'

/** Start and end points are small; their label is shown below them. */
export const labelBelow = (s: Shape) => s.kind === 'start' || s.kind === 'end'

/** Height of the elliptical top (and bottom) of a database cylinder. */
const capHeight = (s: Shape) => (s.kind === 'database' ? Math.min(12, s.height / 4, s.width / 4) : 0)

/** Horizontal inset of the slanted sides of parallelograms, trapezoids and hexagons. */
const slant = (s: Shape) => Math.min(20, s.width / 4)

/** Depth of the wave at the bottom of a document. */
const wave = (s: Shape) => Math.min(10, s.height / 6)

/** The outline as a convex polygon (for edge anchors); ellipses are handled separately. */
function polygon(s: Shape): Point[] {
  const { x, y, width: w, height: h } = s
  const i = slant(s)
  switch (s.kind) {
    case 'decision':
      return [{ x: x + w / 2, y }, { x: x + w, y: y + h / 2 }, { x: x + w / 2, y: y + h }, { x, y: y + h / 2 }]
    case 'io':
      return [{ x: x + i, y }, { x: x + w, y }, { x: x + w - i, y: y + h }, { x, y: y + h }]
    case 'manual':
      return [{ x, y }, { x: x + w, y }, { x: x + w - i, y: y + h }, { x: x + i, y: y + h }]
    case 'preparation':
      return [
        { x: x + i, y }, { x: x + w - i, y }, { x: x + w, y: y + h / 2 },
        { x: x + w - i, y: y + h }, { x: x + i, y: y + h }, { x, y: y + h / 2 },
      ]
    case 'document':
      return rectangle(x, y, w, h - wave(s))
    default:
      return rectangle(x, y, w, h)
  }
}

const rectangle = (x: number, y: number, w: number, h: number): Point[] => [
  { x, y }, { x: x + w, y }, { x: x + w, y: y + h }, { x, y: y + h },
]

const points = (ps: Point[]) => ps.map((p) => `${p.x},${p.y}`).join(' ')

interface OutlineProps {
  shape: Shape
  onPointerDown?: (e: PointerEvent<SVGElement>) => void
  onDoubleClick?: () => void
}

/** The shape's outline; details such as the front edge of a cylinder's top are drawn separately and ignore the pointer. */
export function ShapeOutline({ shape, ...events }: OutlineProps) {
  const { x, y, width: w, height: h } = shape
  switch (shape.kind) {
    case 'decision':
    case 'io':
    case 'manual':
    case 'preparation':
      return <polygon className="outline" points={points(polygon(shape))} {...events} />
    case 'start':
    case 'connector':
      return <ellipse className="outline" cx={x + w / 2} cy={y + h / 2} rx={w / 2} ry={h / 2} {...events} />
    case 'end':
      return (
        <>
          <ellipse className="outline" cx={x + w / 2} cy={y + h / 2} rx={w / 2} ry={h / 2} {...events} />
          <ellipse className="outline-dot" cx={x + w / 2} cy={y + h / 2} rx={w * 0.3} ry={h * 0.3} />
        </>
      )
    case 'terminal':
      return <rect className="outline" x={x} y={y} width={w} height={h} rx={Math.min(h, w) / 2} {...events} />
    case 'rounded':
      return <rect className="outline" x={x} y={y} width={w} height={h} rx={Math.min(16, h / 3, w / 3)} {...events} />
    case 'subroutine':
      return (
        <>
          <rect className="outline" x={x} y={y} width={w} height={h} {...events} />
          <path className="outline-detail" d={`M${x + SUBROUTINE_INSET},${y} V${y + h} M${x + w - SUBROUTINE_INSET},${y} V${y + h}`} />
        </>
      )
    case 'document': {
      const a = wave(shape)
      return (
        <path
          className="outline"
          d={`M${x},${y} H${x + w} V${y + h - a} C${x + w * 0.75},${y + h - 3 * a} ${x + w * 0.25},${y + h + a} ${x},${y + h - a} Z`}
          {...events}
        />
      )
    }
    case 'group': {
      const title = Math.min(GROUP_TITLE_HEIGHT, h)
      return (
        <>
          <rect className="outline" x={x} y={y} width={w} height={h} rx={6} {...events} />
          <line className="outline-detail" x1={x} y1={y + title} x2={x + w} y2={y + title} />
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
      return <rect className="outline" x={x} y={y} width={w} height={h} rx={4} {...events} />
  }
}

/** The label area: inside the shape, clear of slanted or decorated parts; below start and end points; the title bar of a group. */
export function labelBox(s: Shape) {
  const { x, y, width: w, height: h } = s
  switch (s.kind) {
    case 'decision':
      return { x: x + w * 0.15, y: y + h / 4, width: w * 0.7, height: h / 2 }
    case 'group':
      return { x, y, width: w, height: Math.min(GROUP_TITLE_HEIGHT, h) }
    case 'start':
    case 'end':
      return { x: x - 50, y: y + h + 2, width: w + 100, height: 24 }
    case 'connector':
      return { x: x + w * 0.15, y: y + h * 0.15, width: w * 0.7, height: h * 0.7 }
    case 'io':
    case 'manual':
    case 'preparation':
      return { x: x + slant(s) / 2, y, width: Math.max(0, w - slant(s)), height: h }
    case 'subroutine':
      return { x: x + SUBROUTINE_INSET, y, width: Math.max(0, w - 2 * SUBROUTINE_INSET), height: h }
    case 'document':
      return { x, y, width: w, height: Math.max(0, h - wave(s)) }
    default: {
      const top = capHeight(s)
      return { x, y: y + top, width: w, height: Math.max(0, h - top) }
    }
  }
}

export const center = (s: Shape): Point => ({ x: s.x + s.width / 2, y: s.y + s.height / 2 })

/** Where the ray from the shape's center toward the point leaves the outline. */
function borderPoint(s: Shape, toward: Point): Point {
  const c = center(s)
  const dx = toward.x - c.x
  const dy = toward.y - c.y
  if (dx === 0 && dy === 0) {
    return c
  }
  if (isEllipse(s)) {
    const t = 1 / Math.hypot(dx / (s.width / 2), dy / (s.height / 2))
    return { x: c.x + dx * t, y: c.y + dy * t }
  }
  // The center is inside the convex polygon, so the ray crosses exactly one of its sides.
  const ps = polygon(s)
  let best = Infinity
  for (let i = 0; i < ps.length; i++) {
    const a = ps[i]
    const b = ps[(i + 1) % ps.length]
    const ex = b.x - a.x
    const ey = b.y - a.y
    const denominator = dx * ey - dy * ex
    if (denominator === 0) {
      continue
    }
    const t = ((a.x - c.x) * ey - (a.y - c.y) * ex) / denominator
    const u = ((a.x - c.x) * dy - (a.y - c.y) * dx) / denominator
    if (t > 0 && u >= -1e-9 && u <= 1 + 1e-9) {
      best = Math.min(best, t)
    }
  }
  return Number.isFinite(best) ? { x: c.x + dx * best, y: c.y + dy * best } : c
}

type Side = Corner

const SIDES: readonly Side[] = ['top', 'right', 'bottom', 'left']

const SIDE_DIRECTION: Record<Side, Point> = { top: { x: 0, y: -1 }, right: { x: 1, y: 0 }, bottom: { x: 0, y: 1 }, left: { x: -1, y: 0 } }

/** The middle of a side of the outline; on a diamond that is a corner. */
function sidePoint(s: Shape, side: Side): Point {
  const c = center(s)
  const d = SIDE_DIRECTION[side]
  return borderPoint(s, { x: c.x + d.x, y: c.y + d.y })
}

/** The four corners of a diamond (the middles of the sides of other shapes). */
export const corners = (s: Shape) => SIDES.map((side) => ({ corner: side, point: sidePoint(s, side) }))

/** The corner of a diamond nearest to the point. */
export const nearestCorner = (s: Shape, p: Point): Corner =>
  corners(s).reduce((best, c) => (Math.hypot(c.point.x - p.x, c.point.y - p.y) < Math.hypot(best.point.x - p.x, best.point.y - p.y) ? c : best)).corner

/** Where an edge toward the point attaches: on the outline, or at the facing corner of a diamond. */
function anchor(s: Shape, toward: Point): Point {
  if (s.kind !== 'decision') {
    return borderPoint(s, toward)
  }
  const c = center(s)
  const nx = (toward.x - c.x) / (s.width / 2)
  const ny = (toward.y - c.y) / (s.height / 2)
  return sidePoint(s, Math.abs(nx) >= Math.abs(ny) ? (nx >= 0 ? 'right' : 'left') : ny >= 0 ? 'bottom' : 'top')
}

/** The corner an edge end is fixed to: only diamonds have fixed corners. */
const fixedCorner = (s: Shape, corner: EdgeCorner | undefined): Side | null =>
  s.kind === 'decision' && corner && corner !== 'auto' ? corner : null

/** How far an angled edge runs straight out of a side before it turns. */
const STUB = 20

/** Drops repeated points and points in the middle of a straight run. */
function simplify(path: Point[]): Point[] {
  const distinct = path.filter((p, i) => i === 0 || Math.abs(p.x - path[i - 1].x) >= 0.5 || Math.abs(p.y - path[i - 1].y) >= 0.5)
  return distinct.filter((p, i) => {
    if (i === 0 || i === distinct.length - 1) {
      return true
    }
    const a = distinct[i - 1]
    const b = distinct[i + 1]
    return !((Math.abs(a.x - p.x) < 0.5 && Math.abs(b.x - p.x) < 0.5) || (Math.abs(a.y - p.y) < 0.5 && Math.abs(b.y - p.y) < 0.5))
  })
}

/**
 * Horizontal and vertical segments from point s, leaving in direction ds, to point t, entered against direction dt
 * (dt points out of the target's side). Turns as rarely as practical and runs a stub out of a side before turning back.
 */
function orthogonal(s: Point, ds: Point, t: Point, dt: Point): Point[] {
  // Does b lie in front of a, looking in direction d?
  const ahead = (a: Point, d: Point, b: Point) => (b.x - a.x) * d.x + (b.y - a.y) * d.y > 0
  const s1 = { x: s.x + ds.x * STUB, y: s.y + ds.y * STUB }
  const t1 = { x: t.x + dt.x * STUB, y: t.y + dt.y * STUB }
  if (ds.x !== 0 && dt.x !== 0) {
    if (ds.x === -dt.x && ahead(s, ds, t)) {
      const m = (s.x + t.x) / 2
      return simplify([s, { x: m, y: s.y }, { x: m, y: t.y }, t])
    }
    if (ds.x === dt.x) {
      const x = ds.x > 0 ? Math.max(s1.x, t1.x) : Math.min(s1.x, t1.x)
      return simplify([s, { x, y: s.y }, { x, y: t.y }, t])
    }
    const m = (s.y + t.y) / 2
    return simplify([s, s1, { x: s1.x, y: m }, { x: t1.x, y: m }, t1, t])
  }
  if (ds.y !== 0 && dt.y !== 0) {
    if (ds.y === -dt.y && ahead(s, ds, t)) {
      const m = (s.y + t.y) / 2
      return simplify([s, { x: s.x, y: m }, { x: t.x, y: m }, t])
    }
    if (ds.y === dt.y) {
      const y = ds.y > 0 ? Math.max(s1.y, t1.y) : Math.min(s1.y, t1.y)
      return simplify([s, { x: s.x, y }, { x: t.x, y }, t])
    }
    const m = (s.x + t.x) / 2
    return simplify([s, s1, { x: m, y: s1.y }, { x: m, y: t1.y }, t1, t])
  }
  const corner = ds.x !== 0 ? { x: t.x, y: s.y } : { x: s.x, y: t.y }
  if (ahead(s, ds, corner) && ahead(t, dt, corner)) {
    return [s, corner, t]
  }
  return simplify(ds.x !== 0 ? [s, s1, { x: s1.x, y: t1.y }, t1, t] : [s, s1, { x: t1.x, y: s1.y }, t1, t])
}

const pathLength = (path: Point[]) => path.slice(1).reduce((sum, p, i) => sum + Math.hypot(p.x - path[i].x, p.y - path[i].y), 0)

/** An angled path between the given sides; a free side (null) is the one giving the fewest turns, then the shortest path. */
function angledBetween(from: Shape, fromSide: Side | null, to: Shape, toSide: Side | null): Point[] {
  const paths = (fromSide ? [fromSide] : SIDES).flatMap((a) =>
    (toSide ? [toSide] : SIDES).map((b) => orthogonal(sidePoint(from, a), SIDE_DIRECTION[a], sidePoint(to, b), SIDE_DIRECTION[b])),
  )
  return paths.reduce((best, p) => (p.length < best.length || (p.length === best.length && pathLength(p) < pathLength(best)) ? p : best))
}

type EdgeStyle = Pick<Edge, 'routing'> & Partial<Pick<Edge, 'sourceCorner' | 'targetCorner'>>

/**
 * The points of an edge from one shape to another. A straight edge is one segment between the outlines.
 * An angled edge leaves and enters through the middle of a side (a diamond's corner) with horizontal and vertical segments:
 * a Z shape when the shapes are above each other or side by side, an L shape when they are diagonal to each other.
 * Overlapping shapes are joined straight. An end fixed to a diamond corner always attaches there.
 */
export function edgePath(from: Shape, to: Shape, { routing, sourceCorner, targetCorner }: EdgeStyle): Point[] {
  const fromCorner = fixedCorner(from, sourceCorner)
  const toCorner = fixedCorner(to, targetCorner)
  if (routing === 'angled') {
    const dx = to.x >= from.x + from.width ? 1 : to.x + to.width <= from.x ? -1 : 0
    const dy = to.y >= from.y + from.height ? 1 : to.y + to.height <= from.y ? -1 : 0
    if ((fromCorner || toCorner) && (dx !== 0 || dy !== 0)) {
      return angledBetween(from, fromCorner, to, toCorner)
    }
    if (dy !== 0 && dx === 0) {
      const s = sidePoint(from, dy > 0 ? 'bottom' : 'top')
      const t = sidePoint(to, dy > 0 ? 'top' : 'bottom')
      const m = (s.y + t.y) / 2
      return Math.abs(s.x - t.x) < 0.5 ? [s, { x: s.x, y: t.y }] : [s, { x: s.x, y: m }, { x: t.x, y: m }, t]
    }
    if (dx !== 0 && dy === 0) {
      const s = sidePoint(from, dx > 0 ? 'right' : 'left')
      const t = sidePoint(to, dx > 0 ? 'left' : 'right')
      const m = (s.x + t.x) / 2
      return Math.abs(s.y - t.y) < 0.5 ? [s, { x: t.x, y: s.y }] : [s, { x: m, y: s.y }, { x: m, y: t.y }, t]
    }
    if (dx !== 0 && dy !== 0) {
      const s = sidePoint(from, dx > 0 ? 'right' : 'left')
      const t = sidePoint(to, dy > 0 ? 'top' : 'bottom')
      return [s, { x: t.x, y: s.y }, t]
    }
  }
  const start = fromCorner ? sidePoint(from, fromCorner) : anchor(from, toCorner ? sidePoint(to, toCorner) : center(to))
  return [start, toCorner ? sidePoint(to, toCorner) : anchor(to, start)]
}

/** The point halfway along the path, where the edge label goes. */
export function pathMidpoint(path: Point[]): Point {
  const lengths = path.slice(1).map((p, i) => Math.hypot(p.x - path[i].x, p.y - path[i].y))
  let remaining = lengths.reduce((sum, l) => sum + l, 0) / 2
  for (let i = 0; i < lengths.length; i++) {
    if (remaining <= lengths[i] && lengths[i] > 0) {
      const t = remaining / lengths[i]
      return { x: path[i].x + (path[i + 1].x - path[i].x) * t, y: path[i].y + (path[i + 1].y - path[i].y) * t }
    }
    remaining -= lengths[i]
  }
  return path[0]
}

export const toPoints = points

export const boxOf = (a: Point, b: Point) => ({
  x: Math.min(a.x, b.x),
  y: Math.min(a.y, b.y),
  width: Math.abs(a.x - b.x),
  height: Math.abs(a.y - b.y),
})

export type Box = ReturnType<typeof boxOf>

/** Does the selection box select the shape? It must touch it, or enclose it completely for a group. */
export const shapeTouches = (s: Shape, box: Box) =>
  s.kind === 'group'
    ? s.x >= box.x && s.y >= box.y && s.x + s.width <= box.x + box.width && s.y + s.height <= box.y + box.height
    : s.x <= box.x + box.width && s.x + s.width >= box.x && s.y <= box.y + box.height && s.y + s.height >= box.y

// Liang–Barsky clipping: does the segment a→b touch the box?
function segmentTouches(a: Point, b: Point, box: Box) {
  const dx = b.x - a.x
  const dy = b.y - a.y
  let t0 = 0
  let t1 = 1
  for (const [p, q] of [
    [-dx, a.x - box.x],
    [dx, box.x + box.width - a.x],
    [-dy, a.y - box.y],
    [dy, box.y + box.height - a.y],
  ]) {
    if (p === 0) {
      if (q < 0) {
        return false
      }
    } else if (p < 0) {
      t0 = Math.max(t0, q / p)
    } else {
      t1 = Math.min(t1, q / p)
    }
    if (t0 > t1) {
      return false
    }
  }
  return true
}

export const pathTouches = (path: Point[], box: Box) => path.slice(1).some((p, i) => segmentTouches(path[i], p, box))

/** Drawing order: groups first, outer before inner, so their content stays on top of them. */
export const drawOrder = (shapes: Shape[]) => [
  ...shapes.filter((s) => s.kind === 'group').sort((a, b) => b.width * b.height - a.width * a.height),
  ...shapes.filter((s) => s.kind !== 'group'),
]

/** The given shapes plus everything inside the groups among them, at any depth. */
export function withContent(shapes: Shape[], ids: Iterable<string>) {
  const result = new Set(ids)
  let grown = true
  while (grown) {
    grown = false
    for (const s of shapes) {
      if (s.parentId && result.has(s.parentId) && !result.has(s.id)) {
        result.add(s.id)
        grown = true
      }
    }
  }
  return result
}
