import type { Format } from './types'

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

interface SavePickerOptions {
  suggestedName?: string
  types?: { description: string; accept: Record<string, string[]> }[]
}

interface OpenedFile extends SaveTarget {
  getFile(): Promise<File>
}

interface OpenPickerOptions {
  types?: { description: string; accept: Record<string, string[]> }[]
}

declare global {
  interface Window {
    showSaveFilePicker?: (options?: SavePickerOptions) => Promise<SaveTarget>
    showOpenFilePicker?: (options?: OpenPickerOptions) => Promise<OpenedFile[]>
  }
}

export const formats: Record<Format, { label: string; extensions: string[] }> = {
  plantuml: { label: 'PlantUML', extensions: ['.puml', '.plantuml', '.pu', '.iuml'] },
  mermaid: { label: 'Mermaid', extensions: ['.mmd', '.mermaid'] },
}

export const images = {
  svg: { label: 'SVG image', mime: 'image/svg+xml', extension: '.svg' },
  png: { label: 'PNG image', mime: 'image/png', extension: '.png' },
} as const

export type ImageFormat = keyof typeof images

const safeName = (name: string) => name.replace(/[^\w.-]+/g, '_')

export const fileName = (name: string, format: Format) => `${safeName(name)}${formats[format].extensions[0]}`

export const imageFileName = (name: string, format: ImageFormat) => `${safeName(name)}${images[format].extension}`

const MAX_NAME_LENGTH = 200

/** Diagram name for a file: "shop.v2.mmd" → "shop.v2". */
export const nameOf = (file: string) => file.replace(/\.[^.]*$/, '').trim().slice(0, MAX_NAME_LENGTH)

export const canPickSaveLocation = () => typeof window.showSaveFilePicker === 'function'

export const canPickOpenFile = () => typeof window.showOpenFilePicker === 'function'

/** Same rule as the server: PlantUML starts with @startuml, everything else is treated as Mermaid. */
export function detectFormat(text: string): Format {
  const firstLine = text
    .split('\n')
    .map((l) => l.trim())
    .find((l) => l.length > 0 && !l.startsWith("'"))
  return firstLine?.startsWith('@startuml') ? 'plantuml' : 'mermaid'
}

export function formatOf(file: string, fallback: Format): Format {
  const lower = file.toLowerCase()
  return (Object.keys(formats) as Format[]).find((f) => formats[f].extensions.some((e) => lower.endsWith(e))) ?? fallback
}

async function showSavePicker(options: SavePickerOptions): Promise<SaveTarget | null> {
  try {
    return await window.showSaveFilePicker!(options)
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') {
      return null
    }
    throw e
  }
}

/** Opens the save dialog with the default format offered first. Returns null if the user cancels. */
export function pickSaveTarget(name: string, defaultFormat: Format): Promise<SaveTarget | null> {
  const order: Format[] = defaultFormat === 'plantuml' ? ['plantuml', 'mermaid'] : ['mermaid', 'plantuml']
  return showSavePicker({
    suggestedName: fileName(name, defaultFormat),
    types: order.map((f) => ({ description: formats[f].label, accept: { 'text/plain': formats[f].extensions } })),
  })
}

/** Opens the save dialog for an image export. Returns null if the user cancels. */
export function pickImageTarget(name: string, format: ImageFormat): Promise<SaveTarget | null> {
  const image = images[format]
  return showSavePicker({ suggestedName: imageFileName(name, format), types: [{ description: image.label, accept: { [image.mime]: [image.extension] } }] })
}

/** Opens the import dialog; the returned target lets Save overwrite the imported file. Returns null if the user cancels. */
export async function pickOpenFile(): Promise<{ file: File; target: SaveTarget } | null> {
  try {
    const [handle] = await window.showOpenFilePicker!({
      types: [{ description: 'PlantUML / Mermaid', accept: { 'text/plain': [...formats.plantuml.extensions, ...formats.mermaid.extensions, '.txt'] } }],
    })
    return { file: await handle.getFile(), target: handle }
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') {
      return null
    }
    throw e
  }
}

/**
 * Opens the file for writing. Must be called right after the user's click or the picker, before any other await:
 * the browser may need user activation to grant write access. Returns null if the browser refuses.
 */
export async function openWritable(target: SaveTarget): Promise<WritableFile | null> {
  try {
    return await target.createWritable()
  } catch (e) {
    if (e instanceof DOMException && (e.name === 'NotAllowedError' || e.name === 'SecurityError')) {
      return null
    }
    throw e
  }
}

/** Writes the text (or image) into the opened file; on failure the file keeps its previous content. */
export async function writeText(writable: WritableFile, text: () => Promise<string | Blob>) {
  try {
    await writable.write(await text())
    await writable.close()
  } catch (e) {
    await writable.abort?.().catch(() => undefined)
    throw e
  }
}

export function download(file: string, data: string | Blob) {
  const url = URL.createObjectURL(typeof data === 'string' ? new Blob([data], { type: 'text/plain' }) : data)
  const link = document.createElement('a')
  link.href = url
  link.download = file
  link.click()
  setTimeout(() => URL.revokeObjectURL(url))
}
