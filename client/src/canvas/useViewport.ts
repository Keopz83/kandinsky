import { useEffect, useRef, useState } from 'react'
import type { PointerEvent, RefObject } from 'react'
import { centroid, type Point } from '../geometry/point'
import { DEFAULT_VIEW, TRACKPAD_GESTURE_GAP_MS, isTrackpadScroll, pannedBy, toDiagramPoint, zoomedByWheel, type View } from './viewport'

const MIDDLE_BUTTON = 1

/** A pan with the pressed mouse wheel: the pointer position at the start and the view then. */
interface MousePan {
  pointerId: number
  client: Point
  view: View
}

/** Two fingers on a touch screen dragging the whiteboard: their midpoint at the start and the view then. */
interface TouchPan {
  client: Point
  view: View
}

export type ClientPosition = { clientX: number; clientY: number }

const clientPoint = (e: ClientPosition): Point => ({ x: e.clientX, y: e.clientY })

/**
 * Zooming and panning the whiteboard, remembered per diagram. The mouse wheel or a trackpad pinch zooms around the
 * pointer; a two-finger trackpad move, dragging with the wheel pressed or two fingers on a touch screen pans.
 * `onTouchPanStart` cancels what the first finger started.
 */
export function useViewport(svgRef: RefObject<SVGSVGElement | null>, diagramId: string, onTouchPanStart: () => void) {
  const [views, setViews] = useState<Record<string, View>>({})
  const [mousePan, setMousePan] = useState<MousePan | null>(null)
  const [touchPan, setTouchPan] = useState<TouchPan | null>(null)
  const touches = useRef(new Map<number, Point>())
  const view = views[diagramId] ?? DEFAULT_VIEW

  const showView = (next: View) => setViews((all) => ({ ...all, [diagramId]: next }))

  useEffect(() => {
    const svg = svgRef.current!
    let lastTrackpadScroll = 0
    const updateView = (change: (view: View) => View) =>
      setViews((all) => ({ ...all, [diagramId]: change(all[diagramId] ?? DEFAULT_VIEW) }))

    function onWheel(e: WheelEvent) {
      if (e.deltaX === 0 && e.deltaY === 0) {
        return
      }
      e.preventDefault()
      if (!e.ctrlKey && isTrackpadScroll(e, e.timeStamp - lastTrackpadScroll < TRACKPAD_GESTURE_GAP_MS)) {
        lastTrackpadScroll = e.timeStamp
        updateView((v) => pannedBy(v, e.deltaX, e.deltaY))
        return
      }
      if (e.deltaY === 0) {
        return
      }
      const rect = svg.getBoundingClientRect()
      const cursor = { x: e.clientX - rect.left, y: e.clientY - rect.top }
      updateView((v) => zoomedByWheel(v, e, cursor))
    }
    svg.addEventListener('wheel', onWheel, { passive: false })
    return () => svg.removeEventListener('wheel', onWheel)
  }, [svgRef, diagramId])

  function startTouch(e: PointerEvent<HTMLDivElement>) {
    touches.current.set(e.pointerId, clientPoint(e))
    if (touches.current.size === 2) {
      onTouchPanStart()
      setTouchPan({ client: centroid([...touches.current.values()]), view })
    }
    if (touches.current.size >= 2) {
      e.preventDefault()
      e.stopPropagation()
    }
  }

  function onPanStart(e: PointerEvent<HTMLDivElement>) {
    if (e.pointerType === 'touch') {
      startTouch(e)
      return
    }
    if (e.button !== MIDDLE_BUTTON) {
      return
    }
    e.preventDefault()
    e.stopPropagation()
    e.currentTarget.setPointerCapture(e.pointerId)
    setMousePan({ pointerId: e.pointerId, client: clientPoint(e), view })
  }

  function onPanMove(e: PointerEvent<HTMLDivElement>) {
    if (e.pointerType === 'touch' && touches.current.has(e.pointerId)) {
      touches.current.set(e.pointerId, clientPoint(e))
      if (touchPan) {
        e.stopPropagation()
        const fingers = centroid([...touches.current.values()].slice(0, 2))
        showView(pannedBy(touchPan.view, fingers.x - touchPan.client.x, fingers.y - touchPan.client.y))
      }
      return
    }
    if (mousePan?.pointerId === e.pointerId) {
      e.stopPropagation()
      showView(pannedBy(mousePan.view, e.clientX - mousePan.client.x, e.clientY - mousePan.client.y))
    }
  }

  function onPanEnd(e: PointerEvent<HTMLDivElement>) {
    if (touches.current.delete(e.pointerId)) {
      if (touchPan) {
        e.stopPropagation()
        if (touches.current.size < 2) {
          setTouchPan(null)
        }
      }
      return
    }
    if (mousePan?.pointerId === e.pointerId) {
      e.stopPropagation()
      setMousePan(null)
    }
  }

  /** The diagram point under a client (window) position. */
  function toPoint(e: ClientPosition): Point {
    const rect = svgRef.current!.getBoundingClientRect()
    return toDiagramPoint(view, { x: e.clientX - rect.left, y: e.clientY - rect.top })
  }

  return {
    view,
    isPanning: mousePan !== null || touchPan !== null,
    toPoint,
    panHandlers: {
      onPointerDownCapture: onPanStart,
      onPointerMoveCapture: onPanMove,
      onPointerUpCapture: onPanEnd,
      onPointerCancelCapture: onPanEnd,
    },
  }
}
