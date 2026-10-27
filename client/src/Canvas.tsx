import { useEffect, useRef, useState } from 'react'
import type { DragEvent, PointerEvent } from 'react'
import { api } from './api'
import { LabelInput } from './LabelInput'
import type { Diagram, Edge, Shape, Tool } from './types'

export const SHAPE_MIME = 'application/x-kandinsky-shape'

const MIN_SIZE = 20
const DEFAULT_WIDTH = 140
const DEFAULT_HEIGHT = 60
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

const unchanged = (a: Shape, b: Shape) => a.x === b.x && a.y === b.y && a.width === b.width && a.height === b.height

interface Point {
  x: number
  y: number
}

/** The visible part of the whiteboard: screen position of the diagram origin and the zoom factor. */
interface View {
  x: number
  y: number
  zoom: number
}

const DEFAULT_VIEW: View = { x: 0, y: 0, zoom: 1 }

interface Pan {
  pointerId: number
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

const center = (s: Shape): Point => ({ x: s.x + s.width / 2, y: s.y + s.height / 2 })

const boxOf = (a: Point, b: Point) => ({
  x: Math.min(a.x, b.x),
  y: Math.min(a.y, b.y),
  width: Math.abs(a.x - b.x),
  height: Math.abs(a.y - b.y),
})

function borderPoint(s: Shape, toward: Point): Point {
  const c = center(s)
  const dx = toward.x - c.x
  const dy = toward.y - c.y
  if (dx === 0 && dy === 0) {
    return c
  }
  const scale = Math.min(s.width / 2 / Math.abs(dx), s.height / 2 / Math.abs(dy))
  return { x: c.x + dx * scale, y: c.y + dy * scale }
}

function edgeEnds(from: Shape, to: Shape) {
  return { start: borderPoint(from, center(to)), end: borderPoint(to, center(from)) }
}

type Box = ReturnType<typeof boxOf>

const shapeTouches = (s: Shape, box: Box) =>
  s.x <= box.x + box.width && s.x + s.width >= box.x && s.y <= box.y + box.height && s.y + s.height >= box.y

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

export function Canvas({ diagram, tool, selected, onSelect, onToolDone, mutate }: Props) {
  const svgRef = useRef<SVGSVGElement>(null)
  const [views, setViews] = useState<Record<string, View>>({})
  const [pan, setPan] = useState<Pan | null>(null)
  const view = views[diagram.id] ?? DEFAULT_VIEW
  const [editing, setEditing] = useState<Selection>(null)
  const [drag, setDrag] = useState<Drag | null>(null)
  const [marquee, setMarquee] = useState<Marquee | null>(null)
  const [edgeSource, setEdgeSource] = useState<string | null>(null)
  const [pointer, setPointer] = useState<Point | null>(null)

  const dragged = new Map(drag && !drag.copy ? drag.currents.map((s) => [s.id, s]) : [])
  const shapes = diagram.shapes.map((s) => dragged.get(s.id) ?? s)
  const byId = new Map(shapes.map((s) => [s.id, s]))
  const source = tool === 'edge' && edgeSource ? byId.get(edgeSource) : undefined
  const selectedShapes = selected.filter((id) => byId.has(id))
  const selectedEdges = diagram.edges.filter((edge) => selected.includes(edge.id))

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) {
        return
      }
      if (e.key === 'Escape') {
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
          for (const shape of diagram.shapes.filter((shape) => ids.includes(shape.id))) {
            await api.deleteShape(d, shape.id)
          }
        })
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [diagram, selected, onSelect, mutate, onToolDone])

  // Mouse wheel forward zooms in, backward zooms out, keeping the point under the cursor in place.
  useEffect(() => {
    const svg = svgRef.current!
    const diagramId = diagram.id
    function onWheel(e: WheelEvent) {
      if (e.deltaY === 0) {
        return
      }
      e.preventDefault()
      const delta = e.deltaMode === WheelEvent.DOM_DELTA_LINE ? e.deltaY * 16 : e.deltaY
      const rect = svg.getBoundingClientRect()
      const cursor = { x: e.clientX - rect.left, y: e.clientY - rect.top }
      setViews((views) => {
        const v = views[diagramId] ?? DEFAULT_VIEW
        const zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, v.zoom * Math.exp(-delta * 0.002)))
        const scale = zoom / v.zoom
        return { ...views, [diagramId]: { zoom, x: cursor.x - (cursor.x - v.x) * scale, y: cursor.y - (cursor.y - v.y) * scale } }
      })
    }
    svg.addEventListener('wheel', onWheel, { passive: false })
    return () => svg.removeEventListener('wheel', onWheel)
  }, [diagram.id])

  // Pressing the mouse wheel and dragging pans the whiteboard, also when starting on a shape.
  function onPanStart(e: PointerEvent<HTMLDivElement>) {
    if (e.button !== 1) {
      return
    }
    e.preventDefault()
    e.stopPropagation()
    e.currentTarget.setPointerCapture(e.pointerId)
    setPan({ pointerId: e.pointerId, client: { x: e.clientX, y: e.clientY }, view })
  }

  function onPanMove(e: PointerEvent<HTMLDivElement>) {
    if (!pan || e.pointerId !== pan.pointerId) {
      return
    }
    e.stopPropagation()
    const next = { ...pan.view, x: pan.view.x + e.clientX - pan.client.x, y: pan.view.y + e.clientY - pan.client.y }
    setViews((views) => ({ ...views, [diagram.id]: next }))
  }

  function onPanEnd(e: PointerEvent<HTMLDivElement>) {
    if (pan && e.pointerId === pan.pointerId) {
      e.stopPropagation()
      setPan(null)
    }
  }

  function toPoint(e: { clientX: number; clientY: number }): Point {
    const rect = svgRef.current!.getBoundingClientRect()
    return { x: (e.clientX - rect.left - view.x) / view.zoom, y: (e.clientY - rect.top - view.y) / view.zoom }
  }

  function connect(targetId: string) {
    if (!edgeSource) {
      setEdgeSource(targetId)
      return
    }
    if (edgeSource === targetId) {
      return
    }
    const sourceId = edgeSource
    setEdgeSource(null)
    setPointer(null)
    void mutate((d) => api.addEdge(d, sourceId, targetId))
  }

  // The shapes a move carries along: the selected ones plus the endpoints of selected edges.
  function shapesToMove(ids: string[]) {
    const endpoints = new Set(diagram.edges.filter((edge) => ids.includes(edge.id)).flatMap((edge) => [edge.sourceId, edge.targetId]))
    return shapes.filter((s) => ids.includes(s.id) || endpoints.has(s.id))
  }

  /** Starts moving (or resizing) on a shape or edge; `id` is the grabbed object. */
  function startDrag(e: PointerEvent, id: string, kind: Drag['kind']) {
    e.stopPropagation()
    if (e.button !== 0) {
      return
    }
    if (tool === 'edge') {
      if (byId.has(id)) {
        connect(id)
      }
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
    const p = toPoint(e)
    if (source) {
      setPointer(p)
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
          const copy = await api.addShape(d, { label: c.label, x: c.x, y: c.y, width: c.width, height: c.height })
          copies.set(c.id, copy.id)
        }
        const edgeIds: string[] = []
        for (const edge of drag.edges) {
          const copy = await api.addEdge(d, copies.get(edge.sourceId)!, copies.get(edge.targetId)!, edge.label)
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
          const { start, end } = edgeEnds(from, to)
          return segmentTouches(start, end, box)
        })
        .map((edge) => edge.id),
    ]
    if (inside.length > 0) {
      onSelect(marquee.additive ? [...selected, ...inside.filter((id) => !selected.includes(id))] : inside)
    }
  }

  function onPointerUp() {
    endMarquee()
    void endDrag()
  }

  function onDragOver(e: DragEvent) {
    if (e.dataTransfer.types.includes(SHAPE_MIME)) {
      e.preventDefault()
      e.dataTransfer.dropEffect = 'copy'
    }
  }

  function onDrop(e: DragEvent) {
    if (!e.dataTransfer.types.includes(SHAPE_MIME)) {
      return
    }
    e.preventDefault()
    const p = toPoint(e)
    const x = Math.max(0, Math.round(p.x - DEFAULT_WIDTH / 2))
    const y = Math.max(0, Math.round(p.y - DEFAULT_HEIGHT / 2))
    void mutate((d) => api.addShape(d, { x, y }))
  }

  const stopEditing = () => setEditing(null)

  const grid = GRID * view.zoom

  return (
    <div
      className={`canvas ${tool === 'edge' ? 'connecting' : ''} ${pan ? 'panning' : ''}`}
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
        onPointerLeave={onPointerUp}
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

          {diagram.edges.map((edge) => {
            const from = byId.get(edge.sourceId)
            const to = byId.get(edge.targetId)
            if (!from || !to) {
              return null
            }
            const { start, end } = edgeEnds(from, to)
            const middle = { x: (start.x + end.x) / 2, y: (start.y + end.y) / 2 }
            const selected = selectedEdges.includes(edge)
            const isEditing = editing?.kind === 'edge' && editing.id === edge.id
            return (
              <g
                key={edge.id}
                className={`edge ${selected ? 'selected' : ''}`}
                onPointerDown={(e) => startDrag(e, edge.id, 'move')}
                onDoubleClick={() => setEditing({ kind: 'edge', id: edge.id })}
              >
                <line className="hit" x1={start.x} y1={start.y} x2={end.x} y2={end.y} />
                <line
                  className="line"
                  x1={start.x}
                  y1={start.y}
                  x2={end.x}
                  y2={end.y}
                  markerEnd={`url(#${selected ? 'arrow-selected' : 'arrow'})`}
                />
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

          {shapes.map((shape) => {
            const isSelected = selected.includes(shape.id)
            // The first selected shape is the align anchor.
            const isAnchor = selectedShapes.length > 1 && selectedShapes[0] === shape.id
            const isEditing = editing?.kind === 'shape' && editing.id === shape.id
            return (
              <g
                key={shape.id}
                className={`shape ${isSelected ? 'selected' : ''} ${isAnchor ? 'anchor' : ''} ${source?.id === shape.id ? 'source' : ''}`}
              >
                <rect
                  x={shape.x}
                  y={shape.y}
                  width={shape.width}
                  height={shape.height}
                  rx={4}
                  onPointerDown={(e) => startDrag(e, shape.id, 'move')}
                  onDoubleClick={() => setEditing({ kind: 'shape', id: shape.id })}
                />
                <foreignObject
                  x={shape.x}
                  y={shape.y}
                  width={shape.width}
                  height={shape.height}
                  pointerEvents={isEditing ? undefined : 'none'}
                >
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
                    <div className="label">{shape.label}</div>
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
          })}

          {drag?.copy && <CopyGhosts drag={drag} />}

          {marquee && <rect className="marquee" {...boxOf(marquee.start, marquee.current)} />}

          {source && pointer && (
            <line className="preview" x1={center(source).x} y1={center(source).y} x2={pointer.x} y2={pointer.y} />
          )}
        </g>
      </svg>
    </div>
  )
}

function CopyGhosts({ drag }: { drag: Drag }) {
  const byId = new Map(drag.currents.map((c) => [c.id, c]))
  return (
    <g pointerEvents="none">
      {drag.edges.map((edge) => {
        const { start, end } = edgeEnds(byId.get(edge.sourceId)!, byId.get(edge.targetId)!)
        return (
          <g key={edge.id} className="edge ghost">
            <line className="line" x1={start.x} y1={start.y} x2={end.x} y2={end.y} markerEnd="url(#arrow)" />
          </g>
        )
      })}
      {drag.currents.map((c) => (
        <g key={c.id} className="shape ghost">
          <rect x={c.x} y={c.y} width={c.width} height={c.height} rx={4} />
          <foreignObject x={c.x} y={c.y} width={c.width} height={c.height}>
            <div className="label">{c.label}</div>
          </foreignObject>
        </g>
      ))}
    </g>
  )
}
