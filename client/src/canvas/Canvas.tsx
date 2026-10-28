import { useCallback, useRef, useState } from 'react'
import type { DragEvent, MouseEvent as ReactMouseEvent, PointerEvent } from 'react'
import { api } from '../api'
import { edgePath, pathToPoint } from '../geometry/edgeRouting'
import { boxOf, type Point } from '../geometry/point'
import { toPoints } from '../geometry/polyline'
import { DEFAULT_SIZE, isGroup, nearestCorner } from '../geometry/shapeGeometry'
import { drawOrder, topmostShapeAt, withContent } from '../geometry/shapeHierarchy'
import type { Diagram, Edge, EdgeRouting, Shape, ShapeKind, Tool } from '../types'
import { CopyGhosts } from './CopyGhosts'
import { CornerHandles } from './CornerHandles'
import { cornerKey, endShapeId, withCornerDrag, type CornerDrag, type EdgeEnd } from './cornerDrag'
import { copyShapes, deleteObjects, updateGeometry } from './diagramCommands'
import { droppedShapeKind, isShapeDrag } from './dragAndDrop'
import { EdgeContextMenu } from './EdgeContextMenu'
import { ArrowMarkers, EdgeView } from './EdgeView'
import { objectsInBox, toggled, withAdded } from './selection'
import { draggedTo, hasChanged, type ShapeDrag } from './shapeDrag'
import { ShapeView } from './ShapeView'
import { useCanvasShortcuts } from './useCanvasShortcuts'
import { useViewport, type ClientPosition } from './useViewport'

const GRID_SPACING = 20
const PRIMARY_BUTTON = 0
const MIDDLE_BUTTON = 1

export type Mutate = (action: (diagramId: string) => Promise<unknown>) => Promise<void>

/** The shape or edge whose label is being edited. */
type Editing = { kind: 'shape' | 'edge'; id: string } | null

interface Marquee {
  start: Point
  current: Point
  additive: boolean
}

