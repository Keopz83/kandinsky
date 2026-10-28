import type { Shape } from '../types'
import type { Box, Point } from './point'

/** Does the selection box select the shape? It must touch it, or enclose it completely for a group. */
export function shapeTouches(s: Shape, box: Box) {
  if (s.kind === 'group') {
    return s.x >= box.x && s.y >= box.y && s.x + s.width <= box.x + box.width && s.y + s.height <= box.y + box.height
  }
  return s.x <= box.x + box.width && s.x + s.width >= box.x && s.y <= box.y + box.height && s.y + s.height >= box.y
}

/** Liang–Barsky clipping: does the segment a→b touch the box? */
function segmentTouches(a: Point, b: Point, box: Box) {
  const dx = b.x - a.x
  const dy = b.y - a.y
  let enter = 0
  let leave = 1
  const boundaries = [
    [-dx, a.x - box.x],
    [dx, box.x + box.width - a.x],
    [-dy, a.y - box.y],
    [dy, box.y + box.height - a.y],
  ]
  for (const [direction, distanceInside] of boundaries) {
    if (direction === 0 && distanceInside < 0) {
      return false
    }
    if (direction < 0) {
      enter = Math.max(enter, distanceInside / direction)
    } else if (direction > 0) {
      leave = Math.min(leave, distanceInside / direction)
    }
    if (enter > leave) {
      return false
    }
  }
  return true
}

export const pathTouches = (path: Point[], box: Box) => path.slice(1).some((p, i) => segmentTouches(path[i], p, box))
