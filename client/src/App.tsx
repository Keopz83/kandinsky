import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { Canvas } from './Canvas'
import { MenuBar } from './MenuBar'
import { canPickSaveLocation, detectFormat, download, fileName, formatOf, formats, openWritable, pickOpenFile, pickSaveTarget, writeText, type SaveTarget } from './saveFile'
import { Sidebar } from './Sidebar'
import { Tabs } from './Tabs'
import type { AlignDirection, Diagram, DiagramSummary, Format, Tool } from './types'

const POLL_INTERVAL_MS = 2000
const SAVE_FORMAT_KEY = 'kandinsky.saveFormat'
// Mermaid is the most widely used diagrams-as-code format (rendered natively by GitHub, GitLab, Azure DevOps, Notion, …).
const DEFAULT_SAVE_FORMAT: Format = 'mermaid'

function storedSaveFormat(): Format {
  const stored = localStorage.getItem(SAVE_FORMAT_KEY)
  return stored === 'plantuml' || stored === 'mermaid' ? stored : DEFAULT_SAVE_FORMAT
}

// The server sorts by name; tabs keep their position and new whiteboards are appended.
function keepOrder(previous: DiagramSummary[], list: DiagramSummary[]) {
  const byId = new Map(list.map((d) => [d.id, d]))
  const kept = previous.flatMap((d) => byId.get(d.id) ?? [])
  const known = new Set(kept.map((d) => d.id))
  return [...kept, ...list.filter((d) => !known.has(d.id))]
}

function clearHash() {
  history.replaceState(null, '', window.location.pathname + window.location.search)
}

