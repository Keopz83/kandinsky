import { useEffect, useRef } from 'react'

const isTextInput = (target: EventTarget | null) =>
  target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || (target instanceof HTMLElement && target.isContentEditable)

interface Shortcuts {
  /** Escape: back to plain selection. */
  onCancel: () => void
  /** Delete or Backspace with something selected. */
  onDelete: () => void
  hasSelection: boolean
  /** Ctrl/Cmd+V with text in the clipboard. */
  onPaste: (text: string) => void
}

/** Keyboard and clipboard shortcuts of the whiteboard; they don't apply while typing in a field. */
export function useCanvasShortcuts(shortcuts: Shortcuts) {
  // The ref keeps the window listeners on the latest handlers without re-registering them on every render.
  const latest = useRef(shortcuts)
  latest.current = shortcuts

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) {
        return
      }
      if (e.key === 'Escape') {
        latest.current.onCancel()
      }
      if ((e.key === 'Delete' || e.key === 'Backspace') && latest.current.hasSelection) {
        e.preventDefault()
        latest.current.onDelete()
      }
    }
    function onPaste(e: ClipboardEvent) {
      if (isTextInput(e.target)) {
        return
      }
      const text = e.clipboardData?.getData('text/plain') ?? ''
      if (!text.trim()) {
        return
      }
      e.preventDefault()
      latest.current.onPaste(text)
    }
    window.addEventListener('keydown', onKeyDown)
    window.addEventListener('paste', onPaste)
    return () => {
      window.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('paste', onPaste)
    }
  }, [])
}
