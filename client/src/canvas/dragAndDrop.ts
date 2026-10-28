import type { DragEvent } from 'react'
import { SHAPE_KINDS, type ShapeKind } from '../types'

/** Data type of a palette shape dragged onto the canvas. */
const SHAPE_MIME = 'application/x-kandinsky-shape'

export function startShapeDrag(e: DragEvent, kind: ShapeKind) {
  e.dataTransfer.setData(SHAPE_MIME, kind)
  e.dataTransfer.effectAllowed = 'copy'
}

export const isShapeDrag = (e: DragEvent) => e.dataTransfer.types.includes(SHAPE_MIME)

export function droppedShapeKind(e: DragEvent): ShapeKind {
  const kind = e.dataTransfer.getData(SHAPE_MIME) as ShapeKind
  return SHAPE_KINDS.includes(kind) ? kind : 'rectangle'
}
