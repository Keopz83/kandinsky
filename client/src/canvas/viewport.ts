import type { Point } from '../geometry/point'

/** The visible part of the whiteboard: screen position of the diagram origin and the zoom factor. */
export interface View {
  x: number
  y: number
  zoom: number
}

export const DEFAULT_VIEW: View = { x: 0, y: 0, zoom: 1 }

const MIN_ZOOM = 0.25
const MAX_ZOOM = 4
/** Wheel events closer together than this continue the same trackpad gesture. */
export const TRACKPAD_GESTURE_GAP_MS = 150
/** Pixels per line when the browser reports wheel deltas in lines (Firefox). */
const PIXELS_PER_LINE = 16
const WHEEL_ZOOM_SPEED = 0.002
// Pinch deltas are small; scale them up so pinching feels as fast as the wheel.
const PINCH_ZOOM_SPEED = 0.01
/** Chromium and Safari report a mouse wheel notch as a legacy `wheelDeltaY` of exactly this multiple of `deltaY`. */
const LEGACY_WHEEL_DELTA_RATIO = -3

/**
 * Tells a trackpad two-finger scroll from a mouse wheel notch. Mouse wheels scroll by lines (Firefox) or
 * by notches whose legacy `wheelDeltaY` is not exactly -3 × `deltaY` (Chromium, Safari); trackpads send
 * smooth pixel deltas, often horizontal too. Events continuing a recent trackpad gesture stay one.
 */
export function isTrackpadScroll(e: WheelEvent, continuesGesture: boolean) {
  if (e.deltaMode !== WheelEvent.DOM_DELTA_PIXEL) {
    return false
  }
  if (continuesGesture || e.deltaX !== 0) {
    return true
  }
  const legacy = (e as WheelEvent & { wheelDeltaY?: number }).wheelDeltaY
  return legacy !== undefined && legacy !== 0 && legacy === LEGACY_WHEEL_DELTA_RATIO * e.deltaY
}

export const pannedBy = (view: View, dx: number, dy: number): View => ({ ...view, x: view.x + dx, y: view.y + dy })

/** Zooms in for a forward wheel turn (negative delta), keeping the diagram point under the cursor in place. */
export function zoomedByWheel(view: View, e: WheelEvent, cursor: Point): View {
  const delta = e.deltaMode === WheelEvent.DOM_DELTA_LINE ? e.deltaY * PIXELS_PER_LINE : e.deltaY
  const speed = e.ctrlKey ? PINCH_ZOOM_SPEED : WHEEL_ZOOM_SPEED
  const zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, view.zoom * Math.exp(-delta * speed)))
  const scale = zoom / view.zoom
  return { zoom, x: cursor.x - (cursor.x - view.x) * scale, y: cursor.y - (cursor.y - view.y) * scale }
}

/** The diagram point shown at a screen position relative to the whiteboard's top left corner. */
export const toDiagramPoint = (view: View, screen: Point): Point => ({ x: (screen.x - view.x) / view.zoom, y: (screen.y - view.y) / view.zoom })
