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
  onOpen: (file?: File) => void
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
      type="button"
      className="dropdown-item d-flex justify-content-between align-items-center"
      role="menuitem"
      disabled={disabled}
      onClick={() => {
        setOpen(null)
        action()
      }}
    >
      {label}
      {keys && <span className="shortcut ms-4 small text-body-secondary">{keys}</span>}
    </button>
  )

  const trigger = (menu: Menu, label: string) => (
    <button
      type="button"
      className={`btn btn-sm menubar-trigger ${open === menu ? 'active' : ''}`}
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
    <header className="menubar navbar border-bottom px-3 py-1 justify-content-start">
      <h1 className="navbar-brand fs-6 fw-semibold m-0 me-2">Kandinsky</h1>
      <div className="menubar-menus d-flex gap-1" ref={menus}>
        <div className="dropdown">
          {trigger('file', 'File')}
          {open === 'file' && (
            <div className="dropdown-menu show shadow" role="menu">
              {item('New', props.onNew)}
              {item('Open…', () => (canPickOpenFile() ? props.onOpen() : fileInput.current?.click()))}
              <hr className="dropdown-divider" />
              {item('Save', props.onSave, !hasDiagram, shortcut('S'))}
              {item('Save As…', props.onSaveAs, !hasDiagram, shortcut('S', true))}
            </div>
          )}
        </div>
        <div className="dropdown">
          {trigger('settings', 'Settings')}
          {open === 'settings' && (
            <div className="dropdown-menu show shadow" role="menu">
              <h6 className="dropdown-header">Default save format</h6>
              {(Object.keys(formats) as Format[]).map((format) => (
                <button
                  key={format}
                  type="button"
                  className="dropdown-item"
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
              props.onOpen(file)
            }
          }}
        />
      </div>
    </header>
  )
}
