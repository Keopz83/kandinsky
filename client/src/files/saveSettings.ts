import { useEffect, useRef, useState } from 'react'
import type { Format } from '../types'

const SAVE_FORMAT_KEY = 'kandinsky.saveFormat'
// Mermaid is the most widely used diagrams-as-code format (rendered natively by GitHub, GitLab, Azure DevOps, Notion, …).
const DEFAULT_SAVE_FORMAT: Format = 'mermaid'

function storedSaveFormat(): Format {
  const stored = localStorage.getItem(SAVE_FORMAT_KEY)
  return stored === 'plantuml' || stored === 'mermaid' ? stored : DEFAULT_SAVE_FORMAT
}

/** The format Save offers first, kept in the browser's local storage. */
export function useSaveFormatSetting() {
  const [saveFormat, setSaveFormat] = useState<Format>(storedSaveFormat)
  const changeSaveFormat = (format: Format) => {
    localStorage.setItem(SAVE_FORMAT_KEY, format)
    setSaveFormat(format)
  }
  return [saveFormat, changeSaveFormat] as const
}

/** Ctrl/Cmd+S saves, Ctrl/Cmd+Shift+S saves as. */
export function useSaveShortcut(save: (askForLocation: boolean) => void) {
  // The ref keeps the listener on the latest diagram.
  const latestSave = useRef(save)
  latestSave.current = save
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (!(e.ctrlKey || e.metaKey) || e.altKey || e.key.toLowerCase() !== 's') {
        return
      }
      e.preventDefault()
      if (!e.repeat) {
        latestSave.current(e.shiftKey)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])
}
