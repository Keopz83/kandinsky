import { useRef } from 'react'
import { api } from '../api'
import { diagramPng, diagramSvg } from '../export/diagramImage'
import type { Diagram, DiagramSummary, Format } from '../types'
import { detectFormat, fileName, formatOf, formats, imageFileName, images, nameOf, type ImageFormat } from './fileFormats'
import { canPickSaveLocation, download, openWritable, pickImageTarget, pickOpenFile, pickSaveTarget, writeContent, type SaveTarget } from './fileSystemAccess'

/** The file a whiteboard was saved to or opened from, and its name then: Save overwrites it while the name is unchanged. */
interface LinkedFile {
  target: SaveTarget
  format: Format
  name: string
}

/** Text and name last saved to or opened from a file; a whiteboard still matching both is saved. */
interface SavedState {
  format: Format
  text: string
  name: string
}

interface Options {
  diagram: Diagram | null
  saveFormat: Format
  run: (action: () => Promise<unknown>) => Promise<void>
  report: (message: string) => void
  /** Renames the whiteboard (if the name changed) and returns its new name. */
  rename: (id: string, current: string, next: string) => Promise<string>
  show: (diagram: Diagram) => Promise<void>
}

const writeRefusedMessage = (file: string) => `The browser does not allow writing to ${file} here; downloaded it instead`

/** File menu commands: Save / Save As, Open and image export, plus tracking which whiteboards are saved. */
export function useFileCommands({ diagram, saveFormat, run, report, rename, show }: Options) {
  const linkedFiles = useRef(new Map<string, LinkedFile>())
  const savedStates = useRef(new Map<string, SavedState>())

  async function remember(id: string, format: Format, name: string) {
    const text = await api.exportText(id, format)
    savedStates.current.set(id, { format, text, name })
    return text
  }

  async function isSaved({ id, name }: DiagramSummary) {
    const saved = savedStates.current.get(id)
    return saved?.name === name && (await api.exportText(id, saved.format)) === saved.text
  }

  function forget(id: string) {
    linkedFiles.current.delete(id)
    savedStates.current.delete(id)
  }

  /** Without the File System Access API the browser decides the location (or asks, depending on its settings). */
  async function downloadDiagram(id: string, name: string) {
    const file = fileName(name, saveFormat)
    download(file, await remember(id, saveFormat, name))
    report(`Downloaded ${file}`)
  }

  /** Writes the whiteboard into the chosen file; must be called right after picking it (see {@link openWritable}). */
  async function writeDiagram(id: string, name: string, target: SaveTarget, format: Format, isLinked: boolean) {
    const writable = await openWritable(target)
    // Files don't store the name: a file name chosen in the dialog becomes the whiteboard's name.
    const adoptName = async () => (isLinked ? name : rename(id, name, nameOf(target.name)))
    if (!writable) {
      linkedFiles.current.delete(id)
      download(target.name, await remember(id, format, await adoptName()))
      report(writeRefusedMessage(target.name))
      return
    }
    let text = ''
    await writeContent(writable, async () => (text = await api.exportText(id, format)))
    const savedName = await adoptName()
    savedStates.current.set(id, { format, text, name: savedName })
    linkedFiles.current.set(id, { target, format, name: savedName })
    report(`Saved ${target.name} (${formats[format].label})`)
  }

  function save(askForLocation: boolean) {
    if (!diagram) {
      return
    }
    const { id, name } = diagram
    void run(async () => {
      if (!canPickSaveLocation()) {
        await downloadDiagram(id, name)
        return
      }
      // The picker must open before any other await, while the click still counts as user activation.
      const previous = askForLocation ? undefined : linkedFiles.current.get(id)
      // A renamed whiteboard is saved to a new file named after it, keeping the old file.
      const linked = previous?.name === name ? previous : undefined
      const target = linked?.target ?? (await pickSaveTarget(name, saveFormat))
      if (target) {
        await writeDiagram(id, name, target, linked?.format ?? formatOf(target.name, saveFormat), !!linked)
      }
    })
  }

  /** Exports the whiteboard as an SVG or PNG image, like Save: to a file chosen in the dialog, else as a download. */
  function exportImage(format: ImageFormat) {
    if (!diagram) {
      return
    }
    const current = diagram
    void run(async () => {
      // Rendering the SVG is synchronous, so an empty whiteboard is reported before the dialog opens.
      const svg = diagramSvg(current)
      const render = async () => (format === 'svg' ? new Blob([svg.svg], { type: images.svg.mime }) : diagramPng(svg))
      if (!canPickSaveLocation()) {
        const file = imageFileName(current.name, format)
        download(file, await render())
        report(`Downloaded ${file}`)
        return
      }
      const target = await pickImageTarget(current.name, format)
      if (!target) {
        return
      }
      const writable = await openWritable(target)
      if (!writable) {
        download(target.name, await render())
        report(writeRefusedMessage(target.name))
        return
      }
      await writeContent(writable, render)
      report(`Exported ${target.name} (${images[format].label})`)
    })
  }

  /** Opens a PlantUML or Mermaid file as a new whiteboard, from the given file or else the browser's file picker. */
  function open(chosen?: File) {
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
        linkedFiles.current.set(opened.id, { target: picked.target, format, name: opened.name })
      }
      // The file's own text may be formatted differently, so the saved state is what the server exports for it.
      await remember(opened.id, format, opened.name)
      await show(opened)
      report(`Opened ${picked.file.name}`)
    })
  }

  return { save, exportImage, open, isSaved, forget }
}
