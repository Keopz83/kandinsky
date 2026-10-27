import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { Canvas } from './Canvas'
import { LabelInput } from './LabelInput'
import { Sidebar } from './Sidebar'
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

export function App() {
  const [diagram, setDiagram] = useState<Diagram | null>(null)
  const [diagrams, setDiagrams] = useState<DiagramSummary[]>([])
  const [tool, setTool] = useState<Tool>('select')
  const [selection, setSelection] = useState<string[]>([])
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  // Only the most recently requested diagram state may be shown, so late poll responses can't undo a change.
  const latestRequest = useRef(0)
  const diagramId = diagram?.id
  // Shapes may disappear through other clients; ignore those ids.
  const selected = selection.filter((id) => diagram?.shapes.some((s) => s.id === id))

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

  const show = useCallback(async (next: Diagram) => {
    latestRequest.current++
    setDiagram(next)
    setTool('select')
    setSelection([])
    window.location.hash = next.id
    setDiagrams(await api.list())
  }, [])

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
    void run(async () => {
      setDiagrams(await api.list())
      const id = decodeURIComponent(window.location.hash.slice(1))
      if (id) {
        await load(id)
      }
    })
  }, [run, load])

  useEffect(() => {
    if (!diagramId) {
      return
    }
    const timer = setInterval(() => load(diagramId).catch(() => undefined), POLL_INTERVAL_MS)
    return () => clearInterval(timer)
  }, [diagramId, load])

  function onRename(name: string) {
    void mutate(async (id) => {
      await api.rename(id, name)
      setDiagrams(await api.list())
    })
  }

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
      <Sidebar
        diagrams={diagrams}
        currentId={diagramId}
        tool={tool}
        onNew={() => void run(async () => show(await api.create('Untitled')), 'Diagram created')}
        onOpen={(id) => void run(async () => show(await api.get(id)))}
        onSave={() => diagramId && void run(() => api.save(diagramId), 'Saved')}
        onImport={(file) => void run(async () => show(await api.importText(await file.text())), `Imported ${file.name}`)}
        onExport={onExport}
        onAddRectangle={() => void mutate((id) => api.addShape(id, {}))}
        onToolChange={setTool}
        selectedCount={selected.length}
        onAlign={(direction: AlignDirection) => void mutate((id) => api.align(id, selected, direction))}
        onAutoLayout={() => void mutate((id) => api.layout(id))}
      />
      <main className="main">
        <header className="toolbar">
          {diagram ? (
            <LabelInput key={`${diagram.id}:${diagram.name}`} className="title" ariaLabel="Diagram name" value={diagram.name} onCommit={onRename} />
          ) : (
            <span className="title">No diagram open</span>
          )}
          <span className="hint">
            {tool === 'edge'
              ? 'Click the source rectangle, then the target. Esc cancels.'
              : 'Drag to move · Shift+drag to copy · Ctrl/Cmd+click or drag a box to select several · double-click to rename · Delete removes the selection'}
          </span>
        </header>
        {diagram ? (
          <Canvas diagram={diagram} tool={tool} selected={selected} onSelect={setSelection} onToolDone={selectTool} mutate={mutate} />
        ) : (
          <div className="empty">Create a new diagram or import a PlantUML / Mermaid file to get started.</div>
        )}
        <footer className={`status ${error ? 'error' : ''}`}>{error || message}</footer>
      </main>
    </div>
  )
}
