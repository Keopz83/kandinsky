import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { Canvas } from './Canvas'
import { MenuBar } from './MenuBar'
import { canPickSaveLocation, detectFormat, download, fileName, formatOf, formats, nameOf, openWritable, pickOpenFile, pickSaveTarget, writeText, type SaveTarget } from './saveFile'
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
  // File per whiteboard chosen by Save / Save As or opened from, so a later Save overwrites it without asking
  // as long as the whiteboard keeps the name it had then.
  const saveTargets = useRef(new Map<string, { target: SaveTarget; format: Format; name: string }>())
  // Text and name last saved to or opened from a file per whiteboard; a whiteboard still matching both is saved.
  const savedTexts = useRef(new Map<string, { format: Format; text: string; name: string }>())
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

  const remember = useCallback(async (id: string, format: Format, name: string) => {
    const text = await api.exportText(id, format)
    savedTexts.current.set(id, { format, text, name })
    return text
  }, [])

  const isSaved = useCallback(async ({ id, name }: DiagramSummary) => {
    const saved = savedTexts.current.get(id)
    return saved?.name === name && (await api.exportText(id, saved.format)) === saved.text
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

  function onRename(id: string, name: string) {
    void run(() => rename(id, '', name))
  }

  function onClose(target: DiagramSummary) {
    const index = diagrams.findIndex((d) => d.id === target.id)
    const neighbor = diagrams[index + 1] ?? diagrams[index - 1]
    void run(async () => {
      // Saved whiteboards can be reopened from their file, so only unsaved ones need a confirmation.
      if (!(await isSaved(target)) && !window.confirm(`Delete whiteboard "${target.name}"? This cannot be undone.`)) {
        return
      }
      // Switch away first, so a concurrent poll doesn't see the open whiteboard vanish.
      if (target.id === diagramId) {
        await show(neighbor ? await api.get(neighbor.id) : null)
      }
      await api.remove(target.id)
      saveTargets.current.delete(target.id)
      savedTexts.current.delete(target.id)
      await refreshTabs()
      setMessage(`Deleted ${target.name}`)
    })
  }

  const onNew = () => void run(async () => show(await api.create('Untitled')), 'Whiteboard created')

  function onSave(askForLocation: boolean) {
    if (!diagram) {
      return
    }
    const { id, name } = diagram
    void run(async () => {
      if (!canPickSaveLocation()) {
        // Without the File System Access API the browser decides the location (or asks, depending on its settings).
        const file = fileName(name, saveFormat)
        download(file, await remember(id, saveFormat, name))
        setMessage(`Downloaded ${file}`)
        return
      }
      // The picker must open before any other await, while the click still counts as user activation.
      const previous = askForLocation ? undefined : saveTargets.current.get(id)
      // A renamed whiteboard is saved to a new file named after it, keeping the old file.
      const known = previous?.name === name ? previous : undefined
      const target = known?.target ?? (await pickSaveTarget(name, saveFormat))
      if (!target) {
        return
      }
      const format = known?.format ?? formatOf(target.name, saveFormat)
      const writable = await openWritable(target)
      // Files don't store the name: a file name chosen in the dialog becomes the whiteboard's name.
      const adoptName = async () => (known ? name : rename(id, name, nameOf(target.name)))
      if (!writable) {
        saveTargets.current.delete(id)
        download(target.name, await remember(id, format, await adoptName()))
        setMessage(`The browser does not allow writing to ${target.name} here; downloaded it instead`)
        return
      }
      let text = ''
      await writeText(writable, async () => (text = await api.exportText(id, format)))
      const savedName = await adoptName()
      savedTexts.current.set(id, { format, text, name: savedName })
      saveTargets.current.set(id, { target, format, name: savedName })
      setMessage(`Saved ${target.name} (${formats[format].label})`)
    })
  }

  // Ctrl/Cmd+S saves, Ctrl/Cmd+Shift+S saves as; the ref keeps the listener on the latest diagram.
  const saveShortcut = useRef(onSave)
  saveShortcut.current = onSave
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (!(e.ctrlKey || e.metaKey) || e.altKey || e.key.toLowerCase() !== 's') {
        return
      }
      e.preventDefault()
      if (!e.repeat) {
        saveShortcut.current(e.shiftKey)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  function onOpen(chosen?: File) {
    void run(async () => {
      const picked = chosen ? { file: chosen, target: undefined } : await pickOpenFile()
      if (!picked) {
        return
      }
      const text = await picked.file.text()
      // The whiteboard is named after the file, even if the file has a title.
      const opened = await api.importText(text, nameOf(picked.file.name) || undefined)
      const format = formatOf(picked.file.name, detectFormat(text))
      if (picked.target) {
        // Save writes back to the opened file, in the format it was written in.
        saveTargets.current.set(opened.id, { target: picked.target, format, name: opened.name })
      }
      // The file's own text may be formatted differently, so the saved state is what the server exports for it.
      await remember(opened.id, format, opened.name)
      await show(opened)
      setMessage(`Opened ${picked.file.name}`)
    })
  }

  function onSaveFormatChange(format: Format) {
    localStorage.setItem(SAVE_FORMAT_KEY, format)
    setSaveFormat(format)
    setMessage(`Default save format: ${formats[format].label}`)
  }

  return (
    <div className="app d-flex flex-column">
      <MenuBar
        hasDiagram={diagram !== null}
        saveFormat={saveFormat}
        onNew={onNew}
        onSave={() => onSave(false)}
        onSaveAs={() => onSave(true)}
        onOpen={onOpen}
        onSaveFormatChange={onSaveFormatChange}
      />
      <div className="workspace d-flex">
        <Sidebar
          hasDiagram={diagram !== null}
          tool={tool}
          onAddRectangle={() => void mutate((id) => api.addShape(id, {}))}
          onToolChange={setTool}
          selectedShapeCount={selectedShapes.length}
          onAlign={(direction: AlignDirection) => void mutate((id) => api.align(id, selectedShapes, direction))}
          onAutoLayout={() => void mutate((id) => api.layout(id))}
        />
        <main className="main d-flex flex-column">
          <Tabs diagrams={diagrams} currentId={diagramId} onSelect={(id) => void open(id)} onRename={onRename} onClose={onClose} onNew={onNew} />
          <header className="toolbar border-bottom px-3 py-2">
            <span className="hint small text-body-secondary">
              {tool === 'edge'
                ? 'Click the source rectangle, then the target. Repeat for more edges. Esc returns to selection.'
                : 'Drag to move · Shift+drag to copy · Ctrl/Cmd+click or drag a box to select rectangles and edges · double-click to rename · Delete removes the selection · wheel or pinch to zoom · drag with the wheel pressed or two fingers on the trackpad to pan · Ctrl/Cmd+S saves'}
            </span>
          </header>
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
