import { useEffect, useRef, useState } from 'react'
import { canPickOpenFile, formats } from './saveFile'
import type { Format } from './types'

type Menu = 'file' | 'settings'

const IS_MAC = /Mac|iPhone|iPad/.test(navigator.platform)
const shortcut = (key: string, shift = false) => (IS_MAC ? `${shift ? '⇧' : ''}⌘${key}` : `Ctrl+${shift ? 'Shift+' : ''}${key}`)

interface Props {
  hasDiagram: boolean
  saveFormat: Format
  onNew: () => void
  onSave: () => void
  onSaveAs: () => void
  /** Called without a file when the browser offers a file picker that App opens itself. */
  onImport: (file?: File) => void
  onExport: (format: Format) => void
  onSaveFormatChange: (format: Format) => void
}

export function MenuBar(props: Props) {
  const [open, setOpen] = useState<Menu | null>(null)
  const menus = useRef<HTMLDivElement>(null)
  const fileInput = useRef<HTMLInputElement>(null)
  const hasDiagram = props.hasDiagram

  useEffect(() => {
    if (!open) {
      return
    }
    const onPointerDown = (e: PointerEvent) => {
      if (!menus.current?.contains(e.target as Node)) {
        setOpen(null)
      }
    }
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setOpen(null)
      }
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  const item = (label: string, action: () => void, disabled = false, keys?: string) => (
    <button
      role="menuitem"
      disabled={disabled}
      onClick={() => {
        setOpen(null)
        action()
      }}
    >
      {label}
      {keys && <kbd className="shortcut">{keys}</kbd>}
    </button>
  )

  const trigger = (menu: Menu, label: string) => (
    <button
      className={`menubar-trigger ${open === menu ? 'open' : ''}`}
      aria-haspopup="menu"
      aria-expanded={open === menu}
      onClick={() => setOpen(open === menu ? null : menu)}
      // Moving the pointer to another trigger while a menu is open switches menus, like desktop menu bars.
      onPointerEnter={() => open && open !== menu && setOpen(menu)}
    >
      {label}
    </button>
  )

  return (
    <header className="menubar">
      <h1>Kandinsky</h1>
      <div className="menubar-menus" ref={menus}>
        <div className="menubar-menu">
          {trigger('file', 'File')}
          {open === 'file' && (
            <div className="dropdown" role="menu">
              {item('New', props.onNew)}
              {item('Save', props.onSave, !hasDiagram, shortcut('S'))}
              {item('Save As…', props.onSaveAs, !hasDiagram, shortcut('S', true))}
              <hr />
              {item('Import…', () => (canPickOpenFile() ? props.onImport() : fileInput.current?.click()))}
              <hr />
              {item('Export as PlantUML', () => props.onExport('plantuml'), !hasDiagram)}
              {item('Export as Mermaid', () => props.onExport('mermaid'), !hasDiagram)}
            </div>
          )}
        </div>
        <div className="menubar-menu">
          {trigger('settings', 'Settings')}
          {open === 'settings' && (
            <div className="dropdown" role="menu">
              <div className="dropdown-heading">Default save format</div>
              {(Object.keys(formats) as Format[]).map((format) => (
                <button
                  key={format}
                  role="menuitemradio"
                  aria-checked={props.saveFormat === format}
                  onClick={() => {
                    setOpen(null)
                    props.onSaveFormatChange(format)
                  }}
                >
                  <span className="check">{props.saveFormat === format ? '✓' : ''}</span>
                  {formats[format].label}
                </button>
              ))}
            </div>
          )}
        </div>
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
