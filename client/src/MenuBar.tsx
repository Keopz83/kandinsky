import { useEffect, useRef, useState } from 'react'
import type { Format } from './types'

interface Props {
  hasDiagram: boolean
  onNew: () => void
  onSave: () => void
  onImport: (file: File) => void
  onExport: (format: Format) => void
}

export function MenuBar(props: Props) {
  const [open, setOpen] = useState(false)
  const menu = useRef<HTMLDivElement>(null)
  const fileInput = useRef<HTMLInputElement>(null)
  const hasDiagram = props.hasDiagram

  useEffect(() => {
    if (!open) {
      return
    }
    const onPointerDown = (e: PointerEvent) => {
      if (!menu.current?.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setOpen(false)
      }
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  const item = (label: string, action: () => void, disabled = false) => (
    <button
      role="menuitem"
      disabled={disabled}
      onClick={() => {
        setOpen(false)
        action()
      }}
    >
      {label}
    </button>
  )

  return (
    <header className="menubar">
      <h1>Kandinsky</h1>
      <div className="menubar-menu" ref={menu}>
        <button className={`menubar-trigger ${open ? 'open' : ''}`} aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen(!open)}>
          File
        </button>
        {open && (
          <div className="dropdown" role="menu">
            {item('New', props.onNew)}
            {item('Save', props.onSave, !hasDiagram)}
            <hr />
            {item('Import…', () => fileInput.current?.click())}
            <hr />
            {item('Export as PlantUML', () => props.onExport('plantuml'), !hasDiagram)}
            {item('Export as Mermaid', () => props.onExport('mermaid'), !hasDiagram)}
          </div>
        )}
        <input
          ref={fileInput}
          type="file"
          hidden
          accept=".puml,.plantuml,.pu,.iuml,.mmd,.mermaid,.txt"
          onChange={(e) => {
            const file = e.target.files?.[0]
            e.target.value = ''
            if (file) {
              props.onImport(file)
            }
          }}
        />
      </div>
    </header>
  )
}
