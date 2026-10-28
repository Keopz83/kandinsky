import type { Corner, Shape, ShapeKind } from '../types'
import { distance, type Box, type Point } from './point'

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

export const GROUP_TITLE_HEIGHT = 28
/** Distance of the inner vertical lines of a subroutine from its sides. */
export const SUBROUTINE_INSET = 10
const MAX_SLANT = 20
const MAX_CAP_HEIGHT = 12
const MAX_WAVE = 10
/** The label of a start or end point is this much wider than the point on each side. */
const BELOW_LABEL_OVERHANG = 50
const BELOW_LABEL_GAP = 2
const BELOW_LABEL_HEIGHT = 24
/** Share of a diamond's or connector's width kept free of the label on each side. */
const LABEL_SIDE_MARGIN = 0.15
/** Tolerance for intersection parameters, so rays through a polygon vertex still hit a side. */
const EPSILON = 1e-9

export const isGroup = (s: Shape) => s.kind === 'group'

export const isEllipse = (s: Shape) => s.kind === 'start' || s.kind === 'end' || s.kind === 'connector'

/** Start and end points are small; their label is shown below them. */
export const labelBelow = (s: Shape) => s.kind === 'start' || s.kind === 'end'

/** Height of the elliptical top (and bottom) of a database cylinder. */
export const capHeight = (s: Shape) => (s.kind === 'database' ? Math.min(MAX_CAP_HEIGHT, s.height / 4, s.width / 4) : 0)

/** Horizontal inset of the slanted sides of parallelograms, trapezoids and hexagons. */
export const slant = (s: Shape) => Math.min(MAX_SLANT, s.width / 4)

/** Depth of the wave at the bottom of a document. */
export const wave = (s: Shape) => Math.min(MAX_WAVE, s.height / 6)

export const center = (s: Shape): Point => ({ x: s.x + s.width / 2, y: s.y + s.height / 2 })

export const containsPoint = (s: Shape, p: Point) => p.x >= s.x && p.x <= s.x + s.width && p.y >= s.y && p.y <= s.y + s.height

const rectangle = (x: number, y: number, w: number, h: number): Point[] => [
  { x, y }, { x: x + w, y }, { x: x + w, y: y + h }, { x, y: y + h },
]

/** The outline as a convex polygon (for edge anchors); ellipses are handled separately. */
export function outlinePolygon(s: Shape): Point[] {
  const { x, y, width: w, height: h } = s
  const inset = slant(s)
  switch (s.kind) {
    case 'decision':
      return [{ x: x + w / 2, y }, { x: x + w, y: y + h / 2 }, { x: x + w / 2, y: y + h }, { x, y: y + h / 2 }]
    case 'io':
      return [{ x: x + inset, y }, { x: x + w, y }, { x: x + w - inset, y: y + h }, { x, y: y + h }]
    case 'manual':
      return [{ x, y }, { x: x + w, y }, { x: x + w - inset, y: y + h }, { x: x + inset, y: y + h }]
    case 'preparation':
      return [
        { x: x + inset, y }, { x: x + w - inset, y }, { x: x + w, y: y + h / 2 },
        { x: x + w - inset, y: y + h }, { x: x + inset, y: y + h }, { x, y: y + h / 2 },
      ]
    case 'document':
      return rectangle(x, y, w, h - wave(s))
    default:
      return rectangle(x, y, w, h)
  }
}

