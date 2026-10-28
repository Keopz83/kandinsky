import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { Canvas } from './canvas/Canvas'
import { formats } from './files/fileFormats'
import { useSaveFormatSetting, useSaveShortcut } from './files/saveSettings'
import { useFileCommands } from './files/useFileCommands'
import { MenuBar } from './MenuBar'
import { Sidebar } from './Sidebar'
import { Tabs } from './Tabs'
import { ToolHint } from './ToolHint'
import type { AlignDirection, Diagram, DiagramSummary, Format, Tool } from './types'
import { useWhiteboardTabs } from './useWhiteboardTabs'

const POLL_INTERVAL_MS = 2000
const NEW_WHITEBOARD_NAME = 'Untitled'

function clearHash() {
  history.replaceState(null, '', window.location.pathname + window.location.search)
}

// Shapes and edges may disappear through other clients; their ids are ignored.
const existingIds = (diagram: Diagram | null, ids: string[]) =>
  ids.filter((id) => diagram?.shapes.some((s) => s.id === id) || diagram?.edges.some((e) => e.id === id))

export function App() {
  const [diagram, setDiagram] = useState<Diagram | null>(null)
  const [tool, setTool] = useState<Tool>('select')
  const [selection, setSelection] = useState<string[]>([])
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [saveFormat, changeSaveFormat] = useSaveFormatSetting()
  const { tabs, refreshTabs, discardPendingRefreshes } = useWhiteboardTabs()
  // Only the most recently requested diagram state may be shown, so late poll responses can't undo a change.
  const latestRequest = useRef(0)
  // StrictMode runs effects twice in development; the startup diagram must be created only once.
  const started = useRef(false)
  const diagramId = diagram?.id
  const selected = existingIds(diagram, selection)
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

  const show = useCallback(
    async (next: Diagram | null) => {
      latestRequest.current++
      discardPendingRefreshes()
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
    [refreshTabs, discardPendingRefreshes],
  )

  const open = useCallback((id: string | undefined) => run(async () => show(id ? await api.get(id) : null)), [run, show])

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
      await show(linked ?? (await api.create(NEW_WHITEBOARD_NAME)))
    })
  }, [run, show])

  // Changes by other clients (e.g. through the REST API) show up within the poll interval.
  useEffect(() => {
    const timer = setInterval(() => {
      if (diagramId) {
        load(diagramId).catch(() => undefined)
      }
      refreshTabs()
        .then((list) => {
          const openWasDeleted = list && diagramId && !list.some((d) => d.id === diagramId)
          if (openWasDeleted) {
            void open(list[0]?.id)
          }
        })
        .catch(() => undefined)
    }, POLL_INTERVAL_MS)
    return () => clearInterval(timer)
  }, [diagramId, load, refreshTabs, open])

  /** Renames the whiteboard if the name changed; returns its (new) name. */
  async function rename(id: string, current: string, next: string) {
    if (!next || next === current) {
      return current
    }
    const renamed = await api.rename(id, next)
    await refreshTabs()
    if (id === diagramId) {
      await load(id)
    }
    return renamed.name
  }

  const files = useFileCommands({ diagram, saveFormat, run, report: setMessage, rename, show })
  useSaveShortcut(files.save)

  function onRename(id: string, name: string) {
    void run(() => rename(id, '', name))
  }

  function onClose(target: DiagramSummary) {
    const index = tabs.findIndex((d) => d.id === target.id)
    const neighbor = tabs[index + 1] ?? tabs[index - 1]
    void run(async () => {
      // Saved whiteboards can be reopened from their file, so only unsaved ones need a confirmation.
      if (!(await files.isSaved(target)) && !window.confirm(`Delete whiteboard "${target.name}"? This cannot be undone.`)) {
        return
      }
      // Switch away first, so a concurrent poll doesn't see the open whiteboard vanish.
      if (target.id === diagramId) {
        await show(neighbor ? await api.get(neighbor.id) : null)
      }
      await api.remove(target.id)
      files.forget(target.id)
      await refreshTabs()
      setMessage(`Deleted ${target.name}`)
    })
  }

  const onNew = () => void run(async () => show(await api.create(NEW_WHITEBOARD_NAME)), 'Whiteboard created')

  function onSaveFormatChange(format: Format) {
    changeSaveFormat(format)
    setMessage(`Default save format: ${formats[format].label}`)
  }

  return (
    <div className="app d-flex flex-column">
      <MenuBar
        hasDiagram={diagram !== null}
        saveFormat={saveFormat}
        onNew={onNew}
        onSave={() => files.save(false)}
        onSaveAs={() => files.save(true)}
        onExport={files.exportImage}
        onOpen={files.open}
        onSaveFormatChange={onSaveFormatChange}
      />
      <div className="workspace d-flex">
        <Sidebar
          hasDiagram={diagram !== null}
          tool={tool}
          onToolChange={setTool}
          selectedShapeCount={selectedShapes.length}
          onAlign={(direction: AlignDirection) => void mutate((id) => api.align(id, selectedShapes, direction))}
          onAutoLayout={() => void mutate((id) => api.layout(id))}
        />
        <main className="main d-flex flex-column">
          <Tabs diagrams={tabs} currentId={diagramId} onSelect={(id) => void open(id)} onRename={onRename} onClose={onClose} onNew={onNew} />
          <ToolHint tool={tool} />
          {diagram ? (
            <Canvas diagram={diagram} tool={tool} selected={selected} onSelect={setSelection} onToolDone={selectTool} mutate={mutate} />
          ) : (
            <div className="empty d-grid text-body-secondary">Create a new whiteboard or open a PlantUML / Mermaid file to get started.</div>
          )}
          <footer className={`status border-top px-3 py-1 small ${error ? 'text-danger-emphasis bg-danger-subtle' : 'text-body-secondary'}`}>{error || message}</footer>
        </main>
      </div>
    </div>
  )
}