export function App() {
  const [diagram, setDiagram] = useState<Diagram | null>(null)
  const [diagrams, setDiagrams] = useState<DiagramSummary[]>([])
  const [tool, setTool] = useState<Tool>('select')
  const [selection, setSelection] = useState<string[]>([])
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [saveFormat, setSaveFormat] = useState<Format>(storedSaveFormat)
  // File per whiteboard chosen by Save / Save As or imported from, so a later Save overwrites it without asking.
  const saveTargets = useRef(new Map<string, { target: SaveTarget; format: Format }>())
  // Only the most recently requested diagram state may be shown, so late poll responses can't undo a change.
  const latestRequest = useRef(0)
  // Same for the whiteboard list, so a late poll can't drop a just created tab.
  const latestList = useRef(0)
  const tabs = useRef<DiagramSummary[]>([])
  // StrictMode runs effects twice in development; the startup diagram must be created only once.
  const started = useRef(false)
  const diagramId = diagram?.id
  // Shapes and edges may disappear through other clients; ignore those ids.
  const selected = selection.filter((id) => diagram?.shapes.some((s) => s.id === id) || diagram?.edges.some((e) => e.id === id))
  const selectedShapes = selected.filter((id) => diagram?.shapes.some((s) => s.id === id))

  const run = useCallback(async (action: () => Promise<unknown>, success?: string) => {
    setError('')
    try {
      await action()
      if (success) {
        setMessage(success)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }, [])

  const load = useCallback(async (id: string) => {
    const request = ++latestRequest.current
    const loaded = await api.get(id)
    if (request === latestRequest.current) {
      setDiagram(loaded)
    }
  }, [])

  const refreshTabs = useCallback(async () => {
    const request = ++latestList.current
    const list = await api.list()
    if (request !== latestList.current) {
      return null
    }
    tabs.current = keepOrder(tabs.current, list)
    setDiagrams(tabs.current)
    return tabs.current
  }, [])

  const show = useCallback(
    async (next: Diagram | null) => {
      latestRequest.current++
      latestList.current++
      setDiagram(next)
      setTool('select')
      setSelection([])
      if (next) {
        window.location.hash = next.id
      } else {
        clearHash()
      }
      await refreshTabs()
    },
    [refreshTabs],
  )

  const open = useCallback(
    (id: string | undefined) => run(async () => show(id ? await api.get(id) : null)),
    [run, show],
  )

  const mutate = useCallback(
    async (action: (id: string) => Promise<unknown>) => {
      if (diagramId) {
        await run(async () => {
          await action(diagramId)
          await load(diagramId)
        })
      }
    },
    [diagramId, run, load],
  )

  const selectTool = useCallback(() => setTool('select'), [])

  useEffect(() => {
    if (started.current) {
      return
    }
    started.current = true
    void run(async () => {
      // A diagram linked in the URL (e.g. after a reload) is reopened; otherwise the app starts with a blank one.
      const id = decodeURIComponent(window.location.hash.slice(1))
      const linked = id ? await api.get(id).catch(() => null) : null
      await show(linked ?? (await api.create('Untitled')))
    })
  }, [run, show])

  useEffect(() => {
    const timer = setInterval(() => {
      if (diagramId) {
        load(diagramId).catch(() => undefined)
      }
      refreshTabs()
        .then((list) => {
          // The open whiteboard was deleted by another client.
          if (list && diagramId && !list.some((d) => d.id === diagramId)) {
            void open(list[0]?.id)
          }
        })
        .catch(() => undefined)
    }, POLL_INTERVAL_MS)
    return () => clearInterval(timer)
  }, [diagramId, load, refreshTabs, open])

  function onRename(id: string, name: string) {
    void run(async () => {
      await api.rename(id, name)
      await refreshTabs()
      if (id === diagramId) {
        await load(id)
      }
    })
  }

  function onClose(target: DiagramSummary) {
    if (!window.confirm(`Delete whiteboard "${target.name}"? This cannot be undone.`)) {
      return
    }
    const index = diagrams.findIndex((d) => d.id === target.id)
    const neighbor = diagrams[index + 1] ?? diagrams[index - 1]
    void run(async () => {
      // Switch away first, so a concurrent poll doesn't see the open whiteboard vanish.
      if (target.id === diagramId) {
        await show(neighbor ? await api.get(neighbor.id) : null)
      }
      await api.remove(target.id)
      await refreshTabs()
    }, `Deleted ${target.name}`)
  }

  const onNew = () => void run(async () => show(await api.create('Untitled')), 'Whiteboard created')

  function onExport(format: Format) {
    if (!diagram) {
      return
    }
    void run(async () => {
      download(fileName(diagram.name, format), await api.exportText(diagram.id, format))
    }, `Exported as ${formats[format].label}`)
  }

  function onSave(askForLocation: boolean) {
    if (!diagram) {
      return
    }
    const { id, name } = diagram
    void run(async () => {
      if (!canPickSaveLocation()) {
        // Without the File System Access API the browser decides the location (or asks, depending on its settings).
        const file = fileName(name, saveFormat)
        download(file, await api.exportText(id, saveFormat))
        setMessage(`Downloaded ${file}`)
        return
      }
      // The picker must open before any other await, while the click still counts as user activation.
      const known = askForLocation ? undefined : saveTargets.current.get(id)
      const target = known?.target ?? (await pickSaveTarget(name, saveFormat))
      if (!target) {
        return
      }
      const format = known?.format ?? formatOf(target.name, saveFormat)
      const writable = await openWritable(target)
      if (!writable) {
        saveTargets.current.delete(id)
        download(target.name, await api.exportText(id, format))
        setMessage(`The browser does not allow writing to ${target.name} here; downloaded it instead`)
        return
      }
      await writeText(writable, () => api.exportText(id, format))
      saveTargets.current.set(id, { target, format })
      setMessage(`Saved ${target.name} (${formats[format].label})`)
    })
  }

  function onImport(chosen?: File) {
    void run(async () => {
      const picked = chosen ? { file: chosen, target: undefined } : await pickOpenFile()
      if (!picked) {
        return
      }
      const text = await picked.file.text()
      const imported = await api.importText(text)
      if (picked.target) {
        // Save writes back to the imported file, in the format it was written in.
        saveTargets.current.set(imported.id, { target: picked.target, format: formatOf(picked.file.name, detectFormat(text)) })
      }
      await show(imported)
      setMessage(`Imported ${picked.file.name}`)
    })
  }

  function onSaveFormatChange(format: Format) {
    localStorage.setItem(SAVE_FORMAT_KEY, format)
    setSaveFormat(format)
    setMessage(`Default save format: ${formats[format].label}`)
  }

  return (
    <div className="app">
      <MenuBar
        hasDiagram={diagram !== null}
        saveFormat={saveFormat}
        onNew={onNew}
        onSave={() => onSave(false)}
        onSaveAs={() => onSave(true)}
        onImport={onImport}
        onExport={onExport}
        onSaveFormatChange={onSaveFormatChange}
      />
      <div className="workspace">
        <Sidebar
          hasDiagram={diagram !== null}
          tool={tool}
          onAddRectangle={() => void mutate((id) => api.addShape(id, {}))}
          onToolChange={setTool}
          selectedShapeCount={selectedShapes.length}
          onAlign={(direction: AlignDirection) => void mutate((id) => api.align(id, selectedShapes, direction))}
          onAutoLayout={() => void mutate((id) => api.layout(id))}
        />
        <main className="main">
          <Tabs diagrams={diagrams} currentId={diagramId} onSelect={(id) => void open(id)} onRename={onRename} onClose={onClose} onNew={onNew} />
          <header className="toolbar">
            <span className="hint">
              {tool === 'edge'
                ? 'Click the source rectangle, then the target. Repeat for more edges. Esc returns to selection.'
                : 'Drag to move · Shift+drag to copy · Ctrl/Cmd+click or drag a box to select rectangles and edges · double-click to rename · Delete removes the selection · wheel to zoom · drag with the wheel pressed to pan'}
            </span>
          </header>
          {diagram ? (
            <Canvas diagram={diagram} tool={tool} selected={selected} onSelect={setSelection} onToolDone={selectTool} mutate={mutate} />
          ) : (
            <div className="empty">Create a new whiteboard or import a PlantUML / Mermaid file to get started.</div>
          )}
          <footer className={`status ${error ? 'error' : ''}`}>{error || message}</footer>
        </main>
      </div>
    </div>
  )
}
