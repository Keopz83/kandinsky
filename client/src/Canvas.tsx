import { useEffect, useRef, useState } from 'react'
import type { DragEvent, MouseEvent as ReactMouseEvent, PointerEvent } from 'react'
import { api } from './api'
import { LabelInput } from './LabelInput'
import { DEFAULT_SIZE, ShapeOutline, boxOf, center, corners, drawOrder, edgePath, labelBelow, labelBox, nearestCorner, pathMidpoint, pathTouches, shapeTouches, toPoints, withContent } from './geometry'
import type { Point } from './geometry'
import { SHAPE_KINDS } from './types'
import type { Corner, Diagram, Edge, EdgeRouting, Shape, ShapeKind, Tool } from './types'

export const SHAPE_MIME = 'application/x-kandinsky-shape'

const MIN_SIZE = 20
const EDGE_LABEL_WIDTH = 160
const EDGE_LABEL_HEIGHT = 28
const MIN_ZOOM = 0.25
const MAX_ZOOM = 4
const GRID = 20

export type Mutate = (action: (diagramId: string) => Promise<unknown>) => Promise<void>

type Selection = { kind: 'shape' | 'edge'; id: string } | null

interface Marquee {
  start: Point
  current: Point
  additive: boolean
}

interface Drag {
  kind: 'move' | 'resize'
  copy: boolean
  start: Point
  /** The shape or edge that was grabbed. */
  target: string
  originals: Shape[]
  currents: Shape[]
  /** Selected edges, recreated between the copies when copying. */
  edges: Edge[]
  active: boolean
}

/** Dragging an end of an edge to another corner of the diamond it is attached to. */
interface CornerDrag {
  edgeId: string
  end: 'source' | 'target'
  /** The corner nearest to the pointer, where the end goes when released. */
  corner: Corner
  moved: boolean
}

const cornerKey = (end: CornerDrag['end']) => (end === 'source' ? 'sourceCorner' : 'targetCorner')

/** The edge as drawn while one of its ends is dragged to another corner. */
const withCornerDrag = (edge: Edge, drag: CornerDrag | null): Edge =>
  drag?.edgeId === edge.id ? { ...edge, [cornerKey(drag.end)]: drag.corner } : edge

/** The right-click menu of an edge, at a position within the canvas. */
interface EdgeMenu {
  edgeId: string
  x: number
  y: number
}

const unchanged = (a: Shape, b: Shape) => a.x === b.x && a.y === b.y && a.width === b.width && a.height === b.height

/** The visible part of the whiteboard: screen position of the diagram origin and the zoom factor. */
interface View {
  x: number
  y: number
  zoom: number
}

const DEFAULT_VIEW: View = { x: 0, y: 0, zoom: 1 }

const TRACKPAD_GESTURE_GAP_MS = 150

/**
 * Tells a trackpad two-finger scroll from a mouse wheel notch. Mouse wheels scroll by lines (Firefox) or
 * by notches whose legacy `wheelDeltaY` is not exactly -3 × `deltaY` (Chromium, Safari); trackpads send
 * smooth pixel deltas, often horizontal too. Events continuing a recent trackpad gesture stay one.
 */
function isTrackpadScroll(e: WheelEvent, continuesGesture: boolean) {
  if (e.deltaMode !== WheelEvent.DOM_DELTA_PIXEL) {
    return false
  }
  if (continuesGesture || e.deltaX !== 0) {
    return true
  }
  const legacy = (e as WheelEvent & { wheelDeltaY?: number }).wheelDeltaY
  return legacy !== undefined && legacy !== 0 && legacy === -3 * e.deltaY
}

interface Pan {
  pointerId: number
  client: Point
  view: View
}

/** Two fingers on a touch screen dragging the whiteboard: their midpoint at the start and the view then. */
interface TouchPan {
  client: Point
  view: View
}

interface Props {
  diagram: Diagram
  tool: Tool
  /** Ids of the selected shapes and edges (they share one id space). */
  selected: string[]
  onSelect: (ids: string[]) => void
  onToolDone: () => void
  mutate: Mutate
}