/** The label area: inside the shape, clear of slanted or decorated parts; below start and end points; the title bar of a group. */
export function labelBox(s: Shape): Box {
  const { x, y, width: w, height: h } = s
  switch (s.kind) {
    case 'decision':
      return { x: x + w * LABEL_SIDE_MARGIN, y: y + h / 4, width: w * (1 - 2 * LABEL_SIDE_MARGIN), height: h / 2 }
    case 'group':
      return { x, y, width: w, height: Math.min(GROUP_TITLE_HEIGHT, h) }
    case 'start':
    case 'end':
      return { x: x - BELOW_LABEL_OVERHANG, y: y + h + BELOW_LABEL_GAP, width: w + 2 * BELOW_LABEL_OVERHANG, height: BELOW_LABEL_HEIGHT }
    case 'connector': {
      const keep = 1 - 2 * LABEL_SIDE_MARGIN
      return { x: x + w * LABEL_SIDE_MARGIN, y: y + h * LABEL_SIDE_MARGIN, width: w * keep, height: h * keep }
    }
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

/** How far along the ray from `origin` in direction `direction` it crosses the polygon side a→b, if it does. */
function rayHitsSide(origin: Point, direction: Point, a: Point, b: Point): number | null {
  const side = { x: b.x - a.x, y: b.y - a.y }
  const denominator = direction.x * side.y - direction.y * side.x
  if (denominator === 0) {
    return null
  }
  const alongRay = ((a.x - origin.x) * side.y - (a.y - origin.y) * side.x) / denominator
  const alongSide = ((a.x - origin.x) * direction.y - (a.y - origin.y) * direction.x) / denominator
  return alongRay > 0 && alongSide >= -EPSILON && alongSide <= 1 + EPSILON ? alongRay : null
}

/** Where the ray from the shape's center toward the point leaves the outline. */
function borderPoint(s: Shape, toward: Point): Point {
  const c = center(s)
  const direction = { x: toward.x - c.x, y: toward.y - c.y }
  if (direction.x === 0 && direction.y === 0) {
    return c
  }
  const at = (t: number) => ({ x: c.x + direction.x * t, y: c.y + direction.y * t })
  if (isEllipse(s)) {
    return at(1 / Math.hypot(direction.x / (s.width / 2), direction.y / (s.height / 2)))
  }
  // The center is inside the convex polygon, so the ray crosses exactly one of its sides.
  const polygon = outlinePolygon(s)
  const hits = polygon.map((a, i) => rayHitsSide(c, direction, a, polygon[(i + 1) % polygon.length])).filter((t) => t !== null)
  return hits.length > 0 ? at(Math.min(...hits)) : c
}

/** A side of a shape's bounding box; on a diamond, the corner pointing that way. */
export type Side = Corner

export const SIDES: readonly Side[] = ['top', 'right', 'bottom', 'left']

export const SIDE_DIRECTION: Record<Side, Point> = { top: { x: 0, y: -1 }, right: { x: 1, y: 0 }, bottom: { x: 0, y: 1 }, left: { x: -1, y: 0 } }

/** The middle of a side of the outline; on a diamond that is a corner. */
export function sidePoint(s: Shape, side: Side): Point {
  const c = center(s)
  const d = SIDE_DIRECTION[side]
  return borderPoint(s, { x: c.x + d.x, y: c.y + d.y })
}

/** The four corners of a diamond (the middles of the sides of other shapes). */
export const corners = (s: Shape) => SIDES.map((side) => ({ corner: side, point: sidePoint(s, side) }))

/** The corner of a diamond nearest to the point. */
export const nearestCorner = (s: Shape, p: Point): Corner =>
  corners(s).reduce((best, c) => (distance(c.point, p) < distance(best.point, p) ? c : best)).corner

/** The side of the shape facing the point, relative to the shape's proportions. */
function facingSide(s: Shape, toward: Point): Side {
  const c = center(s)
  const nx = (toward.x - c.x) / (s.width / 2)
  const ny = (toward.y - c.y) / (s.height / 2)
  if (Math.abs(nx) >= Math.abs(ny)) {
    return nx >= 0 ? 'right' : 'left'
  }
  return ny >= 0 ? 'bottom' : 'top'
}

/** Where an edge toward the point attaches: on the outline, or at the facing corner of a diamond. */
export const anchor = (s: Shape, toward: Point): Point => (s.kind === 'decision' ? sidePoint(s, facingSide(s, toward)) : borderPoint(s, toward))
