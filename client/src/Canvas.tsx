import { useEffect, useRef, useState } from 'react'
import type { DragEvent, PointerEvent } from 'react'
import { api } from './api'
import { LabelInput } from './LabelInput'
import type { Diagram, Shape, Tool } from './types'

export const SHAPE_MIME = 'application/x-kandinsky-shape'

const MIN_SIZE = 20
const DEFAULT_WIDTH = 140
const DEFAULT_HEIGHT = 60
const MARGIN = 400
const EDGE_LABEL_WIDTH = 160
const EDGE_LABEL_HEIGHT = 28

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
  target: string
  originals: Shape[]
  currents: Shape[]
  active: boolean
}

const unchanged = (a: Shape, b: Shape) => a.x === b.x && a.y === b.y && a.width === b.width && a.height === b.height

interface Point {
  x: number
  y: number
}

interface Props {
  diagram: Diagram
  tool: Tool
  selected: string[]
  onSelect: (shapeIds: string[]) => void
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

export function Canvas({ diagram, tool, selected, onSelect, onToolDone, mutate }: Props) {
  const svgRef = useRef<SVGSVGElement>(null)
  const [selectedEdge, setSelectedEdge] = useState<string | null>(null)
  const [editing, setEditing] = useState<Selection>(null)
  const [drag, setDrag] = useState<Drag | null>(null)
  const [marquee, setMarquee] = useState<Marquee | null>(null)
  const [edgeSource, setEdgeSource] = useState<string | null>(null)
  const [pointer, setPointer] = useState<Point | null>(null)

  const dragged = new Map(drag && !drag.copy ? drag.currents.map((s) => [s.id, s]) : [])
  const shapes = diagram.shapes.map((s) => dragged.get(s.id) ?? s)
  const byId = new Map(shapes.map((s) => [s.id, s]))
  const source = tool === 'edge' && edgeSource ? byId.get(edgeSource) : undefined

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) {
        return
      }
      if (e.key === 'Escape') {
        setEdgeSource(null)
        setSelectedEdge(null)
        onSelect([])
        onToolDone()
      }
      if ((e.key === 'Delete' || e.key === 'Backspace') && (selectedEdge || selected.length > 0)) {
        e.preventDefault()
        const edgeId = selectedEdge
        const shapeIds = selected
        setSelectedEdge(null)
        onSelect([])
        void mutate(async (d) => {
          if (edgeId) {
            await api.deleteEdge(d, edgeId)
          }
          for (const id of shapeIds) {
            await api.deleteShape(d, id)
          }
        })
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [selected, selectedEdge, onSelect, mutate, onToolDone])

  function toPoint(e: { clientX: number; clientY: number }): Point {
    const rect = svgRef.current!.getBoundingClientRect()
    return { x: e.clientX - rect.left, y: e.clientY - rect.top }
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
    onToolDone()
    void mutate((d) => api.addEdge(d, sourceId, targetId))
  }

  function startDrag(e: PointerEvent, shape: Shape, kind: Drag['kind']) {
    e.stopPropagation()
    if (e.button !== 0) {
      return
    }
    if (tool === 'edge') {
      connect(shape.id)
      return
    }
    setSelectedEdge(null)
    if (kind === 'move' && (e.ctrlKey || e.metaKey)) {
      onSelect(selected.includes(shape.id) ? selected.filter((id) => id !== shape.id) : [...selected, shape.id])
      return
    }
    const group = kind === 'move' && selected.includes(shape.id) ? shapes.filter((s) => selected.includes(s.id)) : [shape]
    if (group.length === 1) {
      onSelect([shape.id])
    }
    setDrag({ kind, copy: kind === 'move' && e.shiftKey, start: toPoint(e), target: shape.id, originals: group, currents: group, active: true })
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
      if (originals.length > 1) {
        onSelect([drag.target])
      }
      setDrag(null)
      return
    }
    // Keep showing the dragged geometry until the server state has been reloaded.
    setDrag({ ...drag, active: false })
    if (drag.copy) {
      await mutate(async (d) => {
        const ids: string[] = []
        for (const c of currents) {
          const copy = await api.addShape(d, { label: c.label, x: c.x, y: c.y, width: c.width, height: c.height })
          ids.push(copy.id)
        }
        onSelect(ids)
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
    setSelectedEdge(null)
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
    const inside = shapes
      .filter((s) => s.x <= box.x + box.width && s.x + s.width >= box.x && s.y <= box.y + box.height && s.y + s.height >= box.y)
      .map((s) => s.id)
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

  const width = Math.max(0, ...shapes.map((s) => s.x + s.width)) + MARGIN
  const height = Math.max(0, ...shapes.map((s) => s.y + s.height)) + MARGIN

  return (
    <div className={`canvas ${tool === 'edge' ? 'connecting' : ''}`}>
      <svg
        ref={svgRef}
        width={width}
        height={height}
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

        {diagram.edges.map((edge) => {
          const from = byId.get(edge.sourceId)
          const to = byId.get(edge.targetId)
          if (!from || !to) {
            return null
          }
          const start = borderPoint(from, center(to))
          const end = borderPoint(to, center(from))
          const middle = { x: (start.x + end.x) / 2, y: (start.y + end.y) / 2 }
          const selected = selectedEdge === edge.id
          const isEditing = editing?.kind === 'edge' && editing.id === edge.id
          return (
            <g
              key={edge.id}
              className={`edge ${selected ? 'selected' : ''}`}
              onPointerDown={(e) => {
                e.stopPropagation()
                if (tool === 'select') {
                  setSelectedEdge(edge.id)
                  onSelect([])
                }
              }}
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
          const isAnchor = selected.length > 1 && selected[0] === shape.id
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
                onPointerDown={(e) => startDrag(e, shape, 'move')}
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
                  onPointerDown={(e) => startDrag(e, shape, 'resize')}
                />
              )}
            </g>
          )
        })}

        {drag?.copy &&
          drag.currents.map((c) => (
            <g key={c.id} className="shape ghost" pointerEvents="none">
              <rect x={c.x} y={c.y} width={c.width} height={c.height} rx={4} />
              <foreignObject x={c.x} y={c.y} width={c.width} height={c.height}>
                <div className="label">{c.label}</div>
              </foreignObject>
            </g>
          ))}

        {marquee && <rect className="marquee" {...boxOf(marquee.start, marquee.current)} />}

        {source && pointer && (
          <line className="preview" x1={center(source).x} y1={center(source).y} x2={pointer.x} y2={pointer.y} />
        )}
      </svg>
    </div>
  )
}
