import type { AlignDirection, Diagram, DiagramSummary, Edge, Format, Shape } from './types'

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(`/api/diagrams${path}`, {
    method,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new Error(problem?.detail ?? problem?.title ?? `${response.status} ${response.statusText}`)
  }
  if (response.status === 204) {
    return undefined as T
  }
  const contentType = response.headers.get('Content-Type') ?? ''
  return (contentType.includes('json') ? response.json() : response.text()) as Promise<T>
}

const at = (diagramId: string, ...parts: string[]) =>
  ['', diagramId, ...parts].map(encodeURIComponent).join('/')

export const api = {
  list: () => request<DiagramSummary[]>('GET', ''),
  create: (name: string) => request<Diagram>('POST', '', { name }),
  get: (id: string) => request<Diagram>('GET', at(id)),
  rename: (id: string, name: string) => request<Diagram>('PATCH', at(id), { name }),
  remove: (id: string) => request<void>('DELETE', at(id)),
  exportText: (id: string, format: Format) => request<string>('GET', `${at(id, 'export')}?format=${format}`),
  importText: (text: string) => request<Diagram>('POST', '/import', { text }),
  align: (id: string, shapeIds: string[], direction: AlignDirection) =>
    request<Diagram>('POST', at(id, 'align'), { shapeIds, direction }),
  layout: (id: string) => request<Diagram>('POST', at(id, 'layout')),

  addShape: (id: string, shape: Partial<Omit<Shape, 'id'>>) => request<Shape>('POST', at(id, 'shapes'), shape),
  updateShape: (id: string, shapeId: string, patch: Partial<Omit<Shape, 'id'>>) =>
    request<Shape>('PATCH', at(id, 'shapes', shapeId), patch),
  deleteShape: (id: string, shapeId: string) => request<void>('DELETE', at(id, 'shapes', shapeId)),

  addEdge: (id: string, sourceId: string, targetId: string, label?: string | null) =>
    request<Edge>('POST', at(id, 'edges'), { sourceId, targetId, label }),
  updateEdge: (id: string, edgeId: string, patch: { label?: string }) =>
    request<Edge>('PATCH', at(id, 'edges', edgeId), patch),
  deleteEdge: (id: string, edgeId: string) => request<void>('DELETE', at(id, 'edges', edgeId)),
}
