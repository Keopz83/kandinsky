import { useEffect, useRef } from 'react'
import type { Point } from '../geometry/point'
import type { Edge, EdgeRouting } from '../types'
import { hasFixedCorner } from './cornerDrag'

const ROUTING_LABELS: Record<EdgeRouting, string> = { straight: 'Straight edge', angled: 'Angled edge' }

interface Props {
  edge: Edge
  /** Position within the canvas. */
  position: Point
  onRoutingChange: (routing: EdgeRouting) => void
  onReleaseCorners: () => void
  onClose: () => void
}

/** The right-click menu of an edge. Closes on any press outside it and when the whiteboard is zoomed or panned. */
export function EdgeContextMenu({ edge, position, onRoutingChange, onReleaseCorners, onClose }: Props) {
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const closeOutside = (e: Event) => {
      if (!(e.target instanceof Node && menuRef.current?.contains(e.target))) {
        onClose()
      }
    }
    window.addEventListener('pointerdown', closeOutside, true)
    window.addEventListener('wheel', closeOutside, true)
    return () => {
      window.removeEventListener('pointerdown', closeOutside, true)
      window.removeEventListener('wheel', closeOutside, true)
    }
  }, [onClose])

  return (
    <div ref={menuRef} className="dropdown-menu show edge-menu" style={{ left: position.x, top: position.y }} role="menu">
      {(Object.keys(ROUTING_LABELS) as EdgeRouting[]).map((routing) => {
        const isCurrent = edge.routing === routing
        return (
          <button key={routing} type="button" role="menuitemradio" aria-checked={isCurrent} className="dropdown-item" onClick={() => onRoutingChange(routing)}>
            <span className="check">{isCurrent ? '✓' : ''}</span>
            {ROUTING_LABELS[routing]}
          </button>
        )
      })}
      {hasFixedCorner(edge) && (
        <>
          <hr className="dropdown-divider" />
          <button type="button" role="menuitem" className="dropdown-item" onClick={onReleaseCorners}>
            <span className="check" />
            Automatic diamond corners
          </button>
        </>
      )}
    </div>
  )
}
