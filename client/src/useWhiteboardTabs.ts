import { useCallback, useRef, useState } from 'react'
import { api } from './api'
import type { DiagramSummary } from './types'

// The server sorts by name; tabs keep their position and new whiteboards are appended.
function keepOrder(previous: DiagramSummary[], list: DiagramSummary[]) {
  const byId = new Map(list.map((d) => [d.id, d]))
  const kept = previous.flatMap((d) => byId.get(d.id) ?? [])
  const known = new Set(kept.map((d) => d.id))
  return [...kept, ...list.filter((d) => !known.has(d.id))]
}

/** The whiteboards on the server as tabs, in a stable order. */
export function useWhiteboardTabs() {
  const [tabs, setTabs] = useState<DiagramSummary[]>([])
  const current = useRef<DiagramSummary[]>([])
  // Only the most recently requested list may be shown, so a late poll can't drop a just created tab.
  const latestRequest = useRef(0)

  /** Reloads the list; returns it, or null if a newer request superseded this one. */
  const refreshTabs = useCallback(async () => {
    const request = ++latestRequest.current
    const list = await api.list()
    if (request !== latestRequest.current) {
      return null
    }
    current.current = keepOrder(current.current, list)
    setTabs(current.current)
    return current.current
  }, [])

  const discardPendingRefreshes = useCallback(() => {
    latestRequest.current++
  }, [])

  return { tabs, refreshTabs, discardPendingRefreshes }
}