const midpoint = (points: Point[]): Point => ({
  x: points.reduce((sum, p) => sum + p.x, 0) / points.length,
  y: points.reduce((sum, p) => sum + p.y, 0) / points.length,
})

export function Canvas({ diagram, tool, selected, onSelect, onToolDone, mutate }: Props) {
  const svgRef = useRef<SVGSVGElement>(null)
  const [views, setViews] = useState<Record<string, View>>({})
  const [pan, setPan] = useState<Pan | null>(null)
  const touches = useRef(new Map<number, Point>())
  const [touchPan, setTouchPan] = useState<TouchPan | null>(null)
  const view = views[diagram.id] ?? DEFAULT_VIEW
  const [editing, setEditing] = useState<Selection>(null)
  const [drag, setDrag] = useState<Drag | null>(null)
  const [marquee, setMarquee] = useState<Marquee | null>(null)
  const [edgeSource, setEdgeSource] = useState<string | null>(null)
  const [pointer, setPointer] = useState<Point | null>(null)
  const [menu, setMenu] = useState<EdgeMenu | null>(null)
  const [cornerDrag, setCornerDrag] = useState<CornerDrag | null>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  // Last mouse position over the whiteboard (client coordinates), where pasted diagrams are centered.
  const hover = useRef<{ clientX: number; clientY: number } | null>(null)

  const dragged = new Map(drag && !drag.copy ? drag.currents.map((s) => [s.id, s]) : [])
  const shapes = diagram.shapes.map((s) => dragged.get(s.id) ?? s)
  const ordered = drawOrder(shapes)
  const byId = new Map(shapes.map((s) => [s.id, s]))
  const connecting = tool === 'edge' || tool === 'angled-edge'
  const routing: EdgeRouting = tool === 'angled-edge' ? 'angled' : 'straight'
  const source = connecting && edgeSource ? byId.get(edgeSource) : undefined
  const placing = tool !== 'select' && !connecting ? tool : null
  const selectedShapes = selected.filter((id) => byId.has(id))
  const selectedEdges = diagram.edges.filter((edge) => selected.includes(edge.id))

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) {
        return
      }
      if (e.key === 'Escape') {
        setMenu(null)
        setEdgeSource(null)
        onSelect([])
        onToolDone()
      }
      if ((e.key === 'Delete' || e.key === 'Backspace') && selected.length > 0) {
        e.preventDefault()
        const ids = selected
        onSelect([])
        void mutate(async (d) => {
          // Edges first: deleting a shape also removes its edges on the server.
          for (const edge of diagram.edges.filter((edge) => ids.includes(edge.id))) {
            await api.deleteEdge(d, edge.id)
          }
          // A group is deleted with its content, so shapes inside a deleted group are already gone.
          const groups = diagram.shapes.filter((shape) => shape.kind === 'group' && ids.includes(shape.id)).map((shape) => shape.id)
          const content = withContent(diagram.shapes, groups)
          for (const shape of diagram.shapes.filter((shape) => ids.includes(shape.id) && !(shape.parentId && content.has(shape.parentId)))) {
            await api.deleteShape(d, shape.id)
          }
        })
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [diagram, selected, onSelect, mutate, onToolDone])

  // The edge menu closes on any press outside it and when the whiteboard is zoomed or panned.
  useEffect(() => {
    if (!menu) {
      return
    }
    const close = (e: Event) => {
      if (!(e.target instanceof Node && menuRef.current?.contains(e.target))) {
        setMenu(null)
      }
    }
    window.addEventListener('pointerdown', close, true)
    window.addEventListener('wheel', close, true)
    return () => {
      window.removeEventListener('pointerdown', close, true)
      window.removeEventListener('wheel', close, true)
    }
  }, [menu])

  /** Right-click on an edge selects it and opens its menu (Ctrl+click on macOS keeps toggling the selection). */
  function openEdgeMenu(e: ReactMouseEvent, edgeId: string) {
    if (e.ctrlKey) {
      return
    }
    e.preventDefault()
    e.stopPropagation()
    const rect = svgRef.current!.getBoundingClientRect()
    onSelect([edgeId])
    setMenu({ edgeId, x: e.clientX - rect.left, y: e.clientY - rect.top })
  }

  function setRouting(edgeId: string, routing: EdgeRouting) {
    setMenu(null)
    void mutate((d) => api.updateEdge(d, edgeId, { routing }))
  }

  function releaseCorners(edgeId: string) {
    setMenu(null)
    void mutate((d) => api.updateEdge(d, edgeId, { sourceCorner: 'auto', targetCorner: 'auto' }))
  }

  /** Grabs the end of the selected edge that is attached to a diamond. */
  function startCornerDrag(e: PointerEvent, edge: Edge, end: CornerDrag['end']) {
    e.stopPropagation()
    if (e.button !== 0) {
      return
    }
    const shape = byId.get(end === 'source' ? edge.sourceId : edge.targetId)
    if (shape) {
      setCornerDrag({ edgeId: edge.id, end, corner: nearestCorner(shape, toPoint(e)), moved: false })
    }
  }

  /** Releasing the dragged end fixes it to the nearest corner; leaving the whiteboard cancels. */
  async function endCornerDrag(commit: boolean) {
    if (!cornerDrag) {
      return
    }
    const edge = diagram.edges.find((edge) => edge.id === cornerDrag.edgeId)
    const key = cornerKey(cornerDrag.end)
    if (commit && cornerDrag.moved && edge && edge[key] !== cornerDrag.corner) {
      // Keep showing the dragged corner until the server state has been reloaded.
      await mutate((d) => api.updateEdge(d, edge.id, { [key]: cornerDrag.corner }))
    }
    setCornerDrag(null)
  }

  // Ctrl/Cmd+V pastes PlantUML or Mermaid text from the clipboard, centered on the mouse or else the visible area.
  function onPaste(e: ClipboardEvent) {
    const target = e.target
    if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || (target instanceof HTMLElement && target.isContentEditable)) {
      return
    }
    const text = e.clipboardData?.getData('text/plain') ?? ''
    if (!text.trim()) {
      return
    }
    e.preventDefault()
    const rect = svgRef.current!.getBoundingClientRect()
    const at = toPoint(hover.current ?? { clientX: rect.left + rect.width / 2, clientY: rect.top + rect.height / 2 })
    void mutate(async (d) => {
      const pasted = await api.paste(d, text, at)
      onSelect([...pasted.shapeIds, ...pasted.edgeIds])
    })
  }
  // The ref keeps the listener on the latest view and diagram.
  const pasteHandler = useRef(onPaste)
  pasteHandler.current = onPaste
  useEffect(() => {
    const listener = (e: ClipboardEvent) => pasteHandler.current(e)
    window.addEventListener('paste', listener)
    return () => window.removeEventListener('paste', listener)
  }, [])

  // Mouse wheel forward zooms in, backward zooms out, keeping the point under the cursor in place.
  // A two-finger move on a trackpad drags the whiteboard; a trackpad pinch (sent with ctrlKey) zooms.
  useEffect(() => {
    const svg = svgRef.current!
    const diagramId = diagram.id
    let lastTrackpadScroll = 0
    function onWheel(e: WheelEvent) {
      if (e.deltaX === 0 && e.deltaY === 0) {
        return
      }
      e.preventDefault()
      if (!e.ctrlKey && isTrackpadScroll(e, e.timeStamp - lastTrackpadScroll < TRACKPAD_GESTURE_GAP_MS)) {
        lastTrackpadScroll = e.timeStamp
        setViews((views) => {
          const v = views[diagramId] ?? DEFAULT_VIEW
          return { ...views, [diagramId]: { ...v, x: v.x + e.deltaX, y: v.y + e.deltaY } }
        })
        return
      }
      if (e.deltaY === 0) {
        return
      }
      const delta = e.deltaMode === WheelEvent.DOM_DELTA_LINE ? e.deltaY * 16 : e.deltaY
      // Pinch deltas are small; scale them up so pinching feels as fast as the wheel.
      const speed = e.ctrlKey ? 0.01 : 0.002
      const rect = svg.getBoundingClientRect()
      const cursor = { x: e.clientX - rect.left, y: e.clientY - rect.top }
      setViews((views) => {
        const v = views[diagramId] ?? DEFAULT_VIEW
        const zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, v.zoom * Math.exp(-delta * speed)))
        const scale = zoom / v.zoom
        return { ...views, [diagramId]: { zoom, x: cursor.x - (cursor.x - v.x) * scale, y: cursor.y - (cursor.y - v.y) * scale } }
      })
    }
    svg.addEventListener('wheel', onWheel, { passive: false })
    return () => svg.removeEventListener('wheel', onWheel)
  }, [diagram.id])

  // Pressing the mouse wheel and dragging pans the whiteboard, also when starting on a shape.
  // On a touch screen, moving two fingers pans it; the second finger cancels what the first one started.
  function onPanStart(e: PointerEvent<HTMLDivElement>) {
    if (e.pointerType === 'touch') {
      touches.current.set(e.pointerId, { x: e.clientX, y: e.clientY })
      if (touches.current.size === 2) {
        setDrag(null)
        setMarquee(null)
        setTouchPan({ client: midpoint([...touches.current.values()]), view })
      }
      if (touches.current.size >= 2) {
        e.preventDefault()
        e.stopPropagation()
      }
      return
    }
    if (e.button !== 1) {
      return
    }
    e.preventDefault()
    e.stopPropagation()
    e.currentTarget.setPointerCapture(e.pointerId)
    setPan({ pointerId: e.pointerId, client: { x: e.clientX, y: e.clientY }, view })
  }

  function onPanMove(e: PointerEvent<HTMLDivElement>) {
    if (e.pointerType === 'touch' && touches.current.has(e.pointerId)) {
      touches.current.set(e.pointerId, { x: e.clientX, y: e.clientY })
      if (touchPan) {
        e.stopPropagation()
        const m = midpoint([...touches.current.values()].slice(0, 2))
        const next = { ...touchPan.view, x: touchPan.view.x + m.x - touchPan.client.x, y: touchPan.view.y + m.y - touchPan.client.y }
        setViews((views) => ({ ...views, [diagram.id]: next }))
      }
      return
    }
    if (!pan || e.pointerId !== pan.pointerId) {
      return
    }
    e.stopPropagation()
    const next = { ...pan.view, x: pan.view.x + e.clientX - pan.client.x, y: pan.view.y + e.clientY - pan.client.y }
    setViews((views) => ({ ...views, [diagram.id]: next }))
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
    if (pan && e.pointerId === pan.pointerId) {
      e.stopPropagation()
      setPan(null)
    }
  }

  function toPoint(e: { clientX: number; clientY: number }): Point {
    const rect = svgRef.current!.getBoundingClientRect()
    return { x: (e.clientX - rect.left - view.x) / view.zoom, y: (e.clientY - rect.top - view.y) / view.zoom }
  }

  /** The topmost shape containing the point, if any. */
  function shapeAt(p: Point) {
    return [...ordered].reverse().find((s) => p.x >= s.x && p.x <= s.x + s.width && p.y >= s.y && p.y <= s.y + s.height)
  }

  /** Finishes dragging an edge: releasing on another shape connects it, anywhere else cancels. */
  function endConnect(p: Point | null) {
    if (!edgeSource) {
      return
    }
    const sourceId = edgeSource
    setEdgeSource(null)
    setPointer(null)
    const target = p && shapeAt(p)
    if (target && target.id !== sourceId) {
      void mutate((d) => api.addEdge(d, sourceId, target.id, routing))
    }
  }

  // The shapes a move carries along: the selected ones, the endpoints of selected edges and the content of these groups.
  function shapesToMove(ids: string[]) {
    const endpoints = diagram.edges.filter((edge) => ids.includes(edge.id)).flatMap((edge) => [edge.sourceId, edge.targetId])
    const moved = withContent(shapes, [...ids.filter((id) => byId.has(id)), ...endpoints])
    return shapes.filter((s) => moved.has(s.id))
  }

  /** Starts moving (or resizing) on a shape or edge; `id` is the grabbed object. */
  function startDrag(e: PointerEvent, id: string, kind: Drag['kind']) {
    e.stopPropagation()
    if (e.button !== 0) {
      return
    }
    if (connecting) {
      if (byId.has(id)) {
        setEdgeSource(id)
        setPointer(toPoint(e))
      }
      return
    }
    if (placing) {
      placeShape(e, placing)
      return
    }
    if (kind === 'move' && (e.ctrlKey || e.metaKey)) {
      onSelect(selected.includes(id) ? selected.filter((s) => s !== id) : [...selected, id])
      return
    }
    const ids = kind === 'move' && selected.includes(id) ? selected : [id]
    if (!selected.includes(id)) {
      onSelect([id])
    }
    const group = kind === 'resize' ? shapes.filter((s) => s.id === id) : shapesToMove(ids)
    const edges = diagram.edges.filter((edge) => ids.includes(edge.id))
    setDrag({ kind, copy: kind === 'move' && e.shiftKey, start: toPoint(e), target: id, originals: group, currents: group, edges, active: true })
  }

  function onPointerMove(e: PointerEvent) {
    hover.current = { clientX: e.clientX, clientY: e.clientY }
    const p = toPoint(e)
    if (source) {
      setPointer(p)
    }
    if (cornerDrag) {
      const edge = diagram.edges.find((edge) => edge.id === cornerDrag.edgeId)
      const shape = edge && byId.get(cornerDrag.end === 'source' ? edge.sourceId : edge.targetId)
      if (shape) {
        setCornerDrag({ ...cornerDrag, corner: nearestCorner(shape, p), moved: true })
      }
    }
    if (marquee) {
      setMarquee({ ...marquee, current: p })
    }
    if (!drag?.active) {
      return
    }
    const originals = drag.originals
    if (drag.kind === 'resize') {
      const o = originals[0]
      const width = Math.max(MIN_SIZE, Math.round(o.width + p.x - drag.start.x))
      const height = Math.max(MIN_SIZE, Math.round(o.height + p.y - drag.start.y))
      setDrag({ ...drag, currents: [{ ...o, width, height }] })
      return
    }
    // Clamp the group as a whole so the relative layout is preserved at the canvas edge.
    const dx = Math.max(Math.round(p.x - drag.start.x), -Math.min(...originals.map((s) => s.x)))
    const dy = Math.max(Math.round(p.y - drag.start.y), -Math.min(...originals.map((s) => s.y)))
    setDrag({ ...drag, currents: originals.map((o) => ({ ...o, x: o.x + dx, y: o.y + dy })) })
  }

  async function endDrag() {
    if (!drag?.active) {
      return
    }
    const { originals, currents } = drag
    if (currents.every((c, i) => unchanged(c, originals[i]))) {
      // A plain click on one of several selected objects selects just that one.
      if (selected.length > 1 && selected.includes(drag.target)) {
        onSelect([drag.target])
      }
      setDrag(null)
      return
    }
    // Keep showing the dragged geometry until the server state has been reloaded.
    setDrag({ ...drag, active: false })
    if (drag.copy) {
      await mutate(async (d) => {
        const copies = new Map<string, string>()
        for (const c of currents) {
          const copy = await api.addShape(d, { kind: c.kind, label: c.label, x: c.x, y: c.y, width: c.width, height: c.height })
          copies.set(c.id, copy.id)
        }
        const edgeIds: string[] = []
        for (const edge of drag.edges) {
          const copy = await api.addEdge(d, copies.get(edge.sourceId)!, copies.get(edge.targetId)!, edge.routing, edge.label, {
            sourceCorner: edge.sourceCorner,
            targetCorner: edge.targetCorner,
          })
          edgeIds.push(copy.id)
        }
        // Endpoints copied only to carry an edge along weren't selected, so they don't become selected either.
        onSelect([...currents.filter((c) => selected.includes(c.id)).map((c) => copies.get(c.id)!), ...edgeIds])
      })
    } else {
      await mutate(async (d) => {
        for (const c of currents) {
          await api.updateShape(d, c.id, { x: c.x, y: c.y, width: c.width, height: c.height })
        }
      })
    }
    setDrag(null)
  }

  function onBackgroundPointerDown(e: PointerEvent) {
    if (e.target !== e.currentTarget || e.button !== 0) {
      return
    }
    setEdgeSource(null)
    if (placing) {
      placeShape(e, placing)
      return
    }
    if (tool !== 'select') {
      return
    }
    const additive = e.ctrlKey || e.metaKey
    if (!additive) {
      onSelect([])
    }
    const p = toPoint(e)
    setMarquee({ start: p, current: p, additive })
  }

  function endMarquee() {
    if (!marquee) {
      return
    }
    setMarquee(null)
    const box = boxOf(marquee.start, marquee.current)
    if (box.width === 0 && box.height === 0) {
      return
    }
    const inside = [
      ...shapes.filter((s) => shapeTouches(s, box)).map((s) => s.id),
      ...diagram.edges
        .filter((edge) => {
          const from = byId.get(edge.sourceId)
          const to = byId.get(edge.targetId)
          if (!from || !to) {
            return false
          }
          return pathTouches(edgePath(from, to, edge), box)
        })
        .map((edge) => edge.id),
    ]
    if (inside.length > 0) {
      onSelect(marquee.additive ? [...selected, ...inside.filter((id) => !selected.includes(id))] : inside)
    }
  }

  function onPointerUp(e: PointerEvent) {
    endConnect(e.type === 'pointerup' ? toPoint(e) : null)
    void endCornerDrag(e.type === 'pointerup')
    endMarquee()
    void endDrag()
  }

  function onDragOver(e: DragEvent) {
    if (e.dataTransfer.types.includes(SHAPE_MIME)) {
      e.preventDefault()
      e.dataTransfer.dropEffect = 'copy'
    }
  }

  /** Adds a shape centered on the given client position and selects it. */
  function placeShape(e: { clientX: number; clientY: number }, kind: ShapeKind) {
    const p = toPoint(e)
    const { width, height } = DEFAULT_SIZE[kind]
    const x = Math.max(0, Math.round(p.x - width / 2))
    const y = Math.max(0, Math.round(p.y - height / 2))
    onToolDone()
    void mutate(async (d) => {
      const shape = await api.addShape(d, { kind, x, y, width, height })
      onSelect([shape.id])
    })
  }

  function onDrop(e: DragEvent) {
    if (!e.dataTransfer.types.includes(SHAPE_MIME)) {
      return
    }
    e.preventDefault()
    const kind = e.dataTransfer.getData(SHAPE_MIME) as ShapeKind
    placeShape(e, SHAPE_KINDS.includes(kind) ? kind : 'rectangle')
  }

  const stopEditing = () => setEditing(null)

  const hovered = source && pointer ? shapeAt(pointer) : undefined
  const dropTarget = hovered?.id !== source?.id ? hovered : undefined

  const grid = GRID * view.zoom

  // The ends of the only selected edge that are attached to a diamond get a handle to move them to another corner.
  const cornerEdge = tool === 'select' && selected.length === 1 && selectedEdges.length === 1 ? withCornerDrag(selectedEdges[0], cornerDrag) : null

  function renderCornerHandles(edge: Edge) {
    const from = byId.get(edge.sourceId)
    const to = byId.get(edge.targetId)
    if (!from || !to) {
      return null
    }
    const path = edgePath(from, to, edge)
    const ends = [
      { end: 'source' as const, shape: from, point: path[0] },
      { end: 'target' as const, shape: to, point: path[path.length - 1] },
    ].filter(({ shape }) => shape.kind === 'decision')
    return (
      <g>
        {ends.map(({ end, shape, point }) =>
          cornerDrag?.end === end ? (
            corners(shape).map(({ corner, point }) => (
              <circle key={`${end}-${corner}`} className={`corner-target ${corner === cornerDrag.corner ? 'active' : ''}`} cx={point.x} cy={point.y} r={5} />
            ))
          ) : (
            <circle
              key={end}
              className="corner-handle"
              cx={point.x}
              cy={point.y}
              r={5}
              aria-label={`Move the ${end} end to another corner`}
              onPointerDown={(e) => startCornerDrag(e, edge, end)}
            />
          ),
        )}
      </g>
    )
  }

  function renderShape(shape: Shape) {
    const isSelected = selected.includes(shape.id)
    // The first selected shape is the align anchor.
    const isAnchor = selectedShapes.length > 1 && selectedShapes[0] === shape.id
    const isEditing = editing?.kind === 'shape' && editing.id === shape.id
    return (
      <g
        key={shape.id}
        className={`shape ${shape.kind} ${isSelected ? 'selected' : ''} ${isAnchor ? 'anchor' : ''} ${source?.id === shape.id || dropTarget?.id === shape.id ? 'source' : ''}`}
      >
        <ShapeOutline
          shape={shape}
          onPointerDown={(e) => startDrag(e, shape.id, 'move')}
          onDoubleClick={() => setEditing({ kind: 'shape', id: shape.id })}
        />
        <foreignObject {...labelBox(shape)} pointerEvents={isEditing ? undefined : 'none'}>
          {isEditing ? (
            <LabelInput
              className="label-input"
              ariaLabel="Shape label"
              autoFocus
              value={shape.label}
              onCommit={(label) => label.trim() && void mutate((d) => api.updateShape(d, shape.id, { label }))}
              onDone={stopEditing}
            />
          ) : (
            <div className={`label ${labelBelow(shape) ? 'below' : ''}`}>{shape.label}</div>
          )}
        </foreignObject>
        {isSelected && selected.length === 1 && tool === 'select' && (
          <rect
            className="handle"
            x={shape.x + shape.width - 5}
            y={shape.y + shape.height - 5}
            width={10}
            height={10}
            onPointerDown={(e) => startDrag(e, shape.id, 'resize')}
          />
        )}
      </g>
    )
  }

  return (
    <div
      className={`canvas ${connecting ? 'connecting' : ''} ${placing ? 'placing' : ''} ${pan || touchPan ? 'panning' : ''} ${cornerDrag ? 'cornering' : ''}`}
      onPointerDownCapture={onPanStart}
      onPointerMoveCapture={onPanMove}
      onPointerUpCapture={onPanEnd}
      onPointerCancelCapture={onPanEnd}
      onMouseDown={(e) => e.button === 1 && e.preventDefault()}
    >
      <svg
        ref={svgRef}
        style={{ backgroundSize: `${grid}px ${grid}px`, backgroundPosition: `${view.x}px ${view.y}px` }}
        onPointerDown={onBackgroundPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerLeave={(e) => {
          hover.current = null
          onPointerUp(e)
        }}
        onContextMenu={(e) => e.ctrlKey && e.preventDefault()}
        onDragOver={onDragOver}
        onDrop={onDrop}
      >
        <defs>
          {['arrow', 'arrow-selected'].map((id) => (
            <marker key={id} id={id} viewBox="0 0 10 10" refX="10" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
              <path d="M0,0 L10,5 L0,10 z" className={id} />
            </marker>
          ))}
        </defs>
        <g transform={`translate(${view.x} ${view.y}) scale(${view.zoom})`}>
          {ordered.filter((shape) => shape.kind === 'group').map(renderShape)}

          {diagram.edges.map((edge) => {
            const from = byId.get(edge.sourceId)
            const to = byId.get(edge.targetId)
            if (!from || !to) {
              return null
            }
            const path = edgePath(from, to, withCornerDrag(edge, cornerDrag))
            const middle = pathMidpoint(path)
            const selected = selectedEdges.includes(edge)
            const isEditing = editing?.kind === 'edge' && editing.id === edge.id
            return (
              <g
                key={edge.id}
                className={`edge ${selected ? 'selected' : ''}`}
                onPointerDown={(e) => startDrag(e, edge.id, 'move')}
                onDoubleClick={() => setEditing({ kind: 'edge', id: edge.id })}
                onContextMenu={(e) => openEdgeMenu(e, edge.id)}
              >
                <polyline className="hit" points={toPoints(path)} />
                <polyline className="line" points={toPoints(path)} markerEnd={`url(#${selected ? 'arrow-selected' : 'arrow'})`} />
                {isEditing ? (
                  <foreignObject
                    x={middle.x - EDGE_LABEL_WIDTH / 2}
                    y={middle.y - EDGE_LABEL_HEIGHT / 2}
                    width={EDGE_LABEL_WIDTH}
                    height={EDGE_LABEL_HEIGHT}
                  >
                    <LabelInput
                      className="label-input"
                      ariaLabel="Edge label"
                      autoFocus
                      value={edge.label ?? ''}
                      onCommit={(label) => void mutate((d) => api.updateEdge(d, edge.id, { label }))}
                      onDone={stopEditing}
                    />
                  </foreignObject>
                ) : (
                  edge.label && (
                    <text x={middle.x} y={middle.y - 6} textAnchor="middle">
                      {edge.label}
                    </text>
                  )
                )}
              </g>
            )
          })}

          {ordered.filter((shape) => shape.kind !== 'group').map(renderShape)}

          {cornerEdge && renderCornerHandles(cornerEdge)}

          {drag?.copy && <CopyGhosts drag={drag} />}

          {marquee && <rect className="marquee" {...boxOf(marquee.start, marquee.current)} />}

          {source && pointer && (
            <polyline
              className="preview"
              points={toPoints(
                routing === 'angled'
                  ? edgePath(source, { id: '', kind: 'rectangle', label: '', x: pointer.x, y: pointer.y, width: 0, height: 0 }, { routing: 'angled' })
                  : [center(source), pointer],
              )}
            />
          )}
        </g>
      </svg>
      {menu && (
        <div ref={menuRef} className="dropdown-menu show edge-menu" style={{ left: menu.x, top: menu.y }} role="menu">
          {(['straight', 'angled'] as const).map((r) => {
            const current = diagram.edges.find((edge) => edge.id === menu.edgeId)?.routing === r
            return (
              <button key={r} type="button" role="menuitemradio" aria-checked={current} className="dropdown-item" onClick={() => setRouting(menu.edgeId, r)}>
                <span className="check">{current ? '✓' : ''}</span>
                {r === 'straight' ? 'Straight edge' : 'Angled edge'}
              </button>
            )
          })}
          {(() => {
            const edge = diagram.edges.find((edge) => edge.id === menu.edgeId)
            return (
              edge &&
              (edge.sourceCorner !== 'auto' || edge.targetCorner !== 'auto') && (
                <>
                  <hr className="dropdown-divider" />
                  <button type="button" role="menuitem" className="dropdown-item" onClick={() => releaseCorners(edge.id)}>
                    <span className="check" />
                    Automatic diamond corners
                  </button>
                </>
              )
            )
          })()}
        </div>
      )}
    </div>
  )
}

function CopyGhosts({ drag }: { drag: Drag }) {
  const byId = new Map(drag.currents.map((c) => [c.id, c]))
  return (
    <g pointerEvents="none">
      {drag.edges.map((edge) => {
        const path = edgePath(byId.get(edge.sourceId)!, byId.get(edge.targetId)!, edge)
        return (
          <g key={edge.id} className="edge ghost">
            <polyline className="line" points={toPoints(path)} markerEnd="url(#arrow)" />
          </g>
        )
      })}
      {drawOrder(drag.currents).map((c) => (
        <g key={c.id} className={`shape ${c.kind} ghost`}>
          <ShapeOutline shape={c} />
          <foreignObject {...labelBox(c)}>
            <div className={`label ${labelBelow(c) ? 'below' : ''}`}>{c.label}</div>
          </foreignObject>
        </g>
      ))}
    </g>
  )
}
