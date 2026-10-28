import type { Shape } from '../types'
import type { Point } from './point'
import { containsPoint, isGroup } from './shapeGeometry'

const area = (s: Shape) => s.width * s.height

/** Drawing order: groups first, outer (larger) before inner, so their content stays on top of them. */
export const drawOrder = (shapes: Shape[]) => [...shapes.filter(isGroup).sort((a, b) => area(b) - area(a)), ...shapes.filter((s) => !isGroup(s))]

/** The topmost shape containing the point, if any. */
export const topmostShapeAt = (orderedShapes: Shape[], p: Point) => [...orderedShapes].reverse().find((s) => containsPoint(s, p))

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
