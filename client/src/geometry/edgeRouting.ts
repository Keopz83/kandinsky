import type { Edge, EdgeCorner, Shape } from '../types'
import type { Point } from './point'
import { isSameCoordinate, pathLength, simplify } from './polyline'
import { SIDE_DIRECTION, SIDES, anchor, center, sidePoint, type Side } from './shapeGeometry'

/** How far an angled edge runs straight out of a side before it turns. */
const STUB = 20

export type EdgeStyle = Pick<Edge, 'routing'> & Partial<Pick<Edge, 'sourceCorner' | 'targetCorner'>>

/** The corner an edge end is fixed to: only diamonds have fixed corners. */
const fixedCorner = (s: Shape, corner: EdgeCorner | undefined): Side | null =>
  s.kind === 'decision' && corner && corner !== 'auto' ? corner : null

/** Does b lie in front of a, looking in direction d? */
const isAhead = (a: Point, d: Point, b: Point) => (b.x - a.x) * d.x + (b.y - a.y) * d.y > 0

const stepOut = (p: Point, direction: Point): Point => ({ x: p.x + direction.x * STUB, y: p.y + direction.y * STUB })

/** Both ends leave horizontally: straight across, around the far side, or out and back via the middle. */
function horizontalEnds(s: Point, ds: Point, t: Point, dt: Point): Point[] {
  if (ds.x === -dt.x && isAhead(s, ds, t)) {
    const middleX = (s.x + t.x) / 2
    return simplify([s, { x: middleX, y: s.y }, { x: middleX, y: t.y }, t])
  }
  const s1 = stepOut(s, ds)
  const t1 = stepOut(t, dt)
  if (ds.x === dt.x) {
    const x = ds.x > 0 ? Math.max(s1.x, t1.x) : Math.min(s1.x, t1.x)
    return simplify([s, { x, y: s.y }, { x, y: t.y }, t])
  }
  const middleY = (s.y + t.y) / 2
  return simplify([s, s1, { x: s1.x, y: middleY }, { x: t1.x, y: middleY }, t1, t])
}

/** Both ends leave vertically; the mirror image of {@link horizontalEnds}. */
function verticalEnds(s: Point, ds: Point, t: Point, dt: Point): Point[] {
  const swap = (p: Point): Point => ({ x: p.y, y: p.x })
  return horizontalEnds(swap(s), swap(ds), swap(t), swap(dt)).map(swap)
}

/**
 * Horizontal and vertical segments from point s, leaving in direction ds, to point t, entered against direction dt
 * (dt points out of the target's side). Turns as rarely as practical and runs a stub out of a side before turning back.
 */
function orthogonal(s: Point, ds: Point, t: Point, dt: Point): Point[] {
  if (ds.x !== 0 && dt.x !== 0) {
    return horizontalEnds(s, ds, t, dt)
  }
  if (ds.y !== 0 && dt.y !== 0) {
    return verticalEnds(s, ds, t, dt)
  }
  const corner = ds.x !== 0 ? { x: t.x, y: s.y } : { x: s.x, y: t.y }
  if (isAhead(s, ds, corner) && isAhead(t, dt, corner)) {
    return [s, corner, t]
  }
  const s1 = stepOut(s, ds)
  const t1 = stepOut(t, dt)
  return simplify(ds.x !== 0 ? [s, s1, { x: s1.x, y: t1.y }, t1, t] : [s, s1, { x: t1.x, y: s1.y }, t1, t])
}

/** An angled path between the given sides; a free side (null) is the one giving the fewest turns, then the shortest path. */
function angledBetween(from: Shape, fromSide: Side | null, to: Shape, toSide: Side | null): Point[] {
  const paths = (fromSide ? [fromSide] : SIDES).flatMap((a) =>
    (toSide ? [toSide] : SIDES).map((b) => orthogonal(sidePoint(from, a), SIDE_DIRECTION[a], sidePoint(to, b), SIDE_DIRECTION[b])),
  )
  const isBetter = (p: Point[], best: Point[]) => p.length < best.length || (p.length === best.length && pathLength(p) < pathLength(best))
  return paths.reduce((best, p) => (isBetter(p, best) ? p : best))
}

/** Where `to` lies relative to `from` per axis: -1 before, 1 after, 0 overlapping. */
function relativePosition(from: Shape, to: Shape): Point {
  const x = to.x >= from.x + from.width ? 1 : to.x + to.width <= from.x ? -1 : 0
  const y = to.y >= from.y + from.height ? 1 : to.y + to.height <= from.y ? -1 : 0
  return { x, y }
}

/** A Z shape between shapes above each other or side by side, an L shape between diagonal ones; null if they overlap. */
function angledByPosition(from: Shape, to: Shape, { x: dx, y: dy }: Point): Point[] | null {
  if (dy !== 0 && dx === 0) {
    const s = sidePoint(from, dy > 0 ? 'bottom' : 'top')
    const t = sidePoint(to, dy > 0 ? 'top' : 'bottom')
    const middleY = (s.y + t.y) / 2
    return isSameCoordinate(s.x, t.x) ? [s, { x: s.x, y: t.y }] : [s, { x: s.x, y: middleY }, { x: t.x, y: middleY }, t]
  }
  if (dx !== 0 && dy === 0) {
    const s = sidePoint(from, dx > 0 ? 'right' : 'left')
    const t = sidePoint(to, dx > 0 ? 'left' : 'right')
    const middleX = (s.x + t.x) / 2
    return isSameCoordinate(s.y, t.y) ? [s, { x: t.x, y: s.y }] : [s, { x: middleX, y: s.y }, { x: middleX, y: t.y }, t]
  }
  if (dx !== 0 && dy !== 0) {
    const s = sidePoint(from, dx > 0 ? 'right' : 'left')
    const t = sidePoint(to, dy > 0 ? 'top' : 'bottom')
    return [s, { x: t.x, y: s.y }, t]
  }
  return null
}

function straightPath(from: Shape, fromCorner: Side | null, to: Shape, toCorner: Side | null): Point[] {
  const start = fromCorner ? sidePoint(from, fromCorner) : anchor(from, toCorner ? sidePoint(to, toCorner) : center(to))
  return [start, toCorner ? sidePoint(to, toCorner) : anchor(to, start)]
}

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
    const position = relativePosition(from, to)
    const overlapping = position.x === 0 && position.y === 0
    if ((fromCorner || toCorner) && !overlapping) {
      return angledBetween(from, fromCorner, to, toCorner)
    }
    const angled = angledByPosition(from, to, position)
    if (angled) {
      return angled
    }
  }
  return straightPath(from, fromCorner, to, toCorner)
}

/** The path of an edge being drawn, from its source shape to the pointer. */
export function pathToPoint(from: Shape, to: Point, routing: Edge['routing']): Point[] {
  if (routing === 'straight') {
    return [center(from), to]
  }
  const pointShape: Shape = { id: '', kind: 'rectangle', label: '', x: to.x, y: to.y, width: 0, height: 0 }
  return edgePath(from, pointShape, { routing })
}
