import type { Format } from '../types'
import { OPENABLE_EXTENSIONS, fileName, formats, imageFileName, images, type ImageFormat } from './fileFormats'

// Minimal typing for the File System Access API (Chromium only, not yet in lib.dom).
interface WritableFile {
  write(data: string | Blob): Promise<void>
  close(): Promise<void>
  abort?(): Promise<void>
}

export interface SaveTarget {
  name: string
  createWritable(): Promise<WritableFile>
}

interface FileTypeFilter {
  description: string
  accept: Record<string, string[]>
}

interface SavePickerOptions {
  suggestedName?: string
  types?: FileTypeFilter[]
}

interface OpenedFile extends SaveTarget {
  getFile(): Promise<File>
}

declare global {
  interface Window {
    showSaveFilePicker?: (options?: SavePickerOptions) => Promise<SaveTarget>
    showOpenFilePicker?: (options?: { types?: FileTypeFilter[] }) => Promise<OpenedFile[]>
  }
}

export const canPickSaveLocation = () => typeof window.showSaveFilePicker === 'function'

export const canPickOpenFile = () => typeof window.showOpenFilePicker === 'function'

const isDomError = (e: unknown, ...names: string[]) => e instanceof DOMException && names.includes(e.name)

/** Runs a file picker; null if the user cancels it. */
async function unlessCancelled<T>(picker: () => Promise<T>): Promise<T | null> {
  try {
    return await picker()
  } catch (e) {
    if (isDomError(e, 'AbortError')) {
      return null
    }
    throw e
  }
}

/** Opens the save dialog with the default format offered first. Returns null if the user cancels. */
export function pickSaveTarget(name: string, defaultFormat: Format): Promise<SaveTarget | null> {
  const order: Format[] = defaultFormat === 'plantuml' ? ['plantuml', 'mermaid'] : ['mermaid', 'plantuml']
  const types = order.map((format) => ({ description: formats[format].label, accept: { 'text/plain': formats[format].extensions } }))
  return unlessCancelled(() => window.showSaveFilePicker!({ suggestedName: fileName(name, defaultFormat), types }))
}

/** Opens the save dialog for an image export. Returns null if the user cancels. */
export function pickImageTarget(name: string, format: ImageFormat): Promise<SaveTarget | null> {
  const image = images[format]
  const types = [{ description: image.label, accept: { [image.mime]: [image.extension] } }]
  return unlessCancelled(() => window.showSaveFilePicker!({ suggestedName: imageFileName(name, format), types }))
}

/** Opens the import dialog; the returned target lets Save overwrite the imported file. Returns null if the user cancels. */
export function pickOpenFile(): Promise<{ file: File; target: SaveTarget } | null> {
  return unlessCancelled(async () => {
    const [handle] = await window.showOpenFilePicker!({ types: [{ description: 'PlantUML / Mermaid', accept: { 'text/plain': OPENABLE_EXTENSIONS } }] })
    return { file: await handle.getFile(), target: handle }
  })
}

/**
 * Opens the file for writing. Must be called right after the user's click or the picker, before any other await:
 * the browser may need user activation to grant write access. Returns null if the browser refuses.
 */
export async function openWritable(target: SaveTarget): Promise<WritableFile | null> {
  try {
    return await target.createWritable()
  } catch (e) {
    if (isDomError(e, 'NotAllowedError', 'SecurityError')) {
      return null
    }
    throw e
  }
}

/** Writes the text (or image) into the opened file; on failure the file keeps its previous content. */
export async function writeContent(writable: WritableFile, content: () => Promise<string | Blob>) {
  try {
    await writable.write(await content())
    await writable.close()
  } catch (e) {
    await writable.abort?.().catch(() => undefined)
    throw e
  }
}

/** Lets the browser download the data as a file (to its download folder, or asking, depending on its settings). */
export function download(file: string, data: string | Blob) {
  const url = URL.createObjectURL(typeof data === 'string' ? new Blob([data], { type: 'text/plain' }) : data)
  const link = document.createElement('a')
  link.href = url
  link.download = file
  link.click()
  setTimeout(() => URL.revokeObjectURL(url))
}
