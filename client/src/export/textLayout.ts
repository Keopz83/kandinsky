export const FONT_FAMILY = 'system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif'

export interface Font {
  size: number
  weight: number
}

/** Rough average character width relative to the font size, used when the browser cannot measure text. */
const FALLBACK_CHAR_WIDTH = 0.55
const ELLIPSIS = '…'

let measuringContext: CanvasRenderingContext2D | null = null

export function textWidth(text: string, font: Font) {
  measuringContext ??= document.createElement('canvas').getContext('2d')
  if (!measuringContext) {
    return text.length * font.size * FALLBACK_CHAR_WIDTH
  }
  measuringContext.font = `${font.weight} ${font.size}px ${FONT_FAMILY}`
  return measuringContext.measureText(text).width
}

const fits = (text: string, width: number, font: Font) => textWidth(text, font) <= width

/** Shortens the text with an ellipsis until it fits the width. */
export function truncate(text: string, width: number, font: Font) {
  if (fits(text, width, font)) {
    return text
  }
  const shortened = (end: number) => `${text.slice(0, end).trimEnd()}${ELLIPSIS}`
  let end = text.length
  while (end > 0 && !fits(shortened(end), width, font)) {
    end--
  }
  return shortened(end)
}

/** Splits a word too long for one line into pieces that fit (each at least one character). */
function breakWord(word: string, width: number, font: Font): string[] {
  const pieces: string[] = []
  let rest = word
  while (!fits(rest, width, font) && rest.length > 1) {
    let cut = rest.length - 1
    while (cut > 1 && !fits(rest.slice(0, cut), width, font)) {
      cut--
    }
    pieces.push(rest.slice(0, cut))
    rest = rest.slice(cut)
  }
  return [...pieces, rest]
}

/** Breaks the text into lines fitting the width (long words are broken too), at most maxLines, the last one shortened if needed. */
export function wrap(text: string, width: number, font: Font, maxLines: number): string[] {
  const lines: string[] = []
  for (const word of text.split(/\s+/).filter(Boolean)) {
    const extended = lines.length > 0 ? `${lines[lines.length - 1]} ${word}` : null
    if (extended !== null && fits(extended, width, font)) {
      lines[lines.length - 1] = extended
    } else {
      lines.push(...breakWord(word, width, font))
    }
  }
  if (lines.length <= maxLines) {
    return lines
  }
  const kept = lines.slice(0, Math.max(1, maxLines))
  kept[kept.length - 1] = truncate(`${kept[kept.length - 1]} ${lines[kept.length]}`, width, font)
  return kept
}
