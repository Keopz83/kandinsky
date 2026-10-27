import type { Format } from './types'

// Minimal typing for the File System Access API (Chromium only, not yet in lib.dom).
interface WritableFile {
  write(data: string): Promise<void>
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

declare global {
  interface Window {
    showSaveFilePicker?: (options?: SavePickerOptions) => Promise<SaveTarget>
  }
}

export const formats: Record<Format, { label: string; extensions: string[] }> = {
  plantuml: { label: 'PlantUML', extensions: ['.puml', '.plantuml', '.pu', '.iuml'] },
  mermaid: { label: 'Mermaid', extensions: ['.mmd', '.mermaid'] },
}

export const fileName = (name: string, format: Format) => `${name.replace(/[^\w.-]+/g, '_')}${formats[format].extensions[0]}`

export const canPickSaveLocation = () => typeof window.showSaveFilePicker === 'function'

export function formatOf(file: string, fallback: Format): Format {
  const lower = file.toLowerCase()
  return (Object.keys(formats) as Format[]).find((f) => formats[f].extensions.some((e) => lower.endsWith(e))) ?? fallback
}

/** Opens the save dialog with the default format offered first. Returns null if the user cancels. */
export async function pickSaveTarget(name: string, defaultFormat: Format): Promise<SaveTarget | null> {
  const order: Format[] = defaultFormat === 'plantuml' ? ['plantuml', 'mermaid'] : ['mermaid', 'plantuml']
  try {
    return await window.showSaveFilePicker!({
      suggestedName: fileName(name, defaultFormat),
      types: order.map((f) => ({ description: formats[f].label, accept: { 'text/plain': formats[f].extensions } })),
    })
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

/** Writes the text into the opened file; on failure the file keeps its previous content. */
export async function writeText(writable: WritableFile, text: () => Promise<string>) {
  try {
    await writable.write(await text())
    await writable.close()
  } catch (e) {
    await writable.abort?.().catch(() => undefined)
    throw e
  }
}

export function download(file: string, text: string) {
  const url = URL.createObjectURL(new Blob([text], { type: 'text/plain' }))
  const link = document.createElement('a')
  link.href = url
  link.download = file
  link.click()
  setTimeout(() => URL.revokeObjectURL(url))
}
