import { distance, type Point } from './point'

/** Coordinates closer than this are treated as equal (half a pixel). */
const SAME_COORDINATE_TOLERANCE = 0.5

export const isSameCoordinate = (a: number, b: number) => Math.abs(a - b) < SAME_COORDINATE_TOLERANCE

/** The SVG `points` attribute of a polygon or polyline. */
export const toPoints = (points: Point[]) => points.map((p) => `${p.x},${p.y}`).join(' ')

const segmentLengths = (path: Point[]) => path.slice(1).map((p, i) => distance(p, path[i]))

export const pathLength = (path: Point[]) => segmentLengths(path).reduce((sum, length) => sum + length, 0)

/** The point halfway along the path, where the edge label goes. */
export function pathMidpoint(path: Point[]): Point {
  const lengths = segmentLengths(path)
  let remaining = pathLength(path) / 2
  for (let i = 0; i < lengths.length; i++) {
    if (remaining <= lengths[i] && lengths[i] > 0) {
      const t = remaining / lengths[i]
      return { x: path[i].x + (path[i + 1].x - path[i].x) * t, y: path[i].y + (path[i + 1].y - path[i].y) * t }
    }
    remaining -= lengths[i]
  }
  return path[0]
}

/** Drops repeated points and points in the middle of a straight horizontal or vertical run. */
export function simplify(path: Point[]): Point[] {
  const distinct = path.filter((p, i) => i === 0 || !isSameCoordinate(p.x, path[i - 1].x) || !isSameCoordinate(p.y, path[i - 1].y))
  return distinct.filter((p, i) => {
    if (i === 0 || i === distinct.length - 1) {
      return true
    }
    const before = distinct[i - 1]
    const after = distinct[i + 1]
    const inVerticalRun = isSameCoordinate(before.x, p.x) && isSameCoordinate(after.x, p.x)
    const inHorizontalRun = isSameCoordinate(before.y, p.y) && isSameCoordinate(after.y, p.y)
    return !(inVerticalRun || inHorizontalRun)
  })
}
