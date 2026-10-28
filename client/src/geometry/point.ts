export interface Point {
  x: number
  y: number
}

export interface Box {
  x: number
  y: number
  width: number
  height: number
}

export const distance = (a: Point, b: Point) => Math.hypot(a.x - b.x, a.y - b.y)

export const centroid = (points: Point[]): Point => ({
  x: points.reduce((sum, p) => sum + p.x, 0) / points.length,
  y: points.reduce((sum, p) => sum + p.y, 0) / points.length,
})

/** The box spanned by two opposite corners, e.g. of a selection marquee. */
export const boxOf = (a: Point, b: Point): Box => ({
  x: Math.min(a.x, b.x),
  y: Math.min(a.y, b.y),
  width: Math.abs(a.x - b.x),
  height: Math.abs(a.y - b.y),
})
