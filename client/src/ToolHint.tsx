import type { Tool } from './types'

const SELECT_HINT = [
  'Drag to move',
  'Shift+drag to copy',
  'Ctrl/Cmd+click or drag a box to select rectangles and edges',
  'double-click to rename',
  'right-click an edge to make it straight or angled',
  "drag the dot on a selected edge's diamond end to change the corner",
  'Delete removes the selection',
  'wheel or pinch to zoom',
  'drag with the wheel pressed or two fingers on the trackpad to pan',
  'Ctrl/Cmd+V pastes PlantUML / Mermaid',
  'Ctrl/Cmd+S saves',
].join(' · ')

function hintFor(tool: Tool) {
  switch (tool) {
    case 'select':
      return SELECT_HINT
    case 'edge':
    case 'angled-edge':
      return `Press on the source shape and drag onto the target to draw ${tool === 'edge' ? 'a straight' : 'an angled'} edge. Repeat for more edges. Esc returns to selection.`
    case 'group':
      return 'Click on the canvas to place a group. Shapes lying fully inside a group belong to it and move with it. Esc cancels.'
    default:
      return `Click on the canvas to place a ${tool}. Esc cancels.`
  }
}

/** The toolbar line explaining how to use the current tool. */
export function ToolHint({ tool }: { tool: Tool }) {
  return (
    <header className="toolbar border-bottom px-3 py-2">
      <span className="hint small text-body-secondary">{hintFor(tool)}</span>
    </header>
  )
}