/** The right-click menu of an edge, at a position within the canvas. */
interface EdgeMenu {
  edgeId: string
  position: Point
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

const hasModifier = (e: { ctrlKey: boolean; metaKey: boolean }) => e.ctrlKey || e.metaKey

export function Canvas({ diagram, tool, selected, onSelect, onToolDone, mutate }: Props) {
  const svgRef = useRef<SVGSVGElement>(null)
  const [editing, setEditing] = useState<Editing>(null)
  const [drag, setDrag] = useState<ShapeDrag | null>(null)
  const [marquee, setMarquee] = useState<Marquee | null>(null)
  const [edgeSourceId, setEdgeSourceId] = useState<string | null>(null)
  const [pointer, setPointer] = useState<Point | null>(null)
  const [menu, setMenu] = useState<EdgeMenu | null>(null)
  const [cornerDrag, setCornerDrag] = useState<CornerDrag | null>(null)
  // Last mouse position over the whiteboard, where pasted diagrams are centered.
  const hover = useRef<ClientPosition | null>(null)
  const { view, isPanning, toPoint, panHandlers } = useViewport(svgRef, diagram.id, () => {
    setDrag(null)
    setMarquee(null)
  })

  const draggedShapes = new Map(drag && !drag.copy ? drag.currents.map((s) => [s.id, s]) : [])
  const shapes = diagram.shapes.map((s) => draggedShapes.get(s.id) ?? s)
  const ordered = drawOrder(shapes)
  const byId = new Map(shapes.map((s) => [s.id, s]))
  const isConnecting = tool === 'edge' || tool === 'angled-edge'
  const routing: EdgeRouting = tool === 'angled-edge' ? 'angled' : 'straight'
  const placing = tool !== 'select' && !isConnecting ? tool : null
  const edgeSource = isConnecting && edgeSourceId ? byId.get(edgeSourceId) : undefined
  const selectedShapeIds = selected.filter((id) => byId.has(id))
  const selectedEdges = diagram.edges.filter((edge) => selected.includes(edge.id))
  const findEdge = (id: string) => diagram.edges.find((edge) => edge.id === id)
  const closeMenu = useCallback(() => setMenu(null), [])

  useCanvasShortcuts({
    onCancel: () => {
      setMenu(null)
      setEdgeSourceId(null)
      onSelect([])
      onToolDone()
    },
    hasSelection: selected.length > 0,
    onDelete: () => {
      const ids = selected
      onSelect([])
      void mutate((diagramId) => deleteObjects(diagramId, diagram, ids))
    },
    onPaste: pasteAtPointer,
  })

  /** Pastes PlantUML or Mermaid text centered on the mouse, or else on the visible area, and selects the result. */
  function pasteAtPointer(text: string) {
    const rect = svgRef.current!.getBoundingClientRect()
    const at = toPoint(hover.current ?? { clientX: rect.left + rect.width / 2, clientY: rect.top + rect.height / 2 })
    void mutate(async (diagramId) => {
      const pasted = await api.paste(diagramId, text, at)
      onSelect([...pasted.shapeIds, ...pasted.edgeIds])
    })
  }

  /** Right-click on an edge selects it and opens its menu (Ctrl+click on macOS keeps toggling the selection). */
  function openEdgeMenu(e: ReactMouseEvent, edgeId: string) {
    if (e.ctrlKey) {
      return
    }
    e.preventDefault()
    e.stopPropagation()
    const rect = svgRef.current!.getBoundingClientRect()
    onSelect([edgeId])
    setMenu({ edgeId, position: { x: e.clientX - rect.left, y: e.clientY - rect.top } })
  }

  function updateEdgeFromMenu(edgeId: string, patch: Parameters<typeof api.updateEdge>[2]) {
    setMenu(null)
    void mutate((diagramId) => api.updateEdge(diagramId, edgeId, patch))
  }

  /** Grabs the end of the selected edge that is attached to a diamond. */
  function startCornerDrag(e: PointerEvent, edge: Edge, end: EdgeEnd) {
    e.stopPropagation()
    const shape = byId.get(endShapeId(edge, end))
    if (e.button === PRIMARY_BUTTON && shape) {
      setCornerDrag({ edgeId: edge.id, end, corner: nearestCorner(shape, toPoint(e)), moved: false })
    }
  }

  function moveCornerDrag(p: Point) {
    const edge = cornerDrag && findEdge(cornerDrag.edgeId)
    const shape = edge && byId.get(endShapeId(edge, cornerDrag.end))
    if (shape) {
      setCornerDrag({ ...cornerDrag, corner: nearestCorner(shape, p), moved: true })
    }
  }

  /** Releasing the dragged end fixes it to the nearest corner; leaving the whiteboard cancels. */
  async function endCornerDrag(commit: boolean) {
    if (!cornerDrag) {
      return
    }
    const edge = findEdge(cornerDrag.edgeId)
    const key = cornerKey(cornerDrag.end)
    if (commit && cornerDrag.moved && edge && edge[key] !== cornerDrag.corner) {
      // Keep showing the dragged corner until the server state has been reloaded.
      await mutate((diagramId) => api.updateEdge(diagramId, edge.id, { [key]: cornerDrag.corner }))
    }
    setCornerDrag(null)
  }

  /** Finishes dragging an edge: releasing on another shape connects it, anywhere else cancels. */
  function endConnect(p: Point | null) {
    if (!edgeSourceId) {
      return
    }
    const sourceId = edgeSourceId
    setEdgeSourceId(null)
    setPointer(null)
    const target = p && topmostShapeAt(ordered, p)
    if (target && target.id !== sourceId) {
      void mutate((diagramId) => api.addEdge(diagramId, sourceId, target.id, routing))
    }
  }

  /** The shapes a move carries along: the selected ones, the endpoints of selected edges and the content of these groups. */
  function shapesToMove(ids: string[]) {
    const endpoints = diagram.edges.filter((edge) => ids.includes(edge.id)).flatMap((edge) => [edge.sourceId, edge.targetId])
    const moved = withContent(shapes, [...ids.filter((id) => byId.has(id)), ...endpoints])
    return shapes.filter((s) => moved.has(s.id))
  }

  /** Pressing on a shape or edge: starts an edge, places a shape, toggles the selection, or starts moving or resizing. */
  function onObjectPointerDown(e: PointerEvent, id: string, kind: ShapeDrag['kind']) {
    e.stopPropagation()
    if (e.button !== PRIMARY_BUTTON) {
      return
    }
    if (isConnecting) {
      if (byId.has(id)) {
        setEdgeSourceId(id)
        setPointer(toPoint(e))
      }
      return
    }
    if (placing) {
      placeShape(e, placing)
      return
    }
    if (kind === 'move' && hasModifier(e)) {
      onSelect(toggled(selected, id))
      return
    }
    startShapeDrag(e, id, kind)
  }

  function startShapeDrag(e: PointerEvent, id: string, kind: ShapeDrag['kind']) {
    const ids = kind === 'move' && selected.includes(id) ? selected : [id]
    if (!selected.includes(id)) {
      onSelect([id])
    }
    const dragged = kind === 'resize' ? shapes.filter((s) => s.id === id) : shapesToMove(ids)
    const edges = diagram.edges.filter((edge) => ids.includes(edge.id))
    setDrag({ kind, copy: kind === 'move' && e.shiftKey, start: toPoint(e), target: id, originals: dragged, currents: dragged, edges, active: true })
  }

  function onPointerMove(e: PointerEvent) {
    hover.current = { clientX: e.clientX, clientY: e.clientY }
    const p = toPoint(e)
    if (edgeSource) {
      setPointer(p)
    }
    if (cornerDrag) {
      moveCornerDrag(p)
    }
    if (marquee) {
      setMarquee({ ...marquee, current: p })
    }
    if (drag?.active) {
      setDrag(draggedTo(drag, p))
    }
  }

  async function endShapeDrag() {
    if (!drag?.active) {
      return
    }
    if (!hasChanged(drag)) {
      // A plain click on one of several selected objects selects just that one.
      if (selected.length > 1 && selected.includes(drag.target)) {
        onSelect([drag.target])
      }
      setDrag(null)
      return
    }
    // Keep showing the dragged geometry until the server state has been reloaded.
    const pending = { ...drag, active: false }
    setDrag(pending)
    await mutate((diagramId) => (drag.copy ? copyAndSelect(diagramId, drag) : updateGeometry(diagramId, drag.currents)))
    // A drag started meanwhile (e.g. a quick resize after a move) must not be cancelled.
    setDrag((current) => (current === pending ? null : current))
  }

  async function copyAndSelect(diagramId: string, copyDrag: ShapeDrag) {
    const { copyIds, edgeIds } = await copyShapes(diagramId, copyDrag.currents, copyDrag.edges)
    // Endpoints copied only to carry an edge along weren't selected, so they don't become selected either.
    const copiedSelection = copyDrag.currents.filter((s) => selected.includes(s.id)).map((s) => copyIds.get(s.id)!)
    onSelect([...copiedSelection, ...edgeIds])
  }

  function onBackgroundPointerDown(e: PointerEvent) {
    if (e.target !== e.currentTarget || e.button !== PRIMARY_BUTTON) {
      return
    }
    setEdgeSourceId(null)
    if (placing) {
      placeShape(e, placing)
      return
    }
    if (tool !== 'select') {
      return
    }
    const additive = hasModifier(e)
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
    const inside = objectsInBox(shapes, diagram.edges, box)
    if (inside.length > 0) {
      onSelect(marquee.additive ? withAdded(selected, inside) : inside)
    }
  }

  function onPointerUp(e: PointerEvent) {
    const released = e.type === 'pointerup'
    endConnect(released ? toPoint(e) : null)
    void endCornerDrag(released)
    endMarquee()
    void endShapeDrag()
  }

  function onDragOver(e: DragEvent) {
    if (isShapeDrag(e)) {
      e.preventDefault()
      e.dataTransfer.dropEffect = 'copy'
    }
  }

  function onDrop(e: DragEvent) {
    if (isShapeDrag(e)) {
      e.preventDefault()
      placeShape(e, droppedShapeKind(e))
    }
  }

  /** Adds a shape centered on the given client position and selects it. */
  function placeShape(e: ClientPosition, kind: ShapeKind) {
    const p = toPoint(e)
    const { width, height } = DEFAULT_SIZE[kind]
    const x = Math.max(0, Math.round(p.x - width / 2))
    const y = Math.max(0, Math.round(p.y - height / 2))
    onToolDone()
    void mutate(async (diagramId) => {
      const shape = await api.addShape(diagramId, { kind, x, y, width, height })
      onSelect([shape.id])
    })
  }

  const stopEditing = () => setEditing(null)
  const hoveredShape = edgeSource && pointer ? topmostShapeAt(ordered, pointer) : undefined
  const dropTarget = hoveredShape?.id !== edgeSource?.id ? hoveredShape : undefined
  const gridSize = GRID_SPACING * view.zoom
  // The ends of the only selected edge that are attached to a diamond get a handle to move them to another corner.
  const cornerEdge = tool === 'select' && selected.length === 1 && selectedEdges.length === 1 ? withCornerDrag(selectedEdges[0], cornerDrag) : null
  const menuEdge = menu && findEdge(menu.edgeId)

  function shapeStateClasses(shape: Shape) {
    // The first selected shape is the align anchor.
    const isAnchor = selectedShapeIds.length > 1 && selectedShapeIds[0] === shape.id
    const isEdgeEnd = edgeSource?.id === shape.id || dropTarget?.id === shape.id
    return `${selected.includes(shape.id) ? 'selected' : ''} ${isAnchor ? 'anchor' : ''} ${isEdgeEnd ? 'source' : ''}`
  }

  function renderShape(shape: Shape) {
    return (
      <ShapeView
        key={shape.id}
        shape={shape}
        stateClasses={shapeStateClasses(shape)}
        isEditing={editing?.kind === 'shape' && editing.id === shape.id}
        showResizeHandle={tool === 'select' && selected.length === 1 && selected[0] === shape.id}
        onMoveStart={(e) => onObjectPointerDown(e, shape.id, 'move')}
        onResizeStart={(e) => onObjectPointerDown(e, shape.id, 'resize')}
        onEditStart={() => setEditing({ kind: 'shape', id: shape.id })}
        onLabelCommit={(label) => label.trim() && void mutate((diagramId) => api.updateShape(diagramId, shape.id, { label }))}
        onEditDone={stopEditing}
      />
    )
  }

  function renderEdge(edge: Edge) {
    const from = byId.get(edge.sourceId)
    const to = byId.get(edge.targetId)
    if (!from || !to) {
      return null
    }
    return (
      <EdgeView
        key={edge.id}
        edge={edge}
        path={edgePath(from, to, withCornerDrag(edge, cornerDrag))}
        isSelected={selectedEdges.includes(edge)}
        isEditing={editing?.kind === 'edge' && editing.id === edge.id}
        onPointerDown={(e) => onObjectPointerDown(e, edge.id, 'move')}
        onEditStart={() => setEditing({ kind: 'edge', id: edge.id })}
        onContextMenu={(e) => openEdgeMenu(e, edge.id)}
        onLabelCommit={(label) => void mutate((diagramId) => api.updateEdge(diagramId, edge.id, { label }))}
        onEditDone={stopEditing}
      />
    )
  }

  function renderCornerHandles(edge: Edge) {
    const from = byId.get(edge.sourceId)
    const to = byId.get(edge.targetId)
    if (!from || !to) {
      return null
    }
    return <CornerHandles edge={edge} from={from} to={to} cornerDrag={cornerDrag} onDragStart={(e, end) => startCornerDrag(e, edge, end)} />
  }

  const canvasClasses = `canvas ${isConnecting ? 'connecting' : ''} ${placing ? 'placing' : ''} ${isPanning ? 'panning' : ''} ${cornerDrag ? 'cornering' : ''}`
  return (
    <div className={canvasClasses} {...panHandlers} onMouseDown={(e) => e.button === MIDDLE_BUTTON && e.preventDefault()}>
      <svg
        ref={svgRef}
        style={{ backgroundSize: `${gridSize}px ${gridSize}px`, backgroundPosition: `${view.x}px ${view.y}px` }}
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
        <ArrowMarkers />
        <g transform={`translate(${view.x} ${view.y}) scale(${view.zoom})`}>
          {ordered.filter(isGroup).map(renderShape)}
          {diagram.edges.map(renderEdge)}
          {ordered.filter((shape) => !isGroup(shape)).map(renderShape)}
          {cornerEdge && renderCornerHandles(cornerEdge)}
          {drag?.copy && <CopyGhosts drag={drag} />}
          {marquee && <rect className="marquee" {...boxOf(marquee.start, marquee.current)} />}
          {edgeSource && pointer && <polyline className="preview" points={toPoints(pathToPoint(edgeSource, pointer, routing))} />}
        </g>
      </svg>
      {menu && menuEdge && (
        <EdgeContextMenu
          edge={menuEdge}
          position={menu.position}
          onRoutingChange={(newRouting) => updateEdgeFromMenu(menu.edgeId, { routing: newRouting })}
          onReleaseCorners={() => updateEdgeFromMenu(menu.edgeId, { sourceCorner: 'auto', targetCorner: 'auto' })}
          onClose={closeMenu}
        />
      )}
    </div>
  )
}
