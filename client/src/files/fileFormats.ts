import type { Format } from '../types'

export const formats: Record<Format, { label: string; extensions: string[] }> = {
  plantuml: { label: 'PlantUML', extensions: ['.puml', '.plantuml', '.pu', '.iuml'] },
  mermaid: { label: 'Mermaid', extensions: ['.mmd', '.mermaid'] },
}

export const ALL_FORMATS = Object.keys(formats) as Format[]

/** Extensions offered when opening a diagram file; plain text files may contain either format. */
export const OPENABLE_EXTENSIONS = [...formats.plantuml.extensions, ...formats.mermaid.extensions, '.txt']

export const images = {
  svg: { label: 'SVG image', mime: 'image/svg+xml', extension: '.svg' },
  png: { label: 'PNG image', mime: 'image/png', extension: '.png' },
} as const

export type ImageFormat = keyof typeof images

const MAX_NAME_LENGTH = 200

const safeName = (name: string) => name.replace(/[^\w.-]+/g, '_')

export const fileName = (name: string, format: Format) => `${safeName(name)}${formats[format].extensions[0]}`

export const imageFileName = (name: string, format: ImageFormat) => `${safeName(name)}${images[format].extension}`

/** Diagram name for a file: "shop.v2.mmd" → "shop.v2". */
export const nameOf = (file: string) => file.replace(/\.[^.]*$/, '').trim().slice(0, MAX_NAME_LENGTH)

/** Same rule as the server: PlantUML starts with @startuml, everything else is treated as Mermaid. */
export function detectFormat(text: string): Format {
  const firstLine = text
    .split('\n')
    .map((line) => line.trim())
    .find((line) => line.length > 0 && !line.startsWith("'"))
  return firstLine?.startsWith('@startuml') ? 'plantuml' : 'mermaid'
}

/** The format a file name's extension stands for, else the fallback. */
export function formatOf(file: string, fallback: Format): Format {
  const lower = file.toLowerCase()
  return ALL_FORMATS.find((format) => formats[format].extensions.some((extension) => lower.endsWith(extension))) ?? fallback
}
