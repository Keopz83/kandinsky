import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { Canvas } from './Canvas'
import { LabelInput } from './LabelInput'
import { MenuBar } from './MenuBar'
import { Sidebar } from './Sidebar'
import { Tabs } from './Tabs'
import type { AlignDirection, Diagram, DiagramSummary, Format, Tool } from './types'

const POLL_INTERVAL_MS = 2000

function download(fileName: string, text: string) {
  const url = URL.createObjectURL(new Blob([text], { type: 'text/plain' }))
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  setTimeout(() => URL.revokeObjectURL(url))
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

  function onRename(name: string) {
    void mutate(async (id) => {
      await api.rename(id, name)
      await refreshTabs()
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
    const extension = format === 'plantuml' ? 'puml' : 'mmd'
    void run(async () => {
      download(`${diagram.name.replace(/[^\w.-]+/g, '_')}.${extension}`, await api.exportText(diagram.id, format))
    }, `Exported as ${format === 'plantuml' ? 'PlantUML' : 'Mermaid'}`)
  }

  return (
    <div className="app">
      <MenuBar
        hasDiagram={diagram !== null}
        onNew={onNew}
        onSave={() => diagramId && void run(() => api.save(diagramId), 'Saved')}
        onImport={(file) => void run(async () => show(await api.importText(await file.text())), `Imported ${file.name}`)}
        onExport={onExport}
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
          <Tabs diagrams={diagrams} currentId={diagramId} onSelect={(id) => void open(id)} onClose={onClose} onNew={onNew} />
          <header className="toolbar">
            {diagram ? (
              <LabelInput key={`${diagram.id}:${diagram.name}`} className="title" ariaLabel="Diagram name" value={diagram.name} onCommit={onRename} />
            ) : (
              <span className="title">No diagram open</span>
            )}
            <span className="hint">
              {tool === 'edge'
                ? 'Click the source rectangle, then the target. Repeat for more edges. Esc returns to selection.'
                : 'Drag to move · Shift+drag to copy · Ctrl/Cmd+click or drag a box to select rectangles and edges · double-click to rename · Delete removes the selection'}
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
